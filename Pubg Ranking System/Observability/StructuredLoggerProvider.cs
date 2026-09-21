using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pubg_Ranking_System.Tenancy;
using VmixGraphicsBusiness.Observability;

namespace Pubg_Ranking_System.Observability
{
    /// <summary>
    /// Marks a block of work as belonging to one tournament, so every log line written inside it
    /// is tagged without each call site having to pass an id.
    ///
    /// Usage: <c>using (logger.BeginTournamentScope(tournamentId)) { ... }</c>
    /// </summary>
    public sealed class TournamentLogScope
    {
        public TournamentLogScope(string tournamentId, string? correlationId = null)
        {
            TournamentId = tournamentId;
            CorrelationId = correlationId;
        }

        public string TournamentId { get; }
        public string? CorrelationId { get; }

        public override string ToString()
            => "tournament=" + TournamentId + (CorrelationId is null ? "" : " correlation=" + CorrelationId);
    }

    /// <summary>
    /// Structured logging for this application, replacing the plain-text appender in
    /// <c>FileLogger.cs</c>.
    ///
    /// Three problems with what it replaces, each of which cost real debugging time:
    ///  * it ignored the category name entirely, so every line looked the same and nothing could
    ///    be filtered;
    ///  * it rounded timestamps to the nearest half hour to pick a filename, so the file a line
    ///    landed in did not match when it happened;
    ///  * it was write-only — nothing ever read those files during an event, which is why a
    ///    Kestrel host failing to start went unnoticed.
    ///
    /// What this does instead: one JSON object per line (grep-able, and loadable straight into any
    /// log tool later without a parser), daily files with retention, plus a live in-memory ring
    /// pushed to the dashboard's Logs tab. During a broadcast the operator is in vMix, not in a
    /// terminal, so the only logs that count are the ones visible in the dashboard.
    ///
    /// Deliberately built on <c>Microsoft.Extensions.Logging</c> with no new package: Serilog would
    /// be the conventional answer, but adding a NuGet dependency to a solution that currently
    /// cannot be restored from this session is a risk with no matching benefit.
    /// </summary>
    public sealed class StructuredLoggerProvider : ILoggerProvider
    {
        private readonly string _directory;
        private readonly int _retentionDays;
        private readonly LogLevel _minimum;
        private readonly ConcurrentDictionary<string, StructuredLogger> _loggers = new(StringComparer.Ordinal);
        private readonly object _fileLock = new();

        private DateTime _lastPruneUtc = DateTime.MinValue;

        public StructuredLoggerProvider(string directory, int retentionDays = 14, LogLevel minimum = LogLevel.Information)
        {
            _directory = string.IsNullOrWhiteSpace(directory) ? "resources/logs" : directory;
            _retentionDays = Math.Max(1, retentionDays);
            _minimum = minimum;
            try { Directory.CreateDirectory(_directory); } catch { /* best effort */ }
        }

        /// <summary>
        /// Where tenant-tagged records are fanned out to. Assigned after the host is built, because
        /// logging is configured before the tenant manager exists — until then records still reach
        /// the NDJSON file, they just aren't visible in the dashboard yet.
        /// </summary>
        public TenantScopeManager? Scopes { get; set; }

        public ILogger CreateLogger(string categoryName)
            => _loggers.GetOrAdd(categoryName ?? "app", name => new StructuredLogger(this, name));

        public void Dispose() => _loggers.Clear();

        internal bool IsEnabled(LogLevel level) => level >= _minimum && level != LogLevel.None;

        internal void Write(LogRecord record)
        {
            // The dashboard ring first: it is in-memory and cannot fail, so a disk problem never
            // costs the operator their live view of what is happening.
            Scopes?.Publish(record);
            AppendToFile(record);
        }

