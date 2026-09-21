#nullable enable
using System;

namespace VmixGraphicsBusiness.Observability
{
    /// <summary>Point-in-time view of one tournament's ingest agent, shaped for the dashboard.</summary>
    public sealed class AgentStatusSnapshot
    {
        public bool Connected { get; set; }
        public string? AgentVersion { get; set; }
        public string? SessionId { get; set; }
        public DateTime? LastContactUtc { get; set; }
        public double? SecondsSinceLastContact { get; set; }
        public DateTime? LastAcceptedTickUtc { get; set; }
        public double? SecondsSinceLastTick { get; set; }
        public long TicksAccepted { get; set; }
        public long TicksDropped { get; set; }
        public double? AverageTickIntervalSeconds { get; set; }
        public double? LastObservedLatencySeconds { get; set; }
        public bool VersionSupported { get; set; } = true;
        public string? MinimumVersion { get; set; }

        /// <summary>Short human line for the dashboard pill, e.g. "agent connected, last tick 0.8s ago".</summary>
        public string Summary
        {
            get
            {
                if (!Connected)
                    return LastContactUtc == null
                        ? "no agent has ever connected"
                        : "agent offline, last seen " + FormatAgo(SecondsSinceLastContact) + " ago";

                var line = "agent connected";
                if (SecondsSinceLastTick.HasValue)
                    line += ", last tick " + FormatAgo(SecondsSinceLastTick) + " ago";
                if (!VersionSupported)
                    line += " (agent " + (AgentVersion ?? "?") + " is below the minimum " + MinimumVersion + ")";
                return line;
            }
        }

        private static string FormatAgo(double? seconds)
        {
            if (!seconds.HasValue) return "?";
            var s = seconds.Value;
            if (s < 10) return s.ToString("0.0") + "s";
            if (s < 90) return s.ToString("0") + "s";
            if (s < 5400) return (s / 60).ToString("0") + "m";
            return (s / 3600).ToString("0.0") + "h";
        }
    }

    /// <summary>
    /// Tracks liveness of one tournament's ingest agent.
    ///
    /// The point is to make silence visible. Today, if an agent stops sending, the overlay simply
    /// holds its last frame and nobody knows whether the match is quiet or the pipeline is dead —
    /// and that ambiguity is worst precisely when it matters. An explicit "last tick 0.8s ago"
    /// readout in the dashboard converts an invisible failure into an obvious one.
    ///
    /// One instance per tournament; safe to call concurrently.
    /// </summary>
    public sealed class AgentHealth
    {
        private readonly object _gate = new object();
        private readonly TimeSpan _stale;

        private DateTime? _lastContactUtc;
        private DateTime? _lastTickUtc;
        private string? _agentVersion;
        private string? _sessionId;
        private long _accepted;
        private long _dropped;
        private double _intervalEwma;
        private double? _lastLatencySeconds;

        /// <summary>
        /// Optional floor on agent version. Without a minimum, a backend change silently breaks
        /// field installs that nobody can reach; with one, the dashboard can say so.
        /// </summary>
        public string? MinimumAgentVersion { get; set; }

        /// <param name="staleAfter">
        /// How long without contact counts as offline. PUBG's own cadence is ~2s, so anything past
        /// a handful of seconds is already a visible problem on air.
        /// </param>
        public AgentHealth(TimeSpan? staleAfter = null)
        {
            _stale = staleAfter ?? TimeSpan.FromSeconds(10);
        }

        public void RecordHeartbeat(string? agentVersion = null, string? sessionId = null)
        {
            lock (_gate)
            {
                _lastContactUtc = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(agentVersion)) _agentVersion = agentVersion;
                if (!string.IsNullOrEmpty(sessionId)) _sessionId = sessionId;
            }
        }

