using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VmixData.Models;
using VmixGraphicsBusiness;
using VmixGraphicsBusiness.LiveMatch;
using VmixGraphicsBusiness.PostMatchStats;
using VmixGraphicsBusiness.PreMatch;
using VmixGraphicsBusiness.Utils;

namespace Pubg_Ranking_System
{
    /// <summary>
    /// Which tournament/stage/day/match the client is pointing an action at - the web
    /// equivalent of the four WinForms combo boxes (TournamentName_cmb/Stage_cmb/Day_cmb/
    /// Match_cmb) that every Form1 button read from before doing anything.
    /// </summary>
    public record MatchSelector(string Tournament, string Stage, string Day, string Match);

    public record StartMatchRequest(string Tournament, string Stage, string Day, string Match, bool Confirm = false, string? TypedConfirmation = null);

    public record StartMatchResponse(bool Ok, int StatusCode, string Message, bool RequiresConfirmation, bool RequiresTypedDelete, int? MatchId);

    public record AddTournamentRequest(string Name);

    public record AddStageRequest(string TournamentName, string StageName);

    public record RunAllResult(bool Ok, string? Error);

    /// <summary>
    /// Every action that used to be a Form1 button click, as REST endpoints - this is the
    /// "web API instead of WinForms" half of the migration. Deliberately thin: every handler
    /// resolves the Match row from the selector (same lookup Form1's buttons repeated 8 times
    /// inline) and calls straight into the existing TournamentBusiness/PostMatch/PreMatch/Reset
    /// business classes, completely unchanged - so this file is the only thing that had to be
    /// written new, not the business logic underneath it.
    ///
    /// Each request gets its own DI scope (rootProvider.CreateScope()) so the scoped
    /// vmix_graphicsContext these business classes depend on is never shared across concurrent
    /// requests - Form1 could get away with one long-lived scope because only one person could
    /// click a button at a time; a web API can't assume that.
    ///
    /// Note on button7_Click's missing awaits (task list item 9): every multi-step endpoint here
    /// (run-all, run-legacy-all) awaits each step in sequence. There is no code path left that
    /// fires these off unawaited - the bug is fixed by construction, not patched.
    /// </summary>
    public static class MatchControlApi
    {
        public static void MapMatchControlEndpoints(this WebApplication app, IServiceProvider rootProvider, IBackgroundJobClient backgroundJobClient)
        {
            // ---------- Tournament / stage / match lookups (dropdown data) ----------

            app.MapGet("/api/tournaments", async () =>
            {
                using var scope = rootProvider.CreateScope();
                var tournamentBusiness = scope.ServiceProvider.GetRequiredService<TournamentBusiness>();
                var names = tournamentBusiness.getAll().Select(t => t.Name).OrderBy(n => n).ToList();
                return Results.Json(names);
            });

            // Mirrors the WinForms combo box exactly: every stage name across every tournament,
            // not filtered by the selected tournament. Kept as-is rather than "fixed" here since
            // changing it would be a behavior change beyond this migration's scope.
            app.MapGet("/api/stages", async () =>
            {
                using var scope = rootProvider.CreateScope();
                var tournamentBusiness = scope.ServiceProvider.GetRequiredService<TournamentBusiness>();
                var names = tournamentBusiness.getAllStages().Select(s => s.Name).OrderBy(n => n).ToList();
                return Results.Json(names);
            });

            app.MapPost("/api/tournaments", async (AddTournamentRequest request) =>
            {
                if (string.IsNullOrWhiteSpace(request?.Name))
                {
                    return Results.Json(new { ok = false, error = "Tournament name is required." }, statusCode: StatusCodes.Status400BadRequest);
                }

                using var scope = rootProvider.CreateScope();
                var tournamentBusiness = scope.ServiceProvider.GetRequiredService<TournamentBusiness>();
                var (message, statusCode) = await tournamentBusiness.add_tournament_btn_Click(new Tournament { Name = request.Name });
                return statusCode == 1
                    ? Results.Ok(new { ok = true, message })
                    : Results.Json(new { ok = false, error = message }, statusCode: StatusCodes.Status400BadRequest);
            });

            app.MapPost("/api/tournaments/stages", async (AddStageRequest request) =>
            {
                if (string.IsNullOrWhiteSpace(request?.TournamentName) || string.IsNullOrWhiteSpace(request?.StageName))
                {
                    return Results.Json(new { ok = false, error = "TournamentName and StageName are required." }, statusCode: StatusCodes.Status400BadRequest);
                }

                using var scope = rootProvider.CreateScope();
                var tournamentBusiness = scope.ServiceProvider.GetRequiredService<TournamentBusiness>();
                var (message, statusCode) = tournamentBusiness.Save_Click(new Stage { Name = request.StageName }, request.TournamentName);
                return statusCode == 1
                    ? Results.Ok(new { ok = true, message })
                    : Results.Json(new { ok = false, error = message }, statusCode: StatusCodes.Status400BadRequest);
            });

            // ---------- Match start / stop ----------

            // Replaces start_btn_Click's three-way MessageBox branching (new match / in-progress /
            // completed) with a single endpoint the client can call twice: once with Confirm=false
            // to find out whether confirmation is needed, and again with Confirm=true (plus
            // TypedConfirmation="DELETE" for a completed match) once the operator has agreed - the
            // same two-step "are you sure" -> "type DELETE" flow the WinForms dialog had, just
            // expressed as request/response instead of MessageBox.Show.
            app.MapPost("/api/match/start", async (StartMatchRequest request) =>
            {
                using var scope = rootProvider.CreateScope();
                var tournamentBusiness = scope.ServiceProvider.GetRequiredService<TournamentBusiness>();

                var (message, statusCode, match, isCompleted) = await tournamentBusiness.add_match(
                    request.Tournament, request.Stage, request.Day, request.Match);

                switch (statusCode)
                {
                    case 0:
                        await EnqueueStartAsync(scope, backgroundJobClient, match);
                        return Results.Ok(new StartMatchResponse(true, 0, message, false, false, match.MatchId));

                    case 1:
                        if (!request.Confirm)
                        {
                            return Results.Ok(new StartMatchResponse(true, 1, message, true, false, match.MatchId));
                        }
                        await EnqueueStartAsync(scope, backgroundJobClient, match);
                        return Results.Ok(new StartMatchResponse(true, 1, message, false, false, match.MatchId));

                    case 2:
                        if (!request.Confirm)
                        {
                            return Results.Ok(new StartMatchResponse(true, 2, message, true, false, match.MatchId));
                        }
                        if (!string.Equals(request.TypedConfirmation?.Trim(), "DELETE", StringComparison.Ordinal))
                        {
                            return Results.Ok(new StartMatchResponse(true, 2, message, true, true, match.MatchId));
                        }
                        await tournamentBusiness.DeleteMatchHistory(match);
                        await EnqueueStartAsync(scope, backgroundJobClient, match);
                        return Results.Ok(new StartMatchResponse(true, 2, "Completed match deleted and restarted.", false, false, match.MatchId));

                    default:
                        return Results.Json(new { ok = false, error = "Unexpected match state." }, statusCode: StatusCodes.Status500InternalServerError);
                }
            });

            // Replaces stop_Click. The WinForms version restarted the entire process (its only way
            // to guarantee a clean slate for a WinForms app with mutable form state); a headless
            // web API doesn't carry that same state, so clearing the match-related keys and
            // cancelling queued jobs is the whole story here - no process restart needed or wanted.
            app.MapPost("/api/match/stop", async () =>
            {
                using var scope = rootProvider.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Reset>>();
                CancelAllHighPriorityJobs(backgroundJobClient, logger);

                var matchState = scope.ServiceProvider.GetRequiredService<MatchStateStore>();
                var keyPatterns = new[]
                {
                    $"{HelperRedis.VehicleEliminationsKey}:*",
                    $"{HelperRedis.GrenadeEliminationsKey}:*",
                    $"{HelperRedis.AirDropLootedKey}:*",
                    HelperRedis.PlayerInfolist,
                    HelperRedis.TeamInfoList,
                    "isEliminated:rank",
                    $"{HelperRedis.isEliminated}:*",
                    HelperRedis.FirstBloodKey,
                };
                foreach (var pattern in keyPatterns)
                {
                    await matchState.DeleteByPatternAsync(pattern);
                }

                return Results.Ok(new { ok = true });
            });

            // ---------- Post-match report generation (one step, or the whole set) ----------

            app.MapPost("/api/postmatch/run/{step}", async (string step, MatchSelector selector) =>
            {
                using var scope = rootProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<vmix_graphicsContext>();
                var match = await ResolveMatchAsync(db, selector);
                if (match is null)
                {
                    return Results.Json(new { ok = false, error = "Match not found for that tournament/stage/day/match." }, statusCode: StatusCodes.Status404NotFound);
                }

                var postMatch = scope.ServiceProvider.GetRequiredService<PostMatch>();
                Task task = step switch
                {
                    "teams-to-watch" => postMatch.TeamsToWatch(match),
                    "match-rankings" => postMatch.MatchRankings(match),
                    "overall-rankings" => postMatch.OverallRankings(match),
                    "match-mvp" => postMatch.MatchMvp(match),
                    "wwcd" => postMatch.WWCDStatsAsync(match),
                    "match-summary" => postMatch.MatchSummary(match),
                    "day-summary" => postMatch.DaySummary(match),
                    "top5-match-mvp" => postMatch.Top5MatchMVP(match),
                    "top5-stage-mvp" => postMatch.Top5StageMVP(match),
                    "stage-mvp" => postMatch.StageMVP(match),
                    "top-grenadiers" => postMatch.TopGrenadiers(match),
                    _ => Task.CompletedTask,
                };

                if (task == Task.CompletedTask && step is not ("teams-to-watch" or "match-rankings" or "overall-rankings" or "match-mvp" or "wwcd" or "match-summary" or "day-summary" or "top5-match-mvp" or "top5-stage-mvp" or "stage-mvp" or "top-grenadiers"))
                {
                    return Results.Json(new { ok = false, error = $"Unknown step '{step}'." }, statusCode: StatusCodes.Status400BadRequest);
                }

                await task;
                return Results.Ok(new { ok = true, step });
            });

            // Equivalent of Form1's setall() (button6) - every step properly awaited in sequence.
            // button7_Click's version of this (fire every WWCDStatsAsync/MatchMvp/etc. call without
            // await) is intentionally NOT reproduced anywhere in this API.
            app.MapPost("/api/postmatch/run-all", async (MatchSelector selector) =>
            {
                using var scope = rootProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<vmix_graphicsContext>();
                var match = await ResolveMatchAsync(db, selector);
                if (match is null)
                {
                    return Results.Json(new { ok = false, error = "Match not found for that tournament/stage/day/match." }, statusCode: StatusCodes.Status404NotFound);
                }

                var postMatch = scope.ServiceProvider.GetRequiredService<PostMatch>();
                var errors = new List<string>();

                async Task RunStep(string name, Func<Task> step)
                {
                    try { await step(); }
                    catch (Exception ex) { errors.Add($"{name}: {ex.Message}"); }
                }

                await RunStep("WWCDStatsAsync", () => postMatch.WWCDStatsAsync(match));
                await RunStep("MatchMvp", () => postMatch.MatchMvp(match));
                await RunStep("MatchRankings", () => postMatch.MatchRankings(match));
                await RunStep("OverallRankings", () => postMatch.OverallRankings(match));
                await RunStep("DaySummary", () => postMatch.DaySummary(match));
                await RunStep("MatchSummary", () => postMatch.MatchSummary(match));
                await RunStep("Top5MatchMVP", () => postMatch.Top5MatchMVP(match));
                await RunStep("Top5StageMVP", () => postMatch.Top5StageMVP(match));
                await RunStep("StageMVP", () => postMatch.StageMVP(match));
                await RunStep("TopGrenadiers", () => postMatch.TopGrenadiers(match));
                await RunStep("TeamsToWatch", () => postMatch.TeamsToWatch(match));

                return errors.Count == 0
                    ? Results.Ok(new { ok = true })
                    : Results.Json(new { ok = false, partial = true, errors }, statusCode: StatusCodes.Status207MultiStatus);
            });

            app.MapPost("/api/prematch/map-top-performers", async (MatchSelector selector, string mapName) =>
            {
                using var scope = rootProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<vmix_graphicsContext>();
                var match = await ResolveMatchAsync(db, selector);
                if (match is null)
                {
                    return Results.Json(new { ok = false, error = "Match not found for that tournament/stage/day/match." }, statusCode: StatusCodes.Status404NotFound);
                }

                var preMatch = scope.ServiceProvider.GetRequiredService<PreMatch>();
                await preMatch.MapTopPerformers(match, mapName);
                return Results.Ok(new { ok = true });
            });

            // ---------- Teams: reload from the configured JSON file path ----------
            // (distinct from POST /api/teams/load in LiveDashboardHost.cs, which accepts a JSON
            // body directly from the browser - this one re-reads JsonTeamDataPath from
            // appsettings.json, same as reload_teams_btn_Click did.)
            app.MapPost("/api/teams/reload", async () =>
            {
                using var scope = rootProvider.CreateScope();
                var jsonTeamDataService = scope.ServiceProvider.GetRequiredService<JsonTeamDataService>();
                await jsonTeamDataService.LoadTeamDataAsync();
                return Results.Ok(new { ok = true });
            });
        }

