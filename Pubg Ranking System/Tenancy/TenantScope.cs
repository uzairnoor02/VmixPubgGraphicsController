using VmixGraphicsBusiness.Observability;
using VmixGraphicsBusiness.Tenancy;
using VmixGraphicsBusiness.Utils;

namespace Pubg_Ranking_System.Tenancy
{
    /// <summary>
    /// One tournament's slice of everything that is currently a process-wide singleton.
    ///
    /// Today <c>Program.cs</c> registers exactly one <see cref="MatchStateStore"/>, one
    /// <see cref="OverlayConfigStore"/> and one <see cref="IngestCoordinator"/>, and all three
    /// write their snapshots into the same <c>state/</c> folder. That is correct for one graphics
    /// PC running one event, and it is the reason two tournaments cannot share a host: they would
    /// overwrite each other's match state on disk and each other's overlay config in memory, and
    /// the second tournament's ticks would be rejected by the first one's sequence window.
    ///
    /// A scope fixes that by giving each tournament its own instances and its own
    /// <c>state/tournaments/{id}/</c> directory, without changing the shape of any of those three
    /// classes. Crucially, the single-tenant install keeps working unchanged: the default scope is
    /// constructed from the *existing* singletons rather than new instances (see
    /// <see cref="CreateDefault"/>), so the machine running the 28 Sep tournament behaves exactly
    /// as it does now, snapshot paths included.
    /// </summary>
    public sealed class TenantScope : IDisposable
    {
        private readonly bool _ownsStores;

        private TenantScope(
            TournamentRecord tournament,
            string stateDirectory,
            MatchStateStore matchState,
            OverlayConfigStore overlayConfig,
            IngestCoordinator ingest,
            bool ownsStores)
        {
            Tournament = tournament;
            StateDirectory = stateDirectory;
            MatchState = matchState;
            OverlayConfig = overlayConfig;
            Ingest = ingest;
            _ownsStores = ownsStores;

            Logs = new LogBuffer();
            Agent = new AgentHealth();
        }

        /// <summary>
        /// A scope for an additional tournament: fresh stores, isolated state directory.
        /// </summary>
        public static TenantScope Create(TournamentRecord tournament, string stateDirectory)
        {
            if (tournament is null) throw new ArgumentNullException(nameof(tournament));
            Directory.CreateDirectory(stateDirectory);

            return new TenantScope(
                tournament,
                stateDirectory,
                new MatchStateStore(stateDirectory),
                new OverlayConfigStore(stateDirectory),
                new IngestCoordinator(),
                ownsStores: true);
        }

        /// <summary>
        /// The scope that wraps the process's original singletons. This is what makes multi-tenancy
        /// additive rather than a rewrite: every existing call site keeps talking to the same
        /// instance it always did, and only requests that arrive with a tournament credential get
        /// routed to a different scope.
        ///
        /// <paramref name="ownsStores"/> is false here on purpose — the DI container created these
        /// and owns their lifetime, so disposing this scope must not dispose them.
        /// </summary>
        public static TenantScope CreateDefault(
            TournamentRecord tournament,
            MatchStateStore matchState,
            OverlayConfigStore overlayConfig,
            IngestCoordinator ingest,
            string stateDirectory)
        {
            if (tournament is null) throw new ArgumentNullException(nameof(tournament));
            return new TenantScope(tournament, stateDirectory, matchState, overlayConfig, ingest, ownsStores: false);
        }

        public TournamentRecord Tournament { get; }
        public string TournamentId => Tournament.Id;
        public string TournamentName => Tournament.Name;

        /// <summary>Where this tournament's snapshot files live.</summary>
        public string StateDirectory { get; }

        public MatchStateStore MatchState { get; }
        public OverlayConfigStore OverlayConfig { get; }
        public IngestCoordinator Ingest { get; }

        /// <summary>Recent structured log events for this tournament, as shown in the Logs tab.</summary>
        public LogBuffer Logs { get; }

        /// <summary>Liveness of this tournament's ingest agent.</summary>
        public AgentHealth Agent { get; }

        /// <summary>True for the scope that wraps the process's original singletons.</summary>
        public bool IsDefault => !_ownsStores;

        public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;

        /// <summary>
        /// The SignalR group every client watching this tournament joins. Group naming is derived
        /// from the tournament id, never from the overlay token — a group name is not a secret and
        /// would otherwise leak the token to anything that can enumerate groups or read a log.
        /// </summary>
        public string HubGroup => GroupFor(TournamentId);

        public static string GroupFor(string tournamentId) => "tournament:" + tournamentId;

        /// <summary>
        /// Call on match start and on reset. The audit found the Top4 position lock surviving into
        /// the following match; a stale ingest sequence window is the same bug in a different
        /// place, and it fails harder — every tick of the new match is rejected as stale until the
        /// counter climbs past the old high-water mark.
        /// </summary>
        public void ResetForNewMatch()
        {
            Agent.Reset();
            Ingest.Clear();
        }

        public void Dispose()
        {
            if (_ownsStores) MatchState.Dispose();
        }
    }
}