        public void RecordAcceptedTick(string? agentVersion = null, string? sessionId = null, TimeSpan? observedLatency = null)
        {
            lock (_gate)
            {
                var now = DateTime.UtcNow;
                if (_lastTickUtc.HasValue)
                {
                    var gap = (now - _lastTickUtc.Value).TotalSeconds;
                    // Exponentially weighted mean: one slow tick should not dominate the readout,
                    // and a sustained slowdown should still show up within a few seconds.
                    _intervalEwma = _intervalEwma <= 0 ? gap : (_intervalEwma * 0.8) + (gap * 0.2);
                }
                _lastTickUtc = now;
                _lastContactUtc = now;
                _accepted++;
                if (!string.IsNullOrEmpty(agentVersion)) _agentVersion = agentVersion;
                if (!string.IsNullOrEmpty(sessionId)) _sessionId = sessionId;
                if (observedLatency.HasValue) _lastLatencySeconds = observedLatency.Value.TotalSeconds;
            }
        }

        public void RecordDroppedTick()
        {
            lock (_gate)
            {
                _dropped++;
                _lastContactUtc = DateTime.UtcNow;
            }
        }

        public void Reset()
        {
            lock (_gate)
            {
                _lastTickUtc = null;
                _accepted = 0;
                _dropped = 0;
                _intervalEwma = 0;
                _lastLatencySeconds = null;
            }
        }

        public AgentStatusSnapshot Snapshot()
        {
            lock (_gate)
            {
                var now = DateTime.UtcNow;
                double? sinceContact = _lastContactUtc.HasValue ? (now - _lastContactUtc.Value).TotalSeconds : (double?)null;
                double? sinceTick = _lastTickUtc.HasValue ? (now - _lastTickUtc.Value).TotalSeconds : (double?)null;

                return new AgentStatusSnapshot
                {
                    Connected = sinceContact.HasValue && sinceContact.Value <= _stale.TotalSeconds,
                    AgentVersion = _agentVersion,
                    SessionId = _sessionId,
                    LastContactUtc = _lastContactUtc,
                    SecondsSinceLastContact = sinceContact,
                    LastAcceptedTickUtc = _lastTickUtc,
                    SecondsSinceLastTick = sinceTick,
                    TicksAccepted = _accepted,
                    TicksDropped = _dropped,
                    AverageTickIntervalSeconds = _intervalEwma > 0 ? _intervalEwma : (double?)null,
                    LastObservedLatencySeconds = _lastLatencySeconds,
                    MinimumVersion = MinimumAgentVersion,
                    VersionSupported = IsVersionSupportedLocked()
                };
            }
        }

        private bool IsVersionSupportedLocked()
        {
            if (string.IsNullOrWhiteSpace(MinimumAgentVersion)) return true;
            if (string.IsNullOrWhiteSpace(_agentVersion)) return true; // unknown, not unsupported
            return CompareVersions(_agentVersion!, MinimumAgentVersion!) >= 0;
        }

        /// <summary>
        /// Dotted-numeric comparison that tolerates suffixes ("1.4.0-beta2") by comparing only the
        /// leading numeric parts. Agent versions are ours to set, so this does not need to be a
        /// full SemVer implementation — it needs to not throw on one.
        /// </summary>
        internal static int CompareVersions(string left, string right)
        {
            var a = SplitNumeric(left);
            var b = SplitNumeric(right);
            var len = Math.Max(a.Length, b.Length);
            for (int i = 0; i < len; i++)
            {
                var x = i < a.Length ? a[i] : 0;
                var y = i < b.Length ? b[i] : 0;
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }

        private static int[] SplitNumeric(string version)
        {
            var raw = version.Split('.', '-', '+', ' ');
            var parts = new System.Collections.Generic.List<int>();
            foreach (var token in raw)
            {
                var digits = 0;
                var any = false;
                foreach (var ch in token)
                {
                    if (ch < '0' || ch > '9') break;
                    any = true;
                    digits = (digits * 10) + (ch - '0');
                }
                if (!any) break;
                parts.Add(digits);
            }
            return parts.ToArray();
        }
    }
}