        private static async Task EnqueueStartAsync(IServiceScope scope, IBackgroundJobClient backgroundJobClient, Match match)
        {
            backgroundJobClient.Enqueue<GetLiveData>(HangfireQueues.HighPriority, gld => gld.FetchAndPostData(match));
            await Task.CompletedTask;
        }

        private static async Task<Match?> ResolveMatchAsync(vmix_graphicsContext db, MatchSelector selector)
        {
            var tournament = await db.Tournaments.FirstOrDefaultAsync(x => x.Name == selector.Tournament);
            if (tournament is null) return null;

            var stage = await db.Stages.FirstOrDefaultAsync(x => x.Name == selector.Stage && x.TournamentId == tournament.TournamentId);
            if (stage is null) return null;

            if (!int.TryParse(selector.Day, out var day) || !int.TryParse(selector.Match, out var matchNumber))
            {
                return null;
            }

            return await db.Matches.FirstOrDefaultAsync(x =>
                x.TournamentId == tournament.TournamentId &&
                x.StageId == stage.StageId &&
                x.MatchDayId == day &&
                x.MatchId == matchNumber);
        }

        /// <summary>Ported from Form1.CancelAllHighPriorityJobs verbatim (minus the Form1 instance
        /// dependency) - deletes every enqueued/processing/scheduled job across all three queues,
        /// retried 5x same as the original since Hangfire's monitoring API can race with jobs
        /// being claimed by a worker mid-enumeration.</summary>
        private static void CancelAllHighPriorityJobs(IBackgroundJobClient backgroundJobManager, ILogger logger)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    var monitoringApi = JobStorage.Current.GetMonitoringApi();
                    int deletedCount = 0;

                    var enqueuedJobs = monitoringApi.EnqueuedJobs(HangfireQueues.HighPriority, 0, 1000);
                    enqueuedJobs.AddRange(monitoringApi.EnqueuedJobs(HangfireQueues.LowPriority, 0, 1000));
                    enqueuedJobs.AddRange(monitoringApi.EnqueuedJobs(HangfireQueues.Default, 0, 1000));
                    foreach (var job in enqueuedJobs)
                    {
                        backgroundJobManager.Delete(job.Key);
                        deletedCount++;
                    }

                    var processingJobs = monitoringApi.ProcessingJobs(0, 1000);
                    foreach (var job in processingJobs)
                    {
                        backgroundJobManager.Delete(job.Key);
                        deletedCount++;
                    }

                    var scheduledJobs = monitoringApi.ScheduledJobs(0, 1000);
                    foreach (var job in scheduledJobs)
                    {
                        backgroundJobManager.Delete(job.Key);
                        deletedCount++;
                    }

                    logger.LogInformation("Deleted {Count} jobs from high-priority queue", deletedCount);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error deleting high-priority jobs");
                }
            }
        }
    }
}
