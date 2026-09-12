using VmixData.Models;
using VmixData.Models.MatchModels;

namespace VmixGraphicsBusiness.Utils
{
    /// <summary>
    /// Tracks which match is "live" for the purposes of the remote-agent ingest path (see
    /// IngestApi.cs in the Pubg Ranking System project, and VmixIngestAgent/ - the small program
    /// that runs on the customer's PC next to pcob, since the main application no longer has to
    /// run on that same machine). This is the agent-fed equivalent of the `Match match` parameter
    /// GetLiveData.FetchAndPostData already carries as a local variable for the same purpose when
    /// this app polls pcob directly itself - both paths end up calling the same
    /// LiveStatsBusiness.CreateDynamicLiveStats, just fed from a different source.
    ///
    /// Singleton, in-process only (same lifetime/durability tradeoff as MatchStateStore) - one
    /// graphics PC, one active match at a time.
    /// </summary>
    public class IngestCoordinator
    {
        private readonly object _lock = new();

        public Match? CurrentMatch { get; private set; }
        public List<LiveTeamPointStats>? TeamPoints { get; private set; }

        /// <summary>True once at least one "in game" tick has been ingested for the current
        /// match - lets the ingest endpoint detect the in-game -> not-in-game transition (the
        /// signal to run post-match processing) instead of treating "no ticks yet" the same as
        /// "match just ended".</summary>
        public bool WasInGame { get; set; }

        public void SetActiveMatch(Match match, List<LiveTeamPointStats> teamPoints)
        {
            lock (_lock)
            {
                CurrentMatch = match;
                TeamPoints = teamPoints;
                WasInGame = false;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                CurrentMatch = null;
                TeamPoints = null;
                WasInGame = false;
            }
        }
    }
}
