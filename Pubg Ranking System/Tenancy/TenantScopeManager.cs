using System.Collections.Concurrent;
using VmixGraphicsBusiness.Observability;
using VmixGraphicsBusiness.Tenancy;
using VmixGraphicsBusiness.Utils;

namespace Pubg_Ranking_System.Tenancy
{
    /// <summary>
    /// Owns one <see cref="TenantScope"/> per tournament and is the single place that decides
    /// which one a request belongs to.
    ///
    /// Also the log fan-out point: a record tagged with a tournament lands in that tournament's
    /// ring and in the process-wide ring, so the Logs tab can show one event or everything, and a
    /// process-level failure — the class of failure that used to disappear into a console window
    /// nobody was watching — still has somewhere to surface.
    /// </summary>
    public sealed class TenantScopeManager : IDisposable
    {
        /// <summary>
        /// Id of the implicit tournament that represents "this install, no tenancy configured".
        /// Fixed rather than random so the default scope survives a restart with the same state
        /// directory and the same overlay token.
        /// </summary>
        public const string DefaultTournamentId = "default";

        private readonly TournamentRegistry _registry;
        private readonly string _stateRoot;
        private readonly ConcurrentDictionary<string, TenantScope> _scopes = new(StringComparer.Ordinal);

        public TenantScopeManager(
            TournamentRegistry registry,
            MatchStateStore defaultMatchState,
            OverlayConfigStore defaultOverlayConfig,
            IngestCoordinator defaultIngest,
            string? stateRoot = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _stateRoot = string.IsNullOrWhiteSpace(stateRoot)
                ? Path.Combine(AppContext.BaseDirectory, "state")
                : stateRoot!;

            try { Directory.CreateDirectory(Path.Combine(_stateRoot, "tournaments")); } catch { /* best effort */ }

            ProcessLogs = new LogBuffer(1500);

            // The default tenant is created eagerly and wraps the singletons DI already built, so
            // there is never a moment where the app is running with no resolvable tenant.
            var defaultRecord = _registry.EnsureDefaultTournament(DefaultTournamentId);
            Default = TenantScope.CreateDefault(defaultRecord, defaultMatchState, defaultOverlayConfig, defaultIngest, _stateRoot);
            _scopes[DefaultTournamentId] = Default;
        }

        /// <summary>The scope wrapping the process's original singletons.</summary>
        public TenantScope Default { get; }

        /// <summary>Process-wide log ring: startup, shutdown, and anything with no tenant.</summary>
        public LogBuffer ProcessLogs { get; }

        public event Action<TenantScope>? ScopeCreated;

        /// <summary>Fires for every appended record, tenant-tagged or not. Used to push to SignalR.</summary>
        public event Action<LogRecord>? LogAppended;

        public TenantScope GetOrCreate(TournamentRecord tournament)
        {
            if (tournament is null) throw new ArgumentNullException(nameof(tournament));

            if (_scopes.TryGetValue(tournament.Id, out var existing)) return existing;

            var created = false;
            var scope = _scopes.GetOrAdd(tournament.Id, id =>
            {
                created = true;
                return TenantScope.Create(tournament, Path.Combine(_stateRoot, "tournaments", id));
            });

            if (created)
            {
                Publish("Information", "tenancy", "tenant scope created", tournament.Id,
                    properties: new Dictionary<string, string> { ["stateDir"] = scope.StateDirectory });
                try { ScopeCreated?.Invoke(scope); } catch { /* a subscriber cannot break tenant creation */ }
            }

            return scope;
        }

        public TenantScope? GetOrCreate(string? tournamentId)
        {
            if (string.IsNullOrWhiteSpace(tournamentId)) return null;
            if (_scopes.TryGetValue(tournamentId!, out var existing)) return existing;
            var record = _registry.Get(tournamentId);
            return record is null ? null : GetOrCreate(record);
        }

        public TenantScope? TryGet(string? tournamentId)
        {
            if (string.IsNullOrWhiteSpace(tournamentId)) return null;
            return _scopes.TryGetValue(tournamentId!, out var scope) ? scope : null;
        }

        public IReadOnlyList<TenantScope> ActiveScopes() => _scopes.Values.ToList();

        /// <summary>Drops a scope and releases its stores. The default scope is never evictable.</summary>
        public bool Evict(string tournamentId)
        {
            if (string.Equals(tournamentId, DefaultTournamentId, StringComparison.Ordinal)) return false;
            if (!_scopes.TryRemove(tournamentId, out var scope)) return false;
            scope.Dispose();
            Publish("Information", "tenancy", "tenant scope evicted", tournamentId);
            return true;
        }

        // ------------------------------------------------------------------ logging

        /// <summary>
        /// Single entry point for structured logging, so no call site has to remember to write to
        /// both the tenant ring and the process ring.
        /// </summary>
        public LogRecord Publish(LogRecord record)
        {
            if (record is null) throw new ArgumentNullException(nameof(record));

            if (!string.IsNullOrEmpty(record.TournamentId))
            {
                var scope = TryGet(record.TournamentId);
                if (scope is not null)
                {
                    if (string.IsNullOrEmpty(record.TournamentName)) record.TournamentName = scope.TournamentName;
                    scope.Logs.Append(record);
                }
            }

            ProcessLogs.Append(record);

            var handler = LogAppended;
            if (handler is not null)
            {
                try { handler(record); } catch { /* a log subscriber must never fail a log call */ }
            }
            return record;
        }

        public LogRecord Publish(
            string level,
            string category,
            string message,
            string? tournamentId = null,
            string? error = null,
            Dictionary<string, string>? properties = null)
            => Publish(new LogRecord
            {
                Level = level,
                Category = category,
                Message = message,
                TournamentId = tournamentId,
                Error = error,
                Properties = properties
            });

        /// <summary>
        /// Log slice for the dashboard. With no tournament id this is the merged process view;
        /// with one it is that tenant's ring only.
        /// </summary>
        public IReadOnlyList<LogRecord> SnapshotLogs(
            string? tournamentId = null, int max = 200, string? minLevel = null, long afterSeq = 0)
        {
            if (string.IsNullOrWhiteSpace(tournamentId))
                return ProcessLogs.Snapshot(max, minLevel, afterSeq);

            var scope = TryGet(tournamentId);
            return scope is null
                ? Array.Empty<LogRecord>()
                : scope.Logs.Snapshot(max, minLevel, afterSeq);
        }

        public void Dispose()
        {
            foreach (var scope in _scopes.Values) scope.Dispose();
            _scopes.Clear();
        }
    }
}
