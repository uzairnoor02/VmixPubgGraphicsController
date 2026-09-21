#nullable enable
using System;
using System.Collections.Generic;

namespace VmixGraphicsBusiness.Observability
{
    /// <summary>
    /// One structured log event. Deliberately a flat shape with a string-keyed property bag rather
    /// than a formatted line, so the same record can be rendered in the dashboard, written as
    /// newline-delimited JSON, and later shipped to Application Insights without being re-parsed.
    /// </summary>
    public sealed class LogRecord
    {
        /// <summary>Monotonic within the process. The dashboard polls with <c>afterSeq</c> so it never re-fetches.</summary>
        public long Seq { get; set; }

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Trace / Debug / Information / Warning / Error / Critical.</summary>
        public string Level { get; set; } = "Information";

        /// <summary>Logical source, e.g. <c>ingest</c>, <c>tenancy</c>, <c>overlay</c>, <c>postmatch</c>.</summary>
        public string Category { get; set; } = "app";

        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The tenant this event belongs to, or null for process-level events. Every log line in a
        /// multi-tenant system needs this or a support question ("why did tournament X go dark?")
        /// cannot be answered from the logs at all.
        /// </summary>
        public string? TournamentId { get; set; }

        public string? TournamentName { get; set; }

        /// <summary>Exception text, if any. Kept separate from <see cref="Message"/> so the message stays greppable.</summary>
        public string? Error { get; set; }

        public Dictionary<string, string>? Properties { get; set; }

        public static int LevelRank(string? level)
        {
            switch ((level ?? string.Empty).ToLowerInvariant())
            {
                case "trace": case "verbose":     return 0;
                case "debug":                     return 1;
                case "information": case "info":  return 2;
                case "warning": case "warn":      return 3;
                case "error":                     return 4;
                case "critical": case "fatal":    return 5;
                default:                          return 2;
            }
        }
    }

    /// <summary>
    /// A bounded in-memory ring of recent log events, per tournament plus one process-wide ring.
    ///
    /// This exists because the gap analysis's sharpest finding was that a whole Kestrel host failed
    /// to start and the exception went into a console nobody was watching. During a live broadcast
    /// the operator is in vMix, not in a terminal — so the logs have to be somewhere they will
    /// actually be looked at, which means the dashboard. The ring is the cheap half of that: the
    /// durable half is the NDJSON file sink in the host project.
    ///
    /// All members are safe to call concurrently.
    /// </summary>
    public sealed class LogBuffer
    {
        private readonly object _gate = new object();
        private readonly int _capacity;
        private readonly Queue<LogRecord> _records = new Queue<LogRecord>();
        private long _seq;

        public LogBuffer(int capacity = 750)
        {
            _capacity = Math.Max(50, capacity);
        }

        /// <summary>
        /// Raised for every appended record. Subscribers must be fast and must not throw — this
        /// fires on whatever thread produced the log line, including the live tick path.
        /// </summary>
        public event Action<LogRecord>? Appended;

        public long TotalAppended { get { lock (_gate) return _seq; } }

        public LogRecord Append(LogRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            lock (_gate)
            {
                record.Seq = ++_seq;
                if (record.TimestampUtc == default) record.TimestampUtc = DateTime.UtcNow;
                _records.Enqueue(record);
                while (_records.Count > _capacity) _records.Dequeue();
            }

            var handler = Appended;
            if (handler != null)
            {
                try { handler(record); }
                catch
                {
                    // A broken log subscriber must never be able to break the thing being logged.
                }
            }
            return record;
        }

        public LogRecord Append(string level, string category, string message,
                                string? tournamentId = null, string? error = null,
                                Dictionary<string, string>? properties = null)
            => Append(new LogRecord
            {
                Level = level,
                Category = category,
                Message = message,
                TournamentId = tournamentId,
                Error = error,
                Properties = properties
            });

        /// <summary>
        /// Most recent records first is wrong for a log view, so this returns oldest-to-newest
        /// within the requested slice.
        /// </summary>
        public IReadOnlyList<LogRecord> Snapshot(int max = 200, string? minLevel = null, long afterSeq = 0)
        {
            var floor = LogRecord.LevelRank(minLevel ?? "trace");
            var take = Math.Max(1, Math.Min(max, _capacity));

            lock (_gate)
            {
                var matching = new List<LogRecord>();
                foreach (var r in _records)
                {
                    if (r.Seq <= afterSeq) continue;
                    if (LogRecord.LevelRank(r.Level) < floor) continue;
                    matching.Add(r);
                }

                if (matching.Count <= take) return matching;
                return matching.GetRange(matching.Count - take, take);
            }
        }

        public void Clear()
        {
            lock (_gate) _records.Clear();
        }
    }
}
