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
    ///
    /// It also owns the tick-ordering window for that match (see TryAdmitTick / TryMarkPublished).
    /// The agent posts ticks strictly in order, but "posted in order" is not "arrives in order"
    /// once the agent is on a customer's PC and this app is hosted centrally: a tick whose HTTP
    /// request stalls (or times out agent-side at HttpTimeoutSeconds while the server keeps
    /// processing it) can finish *after* the tick that came behind it. Without a guard, that
    /// older snapshot overwrites the newer one and the overlay visibly jumps backwards - a team
    /// regains a player, an elimination un-happens. Both guards below exist to make the newest
    /// tick always win, regardless of arrival or completion order.
    /// </summary>
    public class IngestCoordinator
    {
        private readonly object _lock = new();

        // Ordering window. Scoped to one agent session: the agent generates a fresh session id at
        // the start of every match (and on restart), which is what lets the sequence counter go
        // back to 1 without the server mistaking that for a flood of stale ticks.
        private string? _sessionId;
        private long _acceptedSeq;
        private long _publishedSeq;
        private long _staleTicksDropped;
        private long _supersededTicksDropped;

        /// <summary>Per-match kill-feed dedupe state for the agent ingest path, mirroring the
        /// instance GetLiveData keeps for direct polling. Cleared whenever a match starts or ends
        /// so one match's eliminations can never leak into the next one's feed.</summary>
        public KillFeedTracker KillFeed { get; } = new();

        public Match? CurrentMatch { get; private set; }
        public List<LiveTeamPointStats>? TeamPoints { get; private set; }

        /// <summary>True once at least one "in game" tick has been ingested for the current
        /// match - lets the ingest endpoint detect the in-game -> not-in-game transition (the
        /// signal to run post-match processing) instead of treating "no ticks yet" the same as
        /// "match just ended".</summary>
        public bool WasInGame { get; set; }

        /// <summary>The agent session currently feeding this match, or null before the first
        /// sequenced tick arrives. Diagnostic only.</summary>
        public string? SessionId { get { lock (_lock) { return _sessionId; } } }

        /// <summary>Highest tick sequence admitted for processing. Diagnostic only.</summary>
        public long AcceptedSeq { get { lock (_lock) { return _acceptedSeq; } } }

        /// <summary>Highest tick sequence whose result actually reached the overlay. Diagnostic
        /// only - a gap between this and AcceptedSeq is normal and means out-of-order ticks are
        /// being correctly discarded rather than rendered.</summary>
        public long PublishedSeq { get { lock (_lock) { return _publishedSeq; } } }

        /// <summary>Ticks rejected before processing because a newer one had already been
        /// admitted. Surfaced on /api/ingest/status - a steadily climbing number means the link
        /// between the agent and this server is reordering or retrying a lot.</summary>
        public long StaleTicksDropped { get { lock (_lock) { return _staleTicksDropped; } } }

        /// <summary>Ticks that were processed but whose result was thrown away at publish time
        /// because a newer tick finished first. Diagnostic only.</summary>
        public long SupersededTicksDropped { get { lock (_lock) { return _supersededTicksDropped; } } }

        /// <summary>
        /// Decides whether a tick is new enough to process. Call this once, before doing any
        /// work, and drop the tick if it returns false.
        ///
        /// A tick from an unrecognised session resets the window rather than being rejected -
        /// otherwise an agent restart mid-match (new session, sequence back to 1) would be
        /// locked out until the match ended.
        /// </summary>
        /// <param name="currentSeq">The highest sequence already admitted, for the caller to
        /// report back to the agent.</param>
        public bool TryAdmitTick(string sessionId, long seq, out long currentSeq)
        {
            lock (_lock)
            {
                if (!string.Equals(_sessionId, sessionId, StringComparison.Ordinal))
                {
                    _sessionId = sessionId;
                    _acceptedSeq = seq;
                    _publishedSeq = 0;
                    currentSeq = seq;
                    return true;
                }

                if (seq <= _acceptedSeq)
                {
                    _staleTicksDropped++;
                    currentSeq = _acceptedSeq;
                    return false;
                }

                _acceptedSeq = seq;
                currentSeq = seq;
                return true;
            }
        }

        /// <summary>
        /// The second half of the guard, checked after processing and immediately before the
        /// result goes to the overlay. TryAdmitTick alone is not enough: two ticks can both be
        /// admitted in the right order and still *finish* in the wrong one if the earlier one's
        /// processing is slower. Returns false when a newer tick has already published, meaning
        /// this result is now history and must be discarded rather than rendered.
        /// </summary>
        public bool TryMarkPublished(string sessionId, long seq)
        {
            lock (_lock)
            {
                if (!string.Equals(_sessionId, sessionId, StringComparison.Ordinal) || seq <= _publishedSeq)
                {
                    _supersededTicksDropped++;
                    return false;
                }

                _publishedSeq = seq;
                return true;
            }
        }

        public void SetActiveMatch(Match match, List<LiveTeamPointStats> teamPoints)
        {
            lock (_lock)
            {
                CurrentMatch = match;
                TeamPoints = teamPoints;
                WasInGame = false;
                ResetSequenceWindow();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                CurrentMatch = null;
                TeamPoints = null;
                WasInGame = false;
                ResetSequenceWindow();
            }
        }

        // Caller holds _lock.
        private void ResetSequenceWindow()
        {
            _sessionId = null;
            _acceptedSeq = 0;
            _publishedSeq = 0;
            _staleTicksDropped = 0;
            _supersededTicksDropped = 0;
            KillFeed.Reset();
        }
    }
}
