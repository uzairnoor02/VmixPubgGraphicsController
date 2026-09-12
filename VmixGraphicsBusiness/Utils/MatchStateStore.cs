using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VmixGraphicsBusiness;

namespace VmixGraphicsBusiness.Utils
{
    /// <summary>
    /// In-process, thread-safe replacement for the Redis usage in the live-match hot path
    /// (see Utils/Redis.cs, HelperRedis.* keys). Everything this app previously stored in Redis
    /// lives here instead as plain in-memory state, with a periodic JSON snapshot to disk so a
    /// crash/restart mid-match can recover team positions, elimination flags and match status
    /// without needing Redis (or WSL) to be running at all.
    ///
    /// This is safe to be in-process because every reader/writer of this state lives in the
    /// SAME process (the WinForms host, or its embedded web host) — nothing here is ever shared
    /// across machines, which is the only case that would actually need a networked cache.
    ///
    /// The method surface intentionally mirrors StackExchange.Redis's IDatabase (StringGetAsync /
    /// StringSetAsync / KeyDeleteAsync / KeyExpireAsync) so existing call sites can be swapped
    /// over with minimal, low-risk, mechanical changes.
    /// </summary>
    public sealed class MatchStateStore : IDisposable
    {
        private sealed class Entry
        {
            public string Value = "";
            public DateTime? ExpiresAtUtc;
        }

        private readonly ConcurrentDictionary<string, Entry> _entries = new();
        private readonly string _snapshotPath;
        private readonly Timer _snapshotTimer;
        private volatile bool _dirty;

        /// <summary>Raised whenever PublishMatchStatus is called — the in-process replacement for
        /// the old Redis pub/sub "match-status-channel". A SignalR hub (or anything else in-process)
        /// can subscribe to push live updates to connected clients.</summary>
        public event Action<string>? MatchStatusChanged;

        /// <summary>Raised once per poll tick with the freshly computed team board, so a SignalR
        /// hub (or anything else in-process) can push it straight to connected dashboard clients
        /// without polling this store itself.</summary>
        public event Action<List<TeamLiveStats>>? LiveTeamsUpdated;

        public void PublishLiveTeams(List<TeamLiveStats> teams)
        {
            try { LiveTeamsUpdated?.Invoke(teams); } catch { /* subscriber's problem, never ours */ }
        }

        /// <summary>Raised whenever CreateTop4LiveRanking recomputes the final-4 win-probability
        /// board (already pushed straight to vMix's native Title graphics via ApiCallProcessor) -
        /// so the web overlay's Top 4 / WWCD panel can show the same real numbers instead of the
        /// placeholder 0% it fell back to before this existed. Top4TeamStats already carries
        /// per-player HealthPercent/LiveState, so no new DTO was needed here.</summary>
        public event Action<List<VmixGraphicsBusiness.LiveMatch.LiveStatsBusiness.Top4TeamStats>>? Top4RankingsUpdated;

        public void PublishTop4Rankings(List<VmixGraphicsBusiness.LiveMatch.LiveStatsBusiness.Top4TeamStats> teams)
        {
            try { Top4RankingsUpdated?.Invoke(teams); } catch { /* subscriber's problem, never ours */ }
        }

        /// <summary>Raised whenever a player achievement (grenade elim, vehicle kill, airdrop
        /// loot, first blood, ...) is newly detected in SetPlayerAcheivments.cs, so the web
        /// overlay (LiveDashboardHost.cs, in the Pubg Ranking System project) can push a live
        /// banner without VmixGraphicsBusiness needing a reference to that project - this event
        /// is the one-way hand-off point between the two.</summary>
        public event Action<LiveAchievementEvent>? AchievementTriggered;

        public void PublishAchievement(LiveAchievementEvent achievementEvent)
        {
            try { AchievementTriggered?.Invoke(achievementEvent); } catch { /* subscriber's problem, never ours */ }
        }

        /// <summary>Raised the moment a team is newly detected as fully eliminated
        /// (LiveStatsBusiness.IsEliminatedAsync - the same guarded, fires-once-per-team spot that
        /// already pushes the "TEAM ELIMINATED" vMix Title banner), so the web overlay's
        /// full-screen banner can show the real thing instead of its derived fallback.</summary>
        public event Action<LiveTeamEliminatedEvent>? TeamEliminated;

        public void PublishTeamEliminated(LiveTeamEliminatedEvent teamEliminatedEvent)
        {
            try { TeamEliminated?.Invoke(teamEliminatedEvent); } catch { /* subscriber's problem, never ours */ }
        }

        public MatchStateStore(string? snapshotDirectory = null)
        {
            var dir = snapshotDirectory ?? Path.Combine(AppContext.BaseDirectory, "state");
            try { Directory.CreateDirectory(dir); } catch { /* best effort */ }
            _snapshotPath = Path.Combine(dir, "match_state_snapshot.json");
            LoadSnapshotFromDisk();
            // Snapshot every 5s if anything changed, and purge expired keys on the same tick.
            _snapshotTimer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        // ---- Redis-shaped API (string get/set/delete/expire) ----

        public string StringGet(string key)
        {
            if (_entries.TryGetValue(key, out var entry) && !IsExpired(entry))
                return entry.Value;
            return string.Empty;
        }

        public Task<string> StringGetAsync(string key) => Task.FromResult(StringGet(key));

        public void StringSet(string key, string value, TimeSpan? expiry = null)
        {
            _entries[key] = new Entry
            {
                Value = value,
                ExpiresAtUtc = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : null
            };
            _dirty = true;
        }

        public Task StringSetAsync(string key, string value, TimeSpan? expiry = null)
        {
            StringSet(key, value, expiry);
            return Task.CompletedTask;
        }

        public Task<bool> KeyDeleteAsync(string key)
        {
            _dirty = true;
            return Task.FromResult(_entries.TryRemove(key, out _));
        }

        /// <summary>Redis-KEYS-pattern-shaped bulk delete, supporting only the trailing "*" prefix
        /// wildcard this codebase actually used (e.g. "VehicleEliminations:*"). A pattern with no
        /// "*" deletes that single exact key. Returns the number of keys removed.</summary>
        public Task<int> DeleteByPatternAsync(string pattern)
        {
            int removed = 0;
            if (pattern.EndsWith("*", StringComparison.Ordinal))
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                foreach (var key in _entries.Keys)
                {
                    if (key.StartsWith(prefix, StringComparison.Ordinal) && _entries.TryRemove(key, out _))
                        removed++;
                }
            }
            else if (_entries.TryRemove(pattern, out _))
            {
                removed++;
            }
            if (removed > 0) _dirty = true;
            return Task.FromResult(removed);
        }

