using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness;
using VmixGraphicsBusiness.LiveMatch;
using VmixGraphicsBusiness.PostMatchStats;
using VmixGraphicsBusiness.Utils;

namespace Pubg_Ranking_System
{
    /// <summary>
    /// One pcob poll tick, as pushed by VmixIngestAgent (the small program that runs on the
    /// customer's PC next to pcob) instead of this application polling pcob itself.
    /// PlayerListJson/TeamInfoJson are the exact raw response bodies pcob's own
    /// gettotalplayerlist / getteaminfolist endpoints return - deserialized here with the same
    /// models GetLiveData.FetchAndPostData already uses, so there is exactly one place that
    /// understands the pcob response shape, not two.
    /// </summary>
    public record IngestTickRequest(bool IsInGame, string? PlayerListJson, string? TeamInfoJson);

    /// <summary>
    /// Receives live match data from VmixIngestAgent when this application isn't running on the
    /// same machine/LAN segment as pcob (the normal production topology once the graphics
    /// pipeline is hosted centrally instead of on each event's local PC) - the other half of the
    /// "collector on the customer PC, processing centrally" split the user asked for.
    ///
    /// This is a second way to feed the exact same pipeline GetLiveData.FetchAndPostData already
    /// drives (LiveStatsBusiness.CreateDynamicLiveStats -> MatchStateStore.PublishLiveTeams ->
    /// SignalR -> the React dashboard/overlay), not a parallel one - see StartMatchRequest.Mode in
    /// MatchControlApi.cs for how an operator picks which source feeds a given match.
    /// </summary>
    public static class IngestApi
    {
        public static void MapIngestEndpoints(this WebApplication app, IServiceProvider rootProvider, IConfiguration configuration, IngestCoordinator ingestCoordinator)
        {
            app.MapPost("/api/ingest/tick", async (HttpRequest httpRequest, IngestTickRequest tick) =>
            {
                // Shared-secret auth, same pattern as WebDashboard:AuthKey - this endpoint is
                // reachable from outside the LAN once this app is centrally hosted, so an
                // unauthenticated version of it would let anyone feed fake match data in.
                var expectedKey = configuration["Agent:IngestKey"];
                if (string.IsNullOrWhiteSpace(expectedKey))
                {
                    return Results.Json(new { ok = false, error = "Agent:IngestKey is not configured on the server." }, statusCode: StatusCodes.Status500InternalServerError);
                }
                if (!httpRequest.Headers.TryGetValue("X-Agent-Key", out var providedKey) || providedKey != expectedKey)
                {
                    return Results.Json(new { ok = false, error = "Invalid or missing X-Agent-Key header." }, statusCode: StatusCodes.Status401Unauthorized);
                }

                var match = ingestCoordinator.CurrentMatch;
                if (match is null)
                {
                    return Results.Json(new { ok = false, error = "No active match - call POST /api/match/start with mode=\"agent\" first." }, statusCode: StatusCodes.Status409Conflict);
                }

                if (!tick.IsInGame)
                {
                    // In-game -> not-in-game transition is the same "match just ended" signal
                    // GetLiveData.FetchAndPostData's own while(IsInGame) loop exiting represents -
                    // run the same post-match step it runs afterward, exactly once.
                    if (ingestCoordinator.WasInGame)
                    {
                        using var endScope = rootProvider.CreateScope();
                        var postMatchAtEnd = endScope.ServiceProvider.GetRequiredService<PostMatch>();
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(tick.PlayerListJson) && !string.IsNullOrWhiteSpace(tick.TeamInfoJson))
                            {
                                var finalPlayers = JsonSerializer.Deserialize<LivePlayersList>(tick.PlayerListJson)!;
                                var finalTeams = JsonSerializer.Deserialize<TeamInfoList>(tick.TeamInfoJson)!;
                                await postMatchAtEnd.createPostMtachStats(finalPlayers, match, finalTeams);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Post-match processing must never take the ingest endpoint down with
                            // it - same "log and move on" contract PostMatch.cs's own
                            // RunPostMatchStepAsync wrapper follows for its 7 steps.
                            Console.WriteLine($"Ingest end-of-match post-processing failed: {ex.Message}");
                        }
                        var matchStateAtEnd = rootProvider.GetRequiredService<MatchStateStore>();
                        matchStateAtEnd.PublishMatchStatus("");
                        ingestCoordinator.Clear();
                    }
                    return Results.Ok(new { ok = true, matchEnded = ingestCoordinator.CurrentMatch is null });
                }

                if (string.IsNullOrWhiteSpace(tick.PlayerListJson) || string.IsNullOrWhiteSpace(tick.TeamInfoJson))
                {
                    return Results.Json(new { ok = false, error = "IsInGame=true ticks must include PlayerListJson and TeamInfoJson." }, statusCode: StatusCodes.Status400BadRequest);
                }

                using var scope = rootProvider.CreateScope();
                var liveStatsBusiness = scope.ServiceProvider.GetRequiredService<LiveStatsBusiness>();
                var matchState = scope.ServiceProvider.GetRequiredService<MatchStateStore>();

                try
                {
                    var rawPlayers = JsonSerializer.Deserialize<LivePlayersList>(tick.PlayerListJson)!;
                    var teamInfoList = JsonSerializer.Deserialize<TeamInfoList>(tick.TeamInfoJson)!;
                    var filteredPlayerInfo = LiveStatsBusiness.FilterPlayerInfo(rawPlayers);

                    var liveStatsResult = await liveStatsBusiness.CreateDynamicLiveStats(match, filteredPlayerInfo, teamInfoList, ingestCoordinator.TeamPoints ?? new List<LiveTeamPointStats>());
                    if (liveStatsResult is List<TeamLiveStats> teamLiveStatsList)
                    {
                        matchState.PublishLiveTeams(teamLiveStatsList);
                    }
                }
                catch (Exception ex)
                {
                    return Results.Json(new { ok = false, error = $"Failed to process tick: {ex.Message}" }, statusCode: StatusCodes.Status400BadRequest);
                }

                ingestCoordinator.WasInGame = true;
                return Results.Ok(new { ok = true });
            });

            app.MapGet("/api/ingest/status", () =>
            {
                var match = ingestCoordinator.CurrentMatch;
                return Results.Json(new
                {
                    active = match is not null,
                    matchId = match?.MatchId,
                    wasInGame = ingestCoordinator.WasInGame,
                });
            });
        }
    }
}
