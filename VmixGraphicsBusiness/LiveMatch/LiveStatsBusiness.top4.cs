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

        public bool ShouldShowTop4Ranking(TeamInfoList liveTeamInfos)
        {
            var liveTeamsCount = liveTeamInfos.teamInfoList.Where(x => x.liveMemberNum > 0).Count();
            return liveTeamsCount <= 4 && liveTeamsCount > 0;
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

                // Get ALL teams in top 4 scenario (including recently eliminated ones)
                var allRelevantTeams = liveTeamInfos.teamInfoList
                    .OrderByDescending(x => x.liveMemberNum)
                    .ThenByDescending(x => x.killNum)
                    .Take(4) // Get top 4 teams (alive or recently eliminated)
                    .ToList();

                // Only proceed if we have 4 or fewer teams alive
                if (liveTeamsCount > 14 || allRelevantTeams.Count == 0)
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

                string HeatlhImages = ConfigGlobal.Images!;

                // Calculate total alive members across all live teams (only for probability calculation)
                int totalAliveMembersAcrossAllTeams = liveTeams.Sum(x => x.liveMemberNum);

                // Avoid division by zero
                if (totalAliveMembersAcrossAllTeams == 0)
                {
                    totalAliveMembersAcrossAllTeams = 1;
                }

                // Group players by team
                var groupedByTeam = playerInfo.PlayerInfoList.ToLookup(info => info.TeamId);

                // Temporary list to hold teams with their raw scores
                List<(Top4TeamStats team, double rawScore)> teamScoresTemp = new List<(Top4TeamStats, double)>();

                // Process each relevant team (including eliminated ones)
                foreach (var teamInfo in allRelevantTeams)
                {
                    try
                    {
                        var teamPlayers = groupedByTeam[teamInfo.teamId].Where(p => p.LiveState != 5).ToList(); // Exclude dead
                        var teamData = pastMatchStats.FirstOrDefault(x => x.teamid == teamInfo.teamId);

                        if (teamData == null)
                        {
                            _logger.LogWarning($"Team {teamInfo.teamId} not found in pastMatchStats, skipping...");
                            continue;
                        }

                        bool isEliminated = teamInfo.liveMemberNum == 0;
                        bool isInBlue = teamPlayers.Any(p => p.IsOutsideBlueCircle);

                        var top4Team = new Top4TeamStats
                        {
                            TeamId = teamInfo.teamId,
                            TeamName = teamData.teamName,
                            TeamLogo = $"{ConfigGlobal.LogosImages}\\{teamInfo.teamId}.png",
                            LiveMemberCount = teamInfo.liveMemberNum
                        };

                        // Calculate team health
                        double teamHealthPercentage = 0;
                        int alivePlayerCount = 0;

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

                            // Only count alive players for health calculation
                            if (player.LiveState == 0) // Alive
                            {
                                teamHealthPercentage += healthPercent;
                                alivePlayerCount++;
                            }
                        }

                        // If team is eliminated, set raw score to 0
                        double rawScore = 0;

                        if (!isEliminated)
                        {
                            // Calculate average team health (only for alive players)
                            double averageTeamHealth = alivePlayerCount > 0 ? teamHealthPercentage / alivePlayerCount : 0;

                            // ✅ WIN PROBABILITY FACTORS
                            // 1. Member advantage (50% weight) - more alive players = better chance
                            double memberAdvantage = (double)teamInfo.liveMemberNum / totalAliveMembersAcrossAllTeams;

                            // 2. Health advantage (30% weight) - healthier team = better chance
                            double healthAdvantage = averageTeamHealth / 100.0;

                            // 3. Kill advantage (20% weight) - more aggressive team gets bonus
                            double maxKills = allRelevantTeams.Max(t => t.killNum);
                            double killAdvantage = maxKills > 0 ? (double)teamInfo.killNum / maxKills : 0;

                            // 4. Position penalty - being outside the zone is dangerous
                            double positionPenalty = isInBlue ? 0.7 : 1.0; // 30% penalty if outside blue

                            // ✅ CALCULATE RAW SCORE
                            rawScore = (
                                memberAdvantage * 0.50 +  // 50% based on team size
                                healthAdvantage * 0.30 +  // 30% based on health
                                killAdvantage * 0.20      // 20% based on kills
                            ) * positionPenalty;
                        }

                        teamScoresTemp.Add((top4Team, rawScore));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing team {TeamId} for Top 4", teamInfo.teamId);
                    }
                }

                // ✅ NORMALIZE SCORES TO PERCENTAGES (sum to 100%)
                double totalScore = teamScoresTemp.Where(ts => ts.rawScore > 0).Sum(ts => ts.rawScore);

                if (totalScore == 0)
                {
                    totalScore = 1; // Prevent division by zero
                }

                // Calculate normalized percentages
                foreach (var (team, rawScore) in teamScoresTemp)
                {
                    if (rawScore > 0)
                    {
                        team.WinProbability = (rawScore / totalScore) * 100;
                    }
                    else
                    {
                        team.WinProbability = 0.00; // Eliminated teams get 0%
                    }
                }

                // Sort teams by win probability (descending)
                var sortedTeams = teamScoresTemp.OrderByDescending(t => t.team.WinProbability).ToList();

                // ✅ UPDATE VMIX FOR EACH TEAM
                int position = 1;
                foreach (var (team, rawScore) in sortedTeams)
                {
                    var teamData = pastMatchStats.FirstOrDefault(x => x.teamid == team.TeamId);
                    var teamInfo = allRelevantTeams.FirstOrDefault(x => x.teamId == team.TeamId);
                    var teamPlayers = groupedByTeam[team.TeamId].Where(p => p.LiveState != 5).ToList();

                    bool isEliminated = teamInfo.liveMemberNum == 0;
                    bool isInBlue = teamPlayers.Any(p => p.IsOutsideBlueCircle);

                    // Set vMix elements
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"TAGT{position}", teamData.teamName.ToUpper()));

                    // Show percentage even if 0.00% for eliminated teams
                    if (isEliminated)
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"PERCENTAGE{position}", ""));
                    }
                    else if (team.WinProbability >= 5)
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"PERCENTAGE{position}", $"{team.WinProbability:F1}%"));
                    }
                    else
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"PERCENTAGE{position}", $""));
                    }

                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"LOGOT{position}", $"{ConfigGlobal.LogosImages}\\{teamData.teamid}.png"));

                    // Zone status background
                    if (isEliminated)
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"EliminatedBGT{position}", HeatlhImages + "\\EliminatedBG\\Team Dead 4.png"));
                    }
                    else if (!isInBlue)
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"EliminatedBGT{position}", HeatlhImages + "\\EliminatedBG\\Team In Zone 4.png"));
                    }
                    else
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"EliminatedBGT{position}", HeatlhImages + "\\EliminatedBG\\Team Out Zone 4.png"));
                    }

                    // Set player health images
                    for (int playerIndex = 0; playerIndex < 4; playerIndex++)
                    {
                        if (playerIndex < team.PlayersHealth.Count)
                        {
                            apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"T{position}P{playerIndex + 1}", team.PlayersHealth[playerIndex].HealthImage));
                        }
                        else
                        {
                            apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"T{position}P{playerIndex + 1}", HeatlhImages + "\\Dead\\0.png"));
                        }
                    }

                    position++;
                    if (position > 4) break;
                }

                // Hide unused team slots
                for (int i = position; i <= 4; i++)
                {
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"TEAMNAME{i}", ""));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"PERCENTAGE{i}", ""));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"LOGO{i}", ""));

                    for (int playerIndex = 1; playerIndex <= 4; playerIndex++)
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"T{i}P{playerIndex}", ""));
                    }
                }

                // Enqueue API calls
                backgroundJobClient.Enqueue<ApiCallProcessor>(HangfireQueues.Default, processor => processor.ProcessApiCalls(apiCalls));

                _logger.LogInformation($"Top 4 live ranking created. Probabilities: {string.Join(", ", sortedTeams.Select(t => $"{t.team.TeamName}={t.team.WinProbability:F1}%"))}");

                return sortedTeams.Select(t => t.team).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateTop4LiveRanking");
                return null;
            }
        }
    }
}