        public Task KeyExpireAsync(string key, TimeSpan expiry)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                entry.ExpiresAtUtc = DateTime.UtcNow.Add(expiry);
                _dirty = true;
            }
            return Task.CompletedTask;
        }

        public bool IsNullOrEmpty(string? value) => string.IsNullOrEmpty(value);

        // ---- typed JSON convenience helpers (mirrors the old RedisCache.GetAsync<T>/SetAsync<T>) ----

        public async Task<T?> GetAsync<T>(string key)
        {
            var raw = await StringGetAsync(key).ConfigureAwait(false);
            if (string.IsNullOrEmpty(raw)) return default;
            try { return JsonSerializer.Deserialize<T>(raw); }
            catch { return default; }
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var json = JsonSerializer.Serialize(value);
            return StringSetAsync(key, json, expiry);
        }

        // ---- match-lifecycle helpers ----

        /// <summary>Clears every match-scoped key (Top4 position locks, elimination flags, cached
        /// player/team lists, active ranking guids). Call this on match start AND on Reset, so a
        /// stale mapping from a previous match can never leak into the next one — this is the fix
        /// for the "Reset.cs never clears Top4TeamPositions" bug.</summary>
        public void ResetMatchState()
        {
            foreach (var key in _entries.Keys)
            {
                if (key.StartsWith(HelperRedis.isEliminated, StringComparison.Ordinal) ||
                    key == "Top4TeamPositions" ||
                    key == "Top4RankingGuid" ||
                    key == "LiveRankingGuid" ||
                    key == HelperRedis.TeamInfoList ||
                    key == HelperRedis.PlayerInfolist ||
                    key == HelperRedis.MatchId)
                {
                    _entries.TryRemove(key, out _);
                }
            }
            _dirty = true;
        }

        public void PublishMatchStatus(string status)
        {
            // fire-and-forget the persisted value; ignore the Task since this is a sync-style event hook
            _ = StringSetAsync(HelperRedis.MatchStatus, status);
            try { MatchStatusChanged?.Invoke(status); } catch { /* subscriber's problem, never ours */ }
        }

        // ---- internals ----

        private static bool IsExpired(Entry entry) =>
            entry.ExpiresAtUtc.HasValue && entry.ExpiresAtUtc.Value <= DateTime.UtcNow;

        private void Tick()
        {
            foreach (var kvp in _entries)
            {
                if (IsExpired(kvp.Value))
                {
                    _entries.TryRemove(kvp.Key, out _);
                    _dirty = true;
                }
            }
            SnapshotIfDirty();
        }

        private void SnapshotIfDirty()
        {
            if (!_dirty) return;
            try
            {
                var toSave = new Dictionary<string, Entry>();
                foreach (var kvp in _entries)
                {
                    if (!IsExpired(kvp.Value)) toSave[kvp.Key] = kvp.Value;
                }
                var json = JsonSerializer.Serialize(toSave);
                var tmpPath = _snapshotPath + ".tmp";
                File.WriteAllText(tmpPath, json);
                File.Copy(tmpPath, _snapshotPath, overwrite: true);
                File.Delete(tmpPath);
                _dirty = false;
            }
            catch
            {
                // Snapshotting is best-effort crash recovery, never allowed to break the live pipeline.
            }
        }

        private void LoadSnapshotFromDisk()
        {
            try
            {
                if (!File.Exists(_snapshotPath)) return;
                var json = File.ReadAllText(_snapshotPath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, Entry>>(json);
                if (loaded == null) return;
                foreach (var kvp in loaded)
                {
                    if (!IsExpired(kvp.Value))
                        _entries[kvp.Key] = kvp.Value;
                }
            }
            catch
            {
                // A corrupt/missing snapshot just means we start from an empty state — never fatal.
            }
        }

        public void Dispose() => _snapshotTimer?.Dispose();
    }

    /// <summary>One newly-detected player achievement, handed from SetPlayerAcheivments.cs to
    /// MatchStateStore.PublishAchievement and on to the web overlay. `Type` matches the id the
    /// Overlay Settings tab / Overlay.tsx use, e.g. "achievement.grenadeElim".</summary>
    public record LiveAchievementEvent(string Type, string PlayerName, string? TeamTag);

    /// <summary>One team newly confirmed fully eliminated, handed from
    /// LiveStatsBusiness.IsEliminatedAsync to MatchStateStore.PublishTeamEliminated and on to the
    /// web overlay's full-screen banner.</summary>
    public record LiveTeamEliminatedEvent(string TeamName, int TeamId, int TotalEliminations, int Rank);
}
