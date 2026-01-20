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

        [AutomaticRetry(Attempts = 0), DisableConcurrentExecution(timeoutInSeconds: 3)]
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

                // ✅ RETRIEVE OR INITIALIZE FIXED TEAM POSITIONS WITH 15 MINUTE EXPIRATION
                string top4PositionsKey = "Top4TeamPositions";
                var storedPositions = await redis.StringGetAsync(top4PositionsKey);
                Dictionary<int, int> teamPositions; // TeamId -> Position mapping

                if (storedPositions.IsNullOrEmpty)
                {
                    // First time entering Top 4 - assign positions based on current ranking
                    _logger.LogInformation("Initializing Top 4 team positions for the first time");
                    teamPositions = new Dictionary<int, int>();

                    var initialRanking = allRelevantTeams
                        .OrderByDescending(x => x.liveMemberNum)
                        .ThenByDescending(x => x.killNum)
                        .ToList();

                    for (int i = 0; i < initialRanking.Count && i < 4; i++)
                    {
                        teamPositions[initialRanking[i].teamId] = i + 1;
                    }

                    // Store in Redis with 15 minute expiration
                    var serializedPositions = JsonSerializer.Serialize(teamPositions);
                    await redis.StringSetAsync(top4PositionsKey, serializedPositions, TimeSpan.FromMinutes(15));

                    _logger.LogInformation($"Stored initial positions with 15min expiration: {string.Join(", ", teamPositions.Select(kv => $"Team{kv.Key}=T{kv.Value}"))}");
                }
                else
                {
                    // Use existing positions and refresh the 15-minute expiration
                    teamPositions = JsonSerializer.Deserialize<Dictionary<int, int>>(storedPositions.ToString());

                    // Refresh expiration to 15 minutes from now
                    await redis.KeyExpireAsync(top4PositionsKey, TimeSpan.FromMinutes(15));

                    _logger.LogInformation($"Using existing positions (expiration refreshed): {string.Join(", ", teamPositions.Select(kv => $"Team{kv.Key}=T{kv.Value}"))}");
                }

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
                List<(Top4TeamStats team, double rawScore, int position)> teamScoresTemp = new List<(Top4TeamStats, double, int)>();

                // Process each relevant team (including eliminated ones)
                foreach (var teamInfo in allRelevantTeams)
                {
                    try
                    {
                        // Skip teams not in our fixed positions
                        if (!teamPositions.ContainsKey(teamInfo.teamId))
                        {
                            _logger.LogWarning($"Team {teamInfo.teamId} not in fixed positions, skipping...");
                            continue;
                        }

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

                        // Get the fixed position for this team
                        int fixedPosition = teamPositions[teamInfo.teamId];
                        teamScoresTemp.Add((top4Team, rawScore, fixedPosition));
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
                foreach (var (team, rawScore, position) in teamScoresTemp)
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

                // ✅ SORT BY FIXED POSITION (NOT by win probability)
                var sortedTeams = teamScoresTemp.OrderBy(t => t.position).ToList();

                // ✅ UPDATE VMIX FOR EACH TEAM IN THEIR FIXED POSITIONS
                foreach (var (team, rawScore, position) in sortedTeams)
                {
                    var teamData = pastMatchStats.FirstOrDefault(x => x.teamid == team.TeamId);
                    var teamInfo = allRelevantTeams.FirstOrDefault(x => x.teamId == team.TeamId);
                    var teamPlayers = groupedByTeam[team.TeamId].Where(p => p.LiveState != 5).ToList();

                    bool isEliminated = teamInfo.liveMemberNum == 0;
                    bool isInBlue = teamPlayers.Any(p => p.IsOutsideBlueCircle);

                    // Set vMix elements using FIXED position
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
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"EliminatedBGT{position}", HeatlhImages + "\\EliminatedBG\\Team Dead4.png"));
                    }
                    else if (isInBlue)
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"EliminatedBGT{position}", HeatlhImages + "\\EliminatedBG\\Team In Zone4.png"));
                    }
                    else
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"EliminatedBGT{position}", HeatlhImages + "\\EliminatedBG\\Team Out Zone.png"));
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
                }

                // Hide unused team slots (if less than 4 teams)
                var usedPositions = sortedTeams.Select(t => t.position).ToList();
                for (int i = 1; i <= 4; i++)
                {
                    if (!usedPositions.Contains(i))
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"TEAMNAME{i}", ""));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(top4RankingGuid, $"PERCENTAGE{i}", ""));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"LOGO{i}", ""));

                        for (int playerIndex = 1; playerIndex <= 4; playerIndex++)
                        {
                            apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(top4RankingGuid, $"T{i}P{playerIndex}", ""));
                        }
                    }
                }

                // Enqueue API calls
                backgroundJobClient.Enqueue<ApiCallProcessor>(HangfireQueues.Default, processor => processor.ProcessApiCalls(apiCalls));

                _logger.LogInformation($"Top 4 live ranking updated. Positions: {string.Join(", ", sortedTeams.Select(t => $"T{t.position}={t.team.TeamName}({t.team.WinProbability:F1}%)"))}");

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