        private void AppendToFile(LogRecord record)
        {
            try
            {
                var payload = new
                {
                    ts = record.TimestampUtc.ToString("o"),
                    level = record.Level,
                    category = record.Category,
                    msg = record.Message,
                    tournamentId = record.TournamentId,
                    tournament = record.TournamentName,
                    error = record.Error,
                    props = record.Properties
                };

                var line = JsonSerializer.Serialize(payload) + Environment.NewLine;
                var path = Path.Combine(_directory, "vmix-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".ndjson");

                lock (_fileLock)
                {
                    File.AppendAllText(path, line, Encoding.UTF8);
                    PruneOldFilesLocked();
                }
            }
            catch
            {
                // Logging must never throw into the code being logged. A failure here is visible
                // as a gap in the files while the in-memory ring keeps working.
            }
        }

        // Caller holds _fileLock.
        private void PruneOldFilesLocked()
        {
            if ((DateTime.UtcNow - _lastPruneUtc) < TimeSpan.FromHours(6)) return;
            _lastPruneUtc = DateTime.UtcNow;

            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
                foreach (var file in Directory.GetFiles(_directory, "vmix-*.ndjson"))
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
                }
            }
            catch { /* retention is housekeeping, never load-bearing */ }
        }

        private sealed class StructuredLogger : ILogger
        {
            private readonly StructuredLoggerProvider _provider;
            private readonly string _category;
            private readonly AsyncLocal<Stack<object?>> _scopes = new();

            public StructuredLogger(StructuredLoggerProvider provider, string category)
            {
                _provider = provider;
                _category = category;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull
            {
                var stack = _scopes.Value ??= new Stack<object?>();
                stack.Push(state);
                return new PopOnDispose(stack);
            }

            public bool IsEnabled(LogLevel logLevel) => _provider.IsEnabled(logLevel);

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel) || formatter is null) return;

                string? tournamentId = null;
                string? correlationId = null;
                Dictionary<string, string>? properties = null;

                var stack = _scopes.Value;
                if (stack is not null)
                {
                    foreach (var scope in stack)
                    {
                        switch (scope)
                        {
                            case TournamentLogScope tournamentScope:
                                tournamentId ??= tournamentScope.TournamentId;
                                correlationId ??= tournamentScope.CorrelationId;
                                break;

                            // Structured scopes created with logger.BeginScope(new { ... }) or a
                            // dictionary arrive as key/value pairs; lift any recognised keys.
                            case IEnumerable<KeyValuePair<string, object?>> pairs:
                                foreach (var pair in pairs)
                                {
                                    if (string.Equals(pair.Key, "TournamentId", StringComparison.OrdinalIgnoreCase))
                                        tournamentId ??= pair.Value?.ToString();
                                    else if (string.Equals(pair.Key, "CorrelationId", StringComparison.OrdinalIgnoreCase))
                                        correlationId ??= pair.Value?.ToString();
                                    else
                                    {
                                        properties ??= new Dictionary<string, string>();
                                        properties[pair.Key] = pair.Value?.ToString() ?? "";
                                    }
                                }
                                break;
                        }
                    }
                }

                if (eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name))
                {
                    properties ??= new Dictionary<string, string>();
                    properties["eventId"] = eventId.Name ?? eventId.Id.ToString();
                }

                if (!string.IsNullOrEmpty(correlationId))
                {
                    properties ??= new Dictionary<string, string>();
                    properties["correlationId"] = correlationId!;
                }

                _provider.Write(new LogRecord
                {
                    Level = logLevel.ToString(),
                    Category = _category,
                    Message = formatter(state, exception),
                    TournamentId = tournamentId,
                    Error = exception is null
                        ? null
                        : exception.GetType().FullName + ": " + exception.Message + Environment.NewLine + exception.StackTrace,
                    Properties = properties
                });
            }

            private sealed class PopOnDispose : IDisposable
            {
                private readonly Stack<object?> _stack;
                private bool _done;

                public PopOnDispose(Stack<object?> stack) => _stack = stack;

                public void Dispose()
                {
                    if (_done) return;
                    _done = true;
                    if (_stack.Count > 0) _stack.Pop();
                }
            }
        }
    }

    public static class StructuredLoggerExtensions
    {
        /// <summary>Tags every log line inside the block with a tournament.</summary>
        public static IDisposable BeginTournamentScope(this ILogger logger, string tournamentId, string? correlationId = null)
            => logger.BeginScope(new TournamentLogScope(tournamentId, correlationId));
    }
}
