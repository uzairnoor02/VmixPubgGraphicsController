using OfficeOpenXml;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using static Google.Apis.Sheets.v4.SheetsService;
using VmixData.Models.MatchModels;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using VmixData.Models;
using Hangfire;
using Microsoft.Extensions.Logging;
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
            /// <summary>Carried frag/smoke/molotov/stun as last seen by the observer (null until
            /// the observer has watched this team this match).</summary>
            public TeamThrowables? Throwables { get; set; }
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
            // Top 4 win-probability board, computed inline in tick order (it used to be a separate
            // Hangfire job per tick, which could run late or out of order). It publishes itself to
            // the overlay via MatchStateStore.PublishTop4Rankings; a failure here never blocks
            // the main standings board below.
            if (ShouldShowTop4Ranking(liveTeamInfos))
            {
                await CreateTop4LiveRanking(playerInfo, liveTeamInfos, pastMatchStats);
            }
            return await CreateLiveStats(match, playerInfo, liveTeamInfos, pastMatchStats);

        }

        [Queue(HangfireQueues.HighPriority)]
        [AutomaticRetry(Attempts = 0), DisableConcurrentExecution(timeoutInSeconds: 2)]
        public async Task<List<Top4TeamStats>> CreateTop4LiveRanking(LivePlayersList playerInfo, TeamInfoList liveTeamInfos, List<LiveTeamPointStats> pastMatchStats)
        {
            using var scope = serviceProvider.CreateScope();
            var redis = scope.ServiceProvider.GetRequiredService<MatchStateStore>();

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

                string HeatlhImages = ConfigGlobal.Images!;

                // ✅ RETRIEVE OR INITIALIZE FIXED TEAM POSITIONS WITH 15 MINUTE EXPIRATION
                string top4PositionsKey = "Top4TeamPositions";
                var storedPositions = await redis.StringGetAsync(top4PositionsKey);
                Dictionary<int, int> teamPositions; // TeamId -> Position mapping

                if (string.IsNullOrEmpty(storedPositions))
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

                        // Every roster slot is shown on the card (dead players as a grey helmet), but
                        // only players still in the fight count towards the win probability.
                        var rosterPlayers = groupedByTeam[teamInfo.teamId].Take(4).ToList();
                        var teamPlayers = rosterPlayers.Where(p => p.LiveState != 5).ToList(); // Exclude dead
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
                            TeamLogo = MediaUrls.TeamLogo(teamInfo.teamId),
                            LiveMemberCount = teamInfo.liveMemberNum,
                            Throwables = redis.Inventory.Get(teamInfo.teamId)
                        };

                        // Calculate team health. Every non-dead player contributes to the average
                        // (dead players are already excluded further up via teamPlayers' LiveState != 5
                        // filter) - a standing player counts at full weight, a knocked player counts at a
                        // small fraction of their health rather than being dropped from the average
                        // entirely. Dropping knocked players used to let a team with 3 of 4 members
                        // knocked down show the same win% as a team with all 4 standing, as long as the
                        // one remaining standing player was healthy - understating exactly the teams that
                        // are most at risk.
                        const float KnockedCombatWeight = 0.15f;
                        double teamHealthWeighted = 0;
                        int countedPlayerCount = 0;

                        foreach (var player in rosterPlayers) // Max 4 players, dead included
                        {
                            var healthInfo = EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax);
                            float healthPercent = player.HealthMax > 0 ? (player.Health / (float)player.HealthMax * 100) : 0;

                            top4Team.PlayersHealth.Add(new PlayerHealthInfo
                            {
                                HealthImage = HeatlhImages + healthInfo.HealthImage,
                                HealthPercent = healthPercent,
                                LiveState = player.LiveState
                            });

                            if (player.LiveState >= 0 && player.LiveState <= 3) // standing / alive
                            {
                                teamHealthWeighted += healthPercent;
                                countedPlayerCount++;
                            }
                            else if (player.LiveState == 4) // knocked out - can't fight, near-elimination
                            {
                                teamHealthWeighted += healthPercent * KnockedCombatWeight;
                                countedPlayerCount++;
                            }
                        }

                        // If team is eliminated, set raw score to 0
                        double rawScore = 0;

                        if (!isEliminated)
                        {
                            // Calculate average team health across the whole squad (standing + knocked)
                            double averageTeamHealth = countedPlayerCount > 0 ? teamHealthWeighted / countedPlayerCount : 0;

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

                // ✅ SORT BY FIXED POSITION (NOT by win probability) - a team keeps its card slot
                // for the rest of the match instead of the cards reshuffling every tick.
                var sortedTeams = teamScoresTemp.OrderBy(t => t.position).ToList();

                _logger.LogDebug("Top 4 updated: {Positions}", string.Join(", ", sortedTeams.Select(t => $"T{t.position}={t.team.TeamName}({t.team.WinProbability:F1}%)")));

                var finalTeams = sortedTeams.Select(t => t.team).ToList();

                // The overlay's Top 4 / WWCD panel - see MatchStateStore.PublishTop4Rankings.
                using (var pubScope = serviceProvider.CreateScope())
                {
                    var matchState = pubScope.ServiceProvider.GetRequiredService<MatchStateStore>();
                    matchState.PublishTop4Rankings(finalTeams);
                }

                return finalTeams;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateTop4LiveRanking");
                return null;
            }
        }
    }
}
