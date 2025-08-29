using OfficeOpenXml;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using static Google.Apis.Sheets.v4.SheetsService;
using VmixData.Models.MatchModels;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using VmixGraphicsBusiness.vmixutils;
using VmixData.Models;
using Hangfire;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using VmixGraphicsBusiness.Utils;
using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.EntityFrameworkCore;

namespace VmixGraphicsBusiness.LiveMatch
{
    public partial class LiveStatsBusiness
    {
        public class Top4TeamStats
        {
            public int TeamId { get; set; }
            public string TeamName { get; set; }
            public string TeamLogo { get; set; }
            public int LiveMemberCount { get; set; }
            public double WinProbability { get; set; }
            public List<PlayerHealthInfo> PlayersHealth { get; set; } = new List<PlayerHealthInfo>();
        }

        public class PlayerHealthInfo
        {
            public string HealthImage { get; set; }
            public float HealthPercent { get; set; }
            public int LiveState { get; set; }
        }
        [AutomaticRetry(Attempts = 0), DisableConcurrentExecution(timeoutInSeconds: 2)]
        public async Task<List<Top4TeamStats>> CreateTop4LiveRanking(LivePlayersList playerInfo, TeamInfoList liveTeamInfos, List<LiveTeamPointStats> pastMatchStats)
        {
            using var scope = serviceProvider.CreateScope();
            IConnectionMultiplexer redisConnection = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            List<string> apiCalls = new List<string>();
            var redis = redisConnection.GetDatabase();
            var vmixdata = await VmixDataUtils.SetVMIXDataoperations();

            try
            {
                // Get live teams (teams with members alive)
                var liveTeams = liveTeamInfos.teamInfoList.Where(x => x.liveMemberNum > 0).ToList();
                var liveTeamsCount = liveTeams.Count();

                // Only proceed if we have exactly 4 or fewer teams alive
                if (liveTeamsCount > 14 || liveTeamsCount == 0)
                {
                    _logger.LogInformation($"Live teams count is {liveTeamsCount}, not suitable for Top 4 display");
                    return null;
                }

                // Use the Top 4 GUID
                string top4RankingGuid = vmixdata.LiverankingGuid4;

                var oldguid = redis.StringGet("Top4RankingGuid");
                if (oldguid != top4RankingGuid)
                {
                    await redis.StringSetAsync("Top4RankingGuid", top4RankingGuid);
                    apiCalls.Add($"function=OverlayInput{4}Out&input={oldguid}");
                    apiCalls.Add($"function=OverlayInput{4}In&input={top4RankingGuid}");
                }

                List<Top4TeamStats> top4Teams = new List<Top4TeamStats>();
                string HeatlhImages = ConfigGlobal.Images!;

                // Calculate total alive members across all live teams for win probability calculation
                int totalAliveMembersAcrossAllTeams = liveTeams.Sum(x => x.liveMemberNum);

                // Group players by team
                var groupedByTeam = playerInfo.PlayerInfoList.ToLookup(info => info.TeamId);

                // Process each live team
                int position = 1;
                foreach (var teamInfo in liveTeams.OrderByDescending(x => x.liveMemberNum)
                                                  .ThenByDescending(x => x.killNum))
                {
                    try
                    {
                        var teamPlayers = groupedByTeam[teamInfo.teamId].Where(p => p.LiveState != 5).ToList(); // Exclude dead players
                        var teamData = pastMatchStats.FirstOrDefault(x => x.teamid == teamInfo.teamId);

                        if (teamData == null)
                        {
                            _logger.LogWarning($"Team {teamInfo.teamId} not found in pastMatchStats, skipping...");
                            continue;
                        }
                        if (teamData == null)
                        {
                            _logger.LogWarning($"Team {teamInfo.teamId} not found in pastMatchStats, skipping...");
                            continue;
                        }

                        bool isEliminated = teamInfo.liveMemberNum == 0;  // ✅ from CreateLiveStats
                        bool isinBlue = teamPlayers.Any(p => p.IsOutsideBlueCircle); // ✅ check players

                        var top4Team = new Top4TeamStats
                        {
                            TeamId = teamInfo.teamId,
                            TeamName = teamData.teamName,
                            TeamLogo = $"{ConfigGlobal.LogosImages}\\{teamInfo.teamId}.png",
                            LiveMemberCount = teamInfo.liveMemberNum
                        };

                        // Calculate team health percentage
                        double teamHealthPercentage = 0;
                        bool hasPlayersOutsideBlueCircle = false;

                        foreach (var player in teamPlayers.Take(4)) // Max 4 players
                        {
                            var healthInfo = EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax);
                            float healthPercent = player.HealthMax > 0 ? (player.Health / (float)player.HealthMax * 100) : 0;

                            top4Team.PlayersHealth.Add(new PlayerHealthInfo
                            {
                                HealthImage = HeatlhImages + healthInfo.HealthImage,
                                HealthPercent = healthPercent,
                                LiveState = player.LiveState
                            });

                            // Add to team health calculation (alive and knocked players contribute to health)
                            if (player.LiveState != 5) // Not dead
                            {
                                teamHealthPercentage += healthPercent;
                            }

                            if (player.IsOutsideBlueCircle)
                            {
                                hasPlayersOutsideBlueCircle = true;
                            }
                        }

                        // Calculate average team health
                        double averageTeamHealth = teamPlayers.Count > 0 ? teamHealthPercentage / teamPlayers.Count : 0;
                        // Temporary list to hold raw scores
                        List<(Top4TeamStats team, double rawScore)> teamScores = new();

                        double memberAdvantage = (double)teamInfo.liveMemberNum / totalAliveMembersAcrossAllTeams;
                        double healthAdvantage = averageTeamHealth / 100.0;
                        double positionPenalty = hasPlayersOutsideBlueCircle ? 0.8 : 1.0; // 20% penalty if outside blue circle
                                                                                         

                        // Inside your foreach (instead of directly assigning WinProbability)
                        double rawScore = (memberAdvantage * 0.6 + healthAdvantage * 0.4) * positionPenalty;
                        teamScores.Add((top4Team, rawScore));
                        double totalScore = teamScores.Sum(ts => ts.rawScore);

                        foreach (var (team, rawScore1) in teamScores)
                        {
                            team.WinProbability = totalScore > 0 ? (rawScore1 / totalScore) * 100 : 0;
                        }
                        // Win probability formula
                        top4Team.WinProbability = (memberAdvantage * 0.6 + healthAdvantage * 0.4) * positionPenalty * 100;

                        // Ensure probability doesn't exceed 100%
                        top4Team.WinProbability = Math.Min(top4Team.WinProbability, 100);

                        //Set vMix elements for this team position// Set vMix elements for this team position
                        apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"TEAMNAME{position}", teamData.teamName.ToUpper()));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"PERCENTAGE{position}", $"{top4Team.WinProbability:F1}%"));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"LOGO{position}", top4Team.TeamLogo));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"LOGOT{position}", $"{ConfigGlobal.LogosImages}" + $"\\{teamData.teamid}.png"));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"LOGOT{position}", $"{ConfigGlobal.LogosImages}" + $"\\0.png"));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"LOGOT{position}", $"{ConfigGlobal.LogosImages}" + $"\\{teamData.teamid}.png"));

                        if (isEliminated)
                        {
                            apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"EliminatedBGT{position}", HeatlhImages + "\\EliminatedBG\\Team Dead.png"));
                        }
                        else if (isinBlue)
                        {
                            apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"EliminatedBGT{position}", HeatlhImages + "\\EliminatedBG\\Team In Zone.png"));
                        }
                        else
                        {
                            apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"EliminatedBGT{position}", HeatlhImages + "\\EliminatedBG\\Team Out Zone.png"));
                        }

                        // Set player health images (up to 4 players)
                        for (int playerIndex = 0; playerIndex < 4; playerIndex++)
                        {
                            if (playerIndex < top4Team.PlayersHealth.Count)
                            {
                                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"T{position}P{playerIndex + 1}", top4Team.PlayersHealth[playerIndex].HealthImage));
                            }
                            else
                            {
                                // Hide unused player slots
                                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"T{position}P{playerIndex + 1}", HeatlhImages + "\\Dead\\0.png"));
                            }
                        }

                        top4Teams.Add(top4Team);
                        position++;

                        if (position > 4) break; // Only show top 4 teams
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing team {TeamId} for Top 4: {Message}", teamInfo.teamId, ex.Message);
                    }
                }

                // Hide unused team slots if less than 4 teams
                for (int i = position; i <= 4; i++)
                {
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"TEAMNAME{i}", ""));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"WWCD{i}", ""));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"LOGO{i}", ""));

                    for (int playerIndex = 1; playerIndex <= 4; playerIndex++)
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"T{i}P{playerIndex}", ""));
                    }
                }

                // Enqueue API calls to Hangfire
                backgroundJobClient.Enqueue<ApiCallProcessor>(HangfireQueues.Default, processor => processor.ProcessApiCalls(apiCalls));

                _logger.LogInformation($"Created Top 4 live ranking with {top4Teams.Count} teams");
                return top4Teams;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateTop4LiveRanking: {Message}", ex.Message);
                return null;
            }
        }

        //[AutomaticRetry(Attempts = 0)]
        //public async Task<List<Top4TeamStats>> CreateTop4LiveRanking(LivePlayersList playerInfo, TeamInfoList liveTeamInfos, List<LiveTeamPointStats> pastMatchStats)
        //{
        //    using var scope = serviceProvider.CreateScope();
        //    IConnectionMultiplexer redisConnection = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        //    List<string> apiCalls = new List<string>();
        //    var redis = redisConnection.GetDatabase();
        //    var vmixdata = await VmixDataUtils.SetVMIXDataoperations();

        //    try
        //    {
        //        // Get live teams (teams with members alive)
        //        var liveTeams = liveTeamInfos.teamInfoList.Where(x => x.liveMemberNum > 0).ToList();
        //        var liveTeamsCount = liveTeams.Count();

        //        // Only proceed if we have exactly 4 or fewer teams alive
        //        if (liveTeamsCount > 5 || liveTeamsCount == 0)
        //        {
        //            return null;
        //        }

        //        // Use the Top 4 GUID
        //        string top4RankingGuid = vmixdata.LiverankingGuid4;

        //        var oldguid = redis.StringGet("Top4RankingGuid");
        //        if (oldguid != top4RankingGuid)
        //        {
        //            await redis.StringSetAsync("Top4RankingGuid", top4RankingGuid);
        //            apiCalls.Add($"function=OverlayInput{4}Out&input={oldguid}");
        //            apiCalls.Add($"function=OverlayInput{4}In&input={top4RankingGuid}");
        //        }

        //        List<Top4TeamStats> top4Teams = new List<Top4TeamStats>();
        //        string HeatlhImages = ConfigGlobal.Images!;

        //        // Calculate total alive members across all live teams for win probability calculation
        //        int totalAliveMembersAcrossAllTeams = liveTeams.Sum(x => x.liveMemberNum);

        //        // Group players by team
        //        var groupedByTeam = playerInfo.PlayerInfoList.ToLookup(info => info.TeamId);

        //        // Process each live team
        //        int position = 1;
        //        foreach (var teamInfo in liveTeams.OrderByDescending(x => x.liveMemberNum)
        //                                          .ThenByDescending(x => x.killNum))
        //        {
        //            try
        //            {
        //                var teamPlayers = groupedByTeam[teamInfo.teamId].Where(p => p.LiveState != 5).ToList(); // Exclude dead players
        //                var teamData = pastMatchStats.FirstOrDefault(x => x.teamid == teamInfo.teamId);

        //                if (teamData == null)
        //                {
        //                    _logger.LogWarning($"Team {teamInfo.teamId} not found in pastMatchStats, skipping...");
        //                    continue;
        //                }

        //                var top4Team = new Top4TeamStats
        //                {
        //                    TeamId = teamInfo.teamId,
        //                    TeamName = teamData.teamName,
        //                    TeamLogo = $"{ConfigGlobal.LogosImages}\\{teamInfo.teamId}.png",
        //                    LiveMemberCount = teamInfo.liveMemberNum
        //                };

        //                // Calculate team health percentage
        //                double teamHealthPercentage = 0;
        //                bool hasPlayersOutsideBlueCircle = false;

        //                foreach (var player in teamPlayers.Take(4)) // Max 4 players
        //                {
        //                    var healthInfo = EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax);
        //                    float healthPercent = player.HealthMax > 0 ? (player.Health / (float)player.HealthMax * 100) : 0;

        //                    top4Team.PlayersHealth.Add(new PlayerHealthInfo
        //                    {
        //                        HealthImage = HeatlhImages + healthInfo.HealthImage,
        //                        HealthPercent = healthPercent,
        //                        LiveState = player.LiveState
        //                    });

        //                    // Add to team health calculation (alive and knocked players contribute to health)
        //                    if (player.LiveState != 5) // Not dead
        //                    {
        //                        teamHealthPercentage += healthPercent;
        //                    }

        //                    if (player.IsOutsideBlueCircle)
        //                    {
        //                        hasPlayersOutsideBlueCircle = true;
        //                    }
        //                }

        //                // Calculate average team health
        //                double averageTeamHealth = teamPlayers.Count > 0 ? teamHealthPercentage / teamPlayers.Count : 0;

        //                // Calculate win probability based on:
        //                // 1. Member count advantage (33.33% base per member)
        //                // 2. Team health percentage
        //                // 3. Position advantage (being outside blue circle reduces chances)

        //                double memberAdvantage = (double)teamInfo.liveMemberNum / totalAliveMembersAcrossAllTeams;
        //                double healthAdvantage = averageTeamHealth / 100.0;
        //                double positionPenalty = hasPlayersOutsideBlueCircle ? 0.8 : 1.0; // 20% penalty if outside blue circle

        //                // Win probability formula
        //                top4Team.WinProbability = (memberAdvantage * 0.6 + healthAdvantage * 0.4) * positionPenalty * 100;

        //                // Ensure probability doesn't exceed 100%
        //                top4Team.WinProbability = Math.Min(top4Team.WinProbability, 100);

        //                // Set vMix elements for this team position
        //                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"TEAMNAME{position}", teamData.teamName.ToUpper()));
        //                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"PERCENTAGE{position}", $"{top4Team.WinProbability:F1}%"));
        //                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"LOGO{position}", top4Team.TeamLogo));
        //                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"LOGOT{position}", $"{ConfigGlobal.LogosImages}" + $"\\{teamData.teamid}.png"));
        //                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"LOGOT{position}", $"{ConfigGlobal.LogosImages}" + $"\\0.png"));
        //                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"LOGOT{position}", $"{ConfigGlobal.LogosImages}" + $"\\{teamData.teamid}.png"));


        //                // Set player health images (up to 4 players)
        //                for (int playerIndex = 0; playerIndex < 4; playerIndex++)
        //                {
        //                    if (playerIndex < top4Team.PlayersHealth.Count)
        //                    {
        //                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"T{position}P{playerIndex + 1}", top4Team.PlayersHealth[playerIndex].HealthImage));
        //                    }
        //                    else
        //                    {
        //                        // Hide unused player slots
        //                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"T{position}P{playerIndex + 1}", HeatlhImages + "\\Dead\\0.png"));
        //                    }
        //                }

        //                top4Teams.Add(top4Team);
        //                position++;

        //                if (position > 4) break; // Only show top 4 teams
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogError(ex, "Error processing team {TeamId} for Top 4: {Message}", teamInfo.teamId, ex.Message);
        //            }
        //        }

        //        // Hide unused team slots if less than 4 teams
        //        for (int i = position; i <= 4; i++)
        //        {
        //            apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"TEAMNAME{i}", ""));
        //            apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"WWCD{i}", ""));
        //            apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"LOGO{i}", ""));

        //            for (int playerIndex = 1; playerIndex <= 4; playerIndex++)
        //            {
        //                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"T{i}P{playerIndex}", ""));
        //            }
        //        }

        //        // Enqueue API calls to Hangfire
        //        backgroundJobClient.Enqueue<ApiCallProcessor>(HangfireQueues.Default, processor => processor.ProcessApiCalls(apiCalls));

        //        _logger.LogInformation($"Created Top 4 live ranking with {top4Teams.Count} teams");
        //        return top4Teams;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error in CreateTop4LiveRanking: {Message}", ex.Message);
        //        return null;
        //    }
        //}

        // Helper method to check if we should show Top 4 ranking
        public bool ShouldShowTop4Ranking(TeamInfoList liveTeamInfos)
        {
            var liveTeamsCount = liveTeamInfos.teamInfoList.Where(x => x.liveMemberNum > 0).Count();
            return liveTeamsCount <= 15 && liveTeamsCount > 0;
        }

        // Method to integrate with your existing CreateLiveStats method
        public async Task<object> CreateDynamicLiveStats(Match match, LivePlayersList playerInfo, TeamInfoList liveTeamInfos, List<LiveTeamPointStats> pastMatchStats)
        {
            // Check if we should show Top 4 ranking
            if (ShouldShowTop4Ranking(liveTeamInfos))
            {
                _logger.LogInformation("Switching to Top 4 live ranking display");
                // CreateTop4LiveRanking(playerInfo, liveTeamInfos, pastMatchStats);
                backgroundJobClient.Enqueue(HangfireQueues.HighPriority, () => CreateTop4LiveRanking(playerInfo, liveTeamInfos, pastMatchStats));

            }
            _logger.LogInformation("Using standard live ranking display");
            return await CreateLiveStats(match, playerInfo, liveTeamInfos, pastMatchStats);

        }

    }
}
