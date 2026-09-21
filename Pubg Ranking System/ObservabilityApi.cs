using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Pubg_Ranking_System.Tenancy;
using VmixGraphicsBusiness.Observability;
using VmixGraphicsBusiness.Tenancy;

namespace Pubg_Ranking_System
{
    public record AgentHeartbeatRequest(string? AgentVersion = null, string? SessionId = null, string? Note = null);

    /// <summary>
    /// The endpoints that answer "is this thing working right now?" — logs, agent liveness, and
    /// health.
    ///
    /// The gap analysis's first observability finding was that an entire embedded Kestrel host
    /// failed to start and the exception went into a console window nobody was watching. Its
    /// second was that an agent going silent is invisible: the overlay simply holds its last frame,
    /// so a dead pipeline and a quiet match look identical from the operator's chair. Both are
    /// addressed here, and both are addressed in the dashboard rather than in a log file, because
    /// during a broadcast the operator is looking at vMix and a browser, not at a terminal.
    /// </summary>
    public static class ObservabilityApi
    {
        /// <param name="checkDatabase">
        /// Optional readiness probe for the database. Passed in as a delegate rather than resolved
        /// here so this file has no dependency on the EF context type, and so an install that has
        /// fallen back to SQLite can report that honestly instead of this code assuming MySQL.
        /// </param>
        public static void MapObservabilityEndpoints(
            this WebApplication app,
            Func<CancellationToken, Task<(bool ok, string detail)>>? checkDatabase = null)
        {
            // ------------------------------------------------------------------ logs

            var admin = app.MapGroup("/api/observability").RequireTenantAdmin();

            admin.MapGet("/logs", (HttpContext http, TenantScopeManager scopes,
                                   string? tournamentId, string? minLevel, long? afterSeq, int? max) =>
            {
                var tenant = http.GetTenant();

                // An operator key sees only its own tournament's log, whatever it asks for. The
                // install-wide key sees the merged view when it does not name one.
                var requested = tenant.CanAdministerAllTournaments
                    ? tournamentId
                    : tenant.TournamentId;

                var records = scopes.SnapshotLogs(
                    requested,
                    Math.Clamp(max ?? 200, 1, 1000),
                    minLevel,
                    afterSeq ?? 0);

                return Results.Json(new
                {
                    // The cursor to pass back as afterSeq, so the dashboard tails the log instead
                    // of re-fetching the whole ring every poll.
                    nextSeq = records.Count > 0 ? records[^1].Seq : (afterSeq ?? 0),
                    scope = requested ?? "all",
                    records = records.Select(r => new
                    {
                        seq = r.Seq,
                        ts = r.TimestampUtc,
                        level = r.Level,
                        category = r.Category,
                        message = r.Message,
                        tournamentId = r.TournamentId,
                        tournament = r.TournamentName,
                        error = r.Error,
                        props = r.Properties
                    }).ToList()
                });
            });

            // ------------------------------------------------------------------ agents

            admin.MapGet("/agents", (HttpContext http, TenantScopeManager scopes, TournamentRegistry registry) =>
            {
                var tenant = http.GetTenant();
                var visible = tenant.CanAdministerAllTournaments
                    ? scopes.ActiveScopes()
                    : scopes.ActiveScopes().Where(s => s.TournamentId == tenant.TournamentId).ToList();

                return Results.Json(visible.Select(scope =>
                {
                    var snapshot = scope.Agent.Snapshot();
                    return new
                    {
                        tournamentId = scope.TournamentId,
                        tournament = scope.TournamentName,
                        connected = snapshot.Connected,
                        summary = snapshot.Summary,
                        agentVersion = snapshot.AgentVersion,
                        sessionId = snapshot.SessionId,
                        lastContactUtc = snapshot.LastContactUtc,
                        secondsSinceLastContact = snapshot.SecondsSinceLastContact,
                        secondsSinceLastTick = snapshot.SecondsSinceLastTick,
                        ticksAccepted = snapshot.TicksAccepted,
                        ticksDropped = snapshot.TicksDropped,
                        averageTickIntervalSeconds = snapshot.AverageTickIntervalSeconds,
                        lastObservedLatencySeconds = snapshot.LastObservedLatencySeconds,
                        versionSupported = snapshot.VersionSupported,
                        minimumVersion = snapshot.MinimumVersion,

                        // Straight from the existing IngestCoordinator, so the ordering diagnostics
                        // that were previously only on /api/ingest/status are now per tournament.
                        ingest = new
                        {
                            matchActive = scope.Ingest.CurrentMatch is not null,
                            matchId = scope.Ingest.CurrentMatch?.MatchId,
                            wasInGame = scope.Ingest.WasInGame,
                            sessionId = scope.Ingest.SessionId,
                            acceptedSeq = scope.Ingest.AcceptedSeq,
                            publishedSeq = scope.Ingest.PublishedSeq,
                            staleTicksDropped = scope.Ingest.StaleTicksDropped,
                            supersededTicksDropped = scope.Ingest.SupersededTicksDropped
                        }
                    };
                }).ToList());
            });

            // Called by the ingest agent on a slow timer, independently of ticks. This is what
            // distinguishes "the agent is alive but the match hasn't started" from "the agent is
            // gone" — without it, both look like silence.
            app.MapPost("/api/ingest/heartbeat", (HttpContext http, AgentHeartbeatRequest? request, TenantScopeManager scopes) =>
            {
                var tenant = http.GetTenant();
                var scope = tenant.Scope!;
                scope.Agent.RecordHeartbeat(request?.AgentVersion, request?.SessionId);

                var snapshot = scope.Agent.Snapshot();
                if (!snapshot.VersionSupported)
                {
                    // Worth a warning rather than a rejection: refusing an old agent mid-event
                    // turns a cosmetic problem into a dark broadcast.
                    scopes.Publish("Warning", "ingest", "agent is below the configured minimum version",
                        scope.TournamentId,
                        properties: new Dictionary<string, string>
                        {
                            ["agentVersion"] = snapshot.AgentVersion ?? "-",
                            ["minimumVersion"] = snapshot.MinimumVersion ?? "-"
                        });
                }

                return Results.Json(new
                {
                    ok = true,
                    tournamentId = scope.TournamentId,
                    tournament = scope.TournamentName,
                    versionSupported = snapshot.VersionSupported,
                    minimumVersion = snapshot.MinimumVersion,
                    // Echoed so the agent can log its own view of the round trip.
                    serverTimeUtc = DateTime.UtcNow
                });
            }).RequireAgentKey();

            // ------------------------------------------------------------------ health

            // Liveness. Unauthenticated on purpose: this is what an uptime monitor or an Azure
            // health probe calls, and neither can hold a key. It deliberately reveals nothing
            // beyond "the process is answering".
            app.MapGet("/health", () => Results.Json(new
            {
                status = "ok",
                utc = DateTime.UtcNow,
                uptimeSeconds = (DateTime.UtcNow - ProcessStartUtc).TotalSeconds
            }));

            // Readiness. Checks the things whose absence makes this process useless rather than
            // merely unhealthy: can it write its state, and can it reach its database.
            app.MapGet("/health/ready", async (TenantScopeManager scopes, TournamentRegistry registry, CancellationToken ct) =>
            {
                var checks = new List<object>();
                var healthy = true;

                var stateWritable = TryWriteProbe(scopes.Default.StateDirectory, out var stateDetail);
                if (!stateWritable) healthy = false;
                checks.Add(new { name = "state-directory", ok = stateWritable, detail = stateDetail });

                checks.Add(new { name = "tenant-registry", ok = true, detail = registry.Count + " tournament(s) loaded" });

                if (checkDatabase is not null)
                {
                    try
                    {
                        var (dbOk, dbDetail) = await checkDatabase(ct);
                        if (!dbOk) healthy = false;
                        checks.Add(new { name = "database", ok = dbOk, detail = dbDetail });
                    }
                    catch (Exception ex)
                    {
                        healthy = false;
                        checks.Add(new { name = "database", ok = false, detail = ex.Message });
                    }
                }

                // Agent liveness is reported but never fails readiness: between matches there is
                // legitimately no agent connected, and a probe that goes red overnight is a probe
                // people learn to ignore.
                foreach (var scope in scopes.ActiveScopes())
                {
                    var snapshot = scope.Agent.Snapshot();
                    checks.Add(new
                    {
                        name = "agent:" + scope.TournamentId,
                        ok = true,
                        detail = snapshot.Summary
                    });
                }

                return Results.Json(
                    new { status = healthy ? "ready" : "degraded", utc = DateTime.UtcNow, checks },
                    statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
            });
        }

        private static readonly DateTime ProcessStartUtc = DateTime.UtcNow;

        private static bool TryWriteProbe(string directory, out string detail)
        {
            try
            {
                Directory.CreateDirectory(directory);
                var probe = Path.Combine(directory, ".health-probe");
                File.WriteAllText(probe, DateTime.UtcNow.ToString("o"));
                File.Delete(probe);
                detail = "writable: " + directory;
                return true;
            }
            catch (Exception ex)
            {
                detail = ex.Message;
                return false;
            }
        }
    }
}
