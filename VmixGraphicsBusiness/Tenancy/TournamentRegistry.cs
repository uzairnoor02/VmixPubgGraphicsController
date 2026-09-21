#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace VmixGraphicsBusiness.Tenancy
{
    /// <summary>Why a credential was rejected. Callers log this; they never return it to the caller verbatim.</summary>
    public enum TenantResolution
    {
        Ok,
        /// <summary>Well-formed but unknown — or deliberately indistinguishable from unknown.</summary>
        NotFound,
        /// <summary>Correct key, but the tournament is switched off.</summary>
        TournamentDisabled,
        /// <summary>Correct key, but retired and past its grace window.</summary>
        KeyRetired,
        /// <summary>Failed shape/checksum validation — almost always a typo or a truncated paste.</summary>
        Malformed
    }

    public readonly struct TenantLookup
    {
        public TenantLookup(TenantResolution resolution, TournamentRecord? tournament, IssuedKey? key, bool usedGracePeriod)
        {
            Resolution = resolution;
            Tournament = tournament;
            Key = key;
            UsedGracePeriod = usedGracePeriod;
        }

        public TenantResolution Resolution { get; }
        public TournamentRecord? Tournament { get; }
        public IssuedKey? Key { get; }

        /// <summary>
        /// True when the presented key had already been rotated and only worked because of its
        /// grace window. Worth logging as a warning: it means an install is still using an old
        /// credential and will break when the window closes.
        /// </summary>
        public bool UsedGracePeriod { get; }

        public bool Success => Resolution == TenantResolution.Ok && Tournament != null;

        public static TenantLookup Fail(TenantResolution reason) => new TenantLookup(reason, null, null, false);
    }

    /// <summary>
    /// The tenant directory: which tournaments exist, and which credentials open them.
    ///
    /// Deliberately file-backed and dependency-free, matching the pattern
    /// <c>MatchStateStore</c>/<c>OverlayConfigStore</c> already use (in-process state plus a JSON
    /// snapshot under <c>state/</c>). That keeps this phase independent of the still-open
    /// MySQL-vs-Postgres decision: when the database story is settled, only the two
    /// <see cref="Load"/>/<see cref="Save"/> methods change, and nothing that calls the registry
    /// has to know.
    ///
    /// All public members are safe to call concurrently.
    /// </summary>
    public sealed class TournamentRegistry
    {
        /// <summary>Default grace window on rotation: long enough to survive a match, short enough to matter.</summary>
        public static readonly TimeSpan DefaultRotationGrace = TimeSpan.FromMinutes(45);

        private readonly object _gate = new object();
        private readonly string _snapshotPath;

        private readonly Dictionary<string, TournamentRecord> _byId =
            new Dictionary<string, TournamentRecord>(StringComparer.Ordinal);

        // Overlay tokens are looked up by plaintext (they arrive in a URL).
        private readonly Dictionary<string, string> _overlayTokenToTournament =
            new Dictionary<string, string>(StringComparer.Ordinal);

        // Agent/dashboard keys are looked up by hash — the plaintext is never stored.
        private readonly Dictionary<string, string> _hashToTournament =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public TournamentRegistry(string stateDirectory)
        {
            if (string.IsNullOrWhiteSpace(stateDirectory)) stateDirectory = "state";
            Directory.CreateDirectory(stateDirectory);
            _snapshotPath = Path.Combine(stateDirectory, "tournaments.json");
            Load();
        }

        /// <summary>Raised after any mutation, so the dashboard can be pushed a fresh list.</summary>
        public event Action? Changed;

        // ---------------------------------------------------------------- reads

        public IReadOnlyList<TournamentRecord> All()
        {
            lock (_gate) return _byId.Values.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public TournamentRecord? Get(string? tournamentId)
        {
            if (string.IsNullOrWhiteSpace(tournamentId)) return null;
            lock (_gate) return _byId.TryGetValue(tournamentId!, out var rec) ? rec : null;
        }

        public int Count { get { lock (_gate) return _byId.Count; } }

        // ---------------------------------------------------------------- writes

        /// <summary>
        /// A newly created tournament, together with the plaintext credentials generated for it.
        /// The agent key appears here and nowhere else — the registry stores only its hash — so a
        /// caller that drops this result has permanently lost that key and must rotate to get
        /// another.
        /// </summary>
        public sealed class TournamentCreation
        {
            public TournamentCreation(TournamentRecord record, string overlayToken, string agentKey)
            {
                Record = record;
                OverlayToken = overlayToken;
                AgentKey = agentKey;
            }

            public TournamentRecord Record { get; }
            public string OverlayToken { get; }
            public string AgentKey { get; }
        }

        /// <summary>
        /// Creates a tournament and issues the two credentials it cannot operate without, so there
        /// is never a window in which a tournament exists but cannot be pointed at vMix or fed by
        /// an agent.
        /// </summary>
        public TournamentCreation CreateWithKeys(string name, string? orgId = null)
        {
            var record = new TournamentRecord
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Untitled tournament" : name.Trim(),
                OrgId = orgId
            };

            string overlayToken;
            string agentKey;

            lock (_gate)
            {
                _byId[record.Id] = record;
                overlayToken = IssueKeyLocked(record, TournamentKeyKind.Overlay, "initial").plaintext;
                agentKey = IssueKeyLocked(record, TournamentKeyKind.Agent, "initial").plaintext;
                Reindex();
                Save();
            }

            Changed?.Invoke();
            return new TournamentCreation(record, overlayToken, agentKey);
        }

        public TournamentRecord Create(string name, string? orgId = null)
            => CreateWithKeys(name, orgId).Record;

        /// <summary>
        /// Returns the implicit "this install" tournament, creating it on first run.
        ///
        /// This is what lets tenancy ship without a migration: an existing single-tenant install
        /// starts up, gets a default tournament with a real overlay token and a real agent key,
        /// and carries on — while the legacy shared secrets in appsettings.json keep working
        /// alongside them until the operator chooses to switch over.
        /// </summary>
        public TournamentRecord EnsureDefaultTournament(string id, string name = "This install")
        {
            bool created = false;
            TournamentRecord record;

            lock (_gate)
            {
                if (!_byId.TryGetValue(id, out var existing))
                {
                    created = true;
                    existing = new TournamentRecord { Id = id, Name = name };
                    _byId[id] = existing;
                    IssueKeyLocked(existing, TournamentKeyKind.Overlay, "initial");
                    IssueKeyLocked(existing, TournamentKeyKind.Agent, "initial");
                    Reindex();
                    Save();
                }
                record = existing;
            }

            if (created) Changed?.Invoke();
            return record;
        }

        public bool Rename(string tournamentId, string name)
        {
            lock (_gate)
            {
                if (!_byId.TryGetValue(tournamentId, out var rec)) return false;
                rec.Name = string.IsNullOrWhiteSpace(name) ? rec.Name : name.Trim();
                Save();
            }
            Changed?.Invoke();
            return true;
        }

        public bool SetEnabled(string tournamentId, bool enabled)
        {
            lock (_gate)
            {
                if (!_byId.TryGetValue(tournamentId, out var rec)) return false;
                rec.Enabled = enabled;
                Save();
            }
            Changed?.Invoke();
            return true;
        }

        public bool SetSchedule(string tournamentId, DateTime? startUtc, DateTime? endUtc)
        {
            lock (_gate)
            {
                if (!_byId.TryGetValue(tournamentId, out var rec)) return false;
                rec.ScheduledStartUtc = startUtc;
                rec.ScheduledEndUtc = endUtc;
                Save();
            }
            Changed?.Invoke();
            return true;
        }

        public bool Delete(string tournamentId)
        {
            lock (_gate)
            {
                if (!_byId.Remove(tournamentId)) return false;
                Reindex();
                Save();
            }
            Changed?.Invoke();
            return true;
        }

        // ---------------------------------------------------------------- keys

        /// <summary>
        /// Issues an additional credential of a kind. The plaintext is returned exactly once; for
        /// agent and dashboard keys it is not recoverable afterwards.
        /// </summary>
        public (IssuedKey key, string plaintext)? IssueKey(string tournamentId, TournamentKeyKind kind, string? label = null)
        {
            (IssuedKey, string)? result;
            lock (_gate)
            {
                if (!_byId.TryGetValue(tournamentId, out var rec)) return null;
                result = IssueKeyLocked(rec, kind, label);
                Reindex();
                Save();
            }
            Changed?.Invoke();
            return result;
        }

        /// <summary>
        /// Replaces the current credential of a kind, retiring the old one with a grace window
        /// rather than killing it immediately — a rotation during a live broadcast must not blank
        /// the graphics. Pass <see cref="TimeSpan.Zero"/> for an immediate cut-off.
        /// </summary>
        public (IssuedKey key, string plaintext)? RotateKey(
            string tournamentId, TournamentKeyKind kind, TimeSpan? grace = null, string? label = null)
        {
            var window = grace ?? DefaultRotationGrace;
            (IssuedKey, string)? result;

            lock (_gate)
            {
                if (!_byId.TryGetValue(tournamentId, out var rec)) return null;

                var now = DateTime.UtcNow;
                foreach (var existing in rec.Keys.Where(k => k.Kind == kind && k.IsCurrent))
                {
                    existing.RetiredAtUtc = now;
                    existing.GraceSeconds = (int)Math.Max(0, window.TotalSeconds);
                }

                result = IssueKeyLocked(rec, kind, label ?? "rotated");
                Reindex();
                Save();
            }

            Changed?.Invoke();
            return result;
        }

        /// <summary>Immediate revocation — no grace. Use when a key is believed leaked.</summary>
        public bool RevokeKey(string tournamentId, string keyId)
        {
            lock (_gate)
            {
                if (!_byId.TryGetValue(tournamentId, out var rec)) return false;
                var key = rec.Keys.FirstOrDefault(k => k.Id == keyId);
                if (key == null) return false;
                key.RetiredAtUtc = DateTime.UtcNow;
                key.GraceSeconds = 0;
                Reindex();
                Save();
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Drops keys that are past their grace window. Safe to call on a timer.</summary>
        public int PruneExpiredKeys()
        {
            int removed;
            lock (_gate)
            {
                var now = DateTime.UtcNow;
                removed = 0;
                foreach (var rec in _byId.Values)
                    removed += rec.Keys.RemoveAll(k => k.IsExpired(now));
                if (removed > 0) { Reindex(); Save(); }
            }
            if (removed > 0) Changed?.Invoke();
            return removed;
        }

        // ---------------------------------------------------------------- resolution

        /// <summary>
        /// Resolves an overlay token from a URL. Note the ordering: shape is checked before any
        /// lookup, so a scan of random strings is rejected without touching the directory.
        /// </summary>
        public TenantLookup ResolveOverlayToken(string? token)
            => Resolve(token, TournamentKeyKind.Overlay);

        /// <summary>Resolves an agent key presented in the <c>X-Agent-Key</c> header.</summary>
        public TenantLookup ResolveAgentKey(string? key)
            => Resolve(key, TournamentKeyKind.Agent);

        /// <summary>Resolves a dashboard key or API key.</summary>
        public TenantLookup ResolveDashboardKey(string? key)
            => Resolve(key, TournamentKeyKind.Dashboard);

        private TenantLookup Resolve(string? presented, TournamentKeyKind expectedKind)
        {
            if (!TournamentKey.TryParse(presented, out var kind) || kind != expectedKind)
                return TenantLookup.Fail(TenantResolution.Malformed);

            var value = presented!.Trim();
            var now = DateTime.UtcNow;

            lock (_gate)
            {
                string? tournamentId;
                if (expectedKind == TournamentKeyKind.Overlay)
                    _overlayTokenToTournament.TryGetValue(value, out tournamentId);
                else
                    _hashToTournament.TryGetValue(TournamentKey.Hash(value), out tournamentId);

                if (tournamentId == null || !_byId.TryGetValue(tournamentId, out var rec))
                    return TenantLookup.Fail(TenantResolution.NotFound);

                IssuedKey? matched = null;
                foreach (var k in rec.Keys)
                {
                    if (k.Kind != expectedKind) continue;
                    var hit = expectedKind == TournamentKeyKind.Overlay
                        ? string.Equals(k.Plaintext, value, StringComparison.Ordinal)
                        : TournamentKey.HashMatches(value, k.Hash);
                    if (hit) { matched = k; break; }
                }

                if (matched == null) return TenantLookup.Fail(TenantResolution.NotFound);
                if (!matched.IsUsableAt(now)) return TenantLookup.Fail(TenantResolution.KeyRetired);
                if (!rec.Enabled) return TenantLookup.Fail(TenantResolution.TournamentDisabled);

                matched.LastUsedAtUtc = now;
                return new TenantLookup(TenantResolution.Ok, rec, matched, usedGracePeriod: !matched.IsCurrent);
            }
        }

        // ---------------------------------------------------------------- internals

        private (IssuedKey key, string plaintext) IssueKeyLocked(TournamentRecord rec, TournamentKeyKind kind, string? label)
        {
            var plaintext = TournamentKey.Generate(kind);
            var key = new IssuedKey
            {
                Kind = kind,
                Hash = TournamentKey.Hash(plaintext),
                Mask = TournamentKey.Mask(plaintext),
                Label = label,
                // See TournamentKey's class comment for why only overlay tokens keep plaintext.
                Plaintext = kind == TournamentKeyKind.Overlay ? plaintext : null
            };
            rec.Keys.Add(key);
            return (key, plaintext);
        }

        private void Reindex()
        {
            _overlayTokenToTournament.Clear();
            _hashToTournament.Clear();

            foreach (var rec in _byId.Values)
            foreach (var key in rec.Keys)
            {
                if (key.Kind == TournamentKeyKind.Overlay)
                {
                    if (!string.IsNullOrEmpty(key.Plaintext))
                        _overlayTokenToTournament[key.Plaintext!] = rec.Id;
                }
                else if (!string.IsNullOrEmpty(key.Hash))
                {
                    _hashToTournament[key.Hash] = rec.Id;
                }
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_snapshotPath)) return;
                var json = File.ReadAllText(_snapshotPath);
                if (string.IsNullOrWhiteSpace(json)) return;

                var records = JsonSerializer.Deserialize<List<TournamentRecord>>(json, JsonOpts);
                if (records == null) return;

                lock (_gate)
                {
                    _byId.Clear();
                    foreach (var rec in records)
                        if (!string.IsNullOrWhiteSpace(rec.Id)) _byId[rec.Id] = rec;
                    Reindex();
                }
            }
            catch (Exception ex)
            {
                // A corrupt snapshot must not stop the app from starting — that failure mode is
                // exactly what the audit flagged (silently swallowed startup exceptions), so this
                // moves the bad file aside and carries on with an empty directory, loudly.
                try
                {
                    var quarantine = _snapshotPath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    File.Move(_snapshotPath, quarantine);
                    Console.Error.WriteLine(
                        "[tenancy] tournaments.json could not be read (" + ex.Message +
                        "); moved to " + quarantine + " and starting with an empty registry.");
                }
                catch
                {
                    Console.Error.WriteLine("[tenancy] tournaments.json unreadable and could not be quarantined: " + ex.Message);
                }
            }
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_byId.Values.ToList(), JsonOpts);
                // Write-then-replace, so a crash mid-write cannot leave a truncated directory
                // behind — losing every tournament's keys is not a recoverable state.
                var temp = _snapshotPath + ".tmp";
                File.WriteAllText(temp, json);
                if (File.Exists(_snapshotPath)) File.Replace(temp, _snapshotPath, null);
                else File.Move(temp, _snapshotPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[tenancy] failed to persist tournaments.json: " + ex.Message);
            }
        }
    }
}
