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

namespace VmixGraphicsBusiness.LiveMatch;
public class LiveStatsBusiness(
        IConfiguration config,
        IBackgroundJobClient backgroundJobClient,
        IServiceProvider serviceProvider,
        ILogger<LiveStatsBusiness> _logger,
        vmix_graphicsContext vmix_GraphicsContext)
{
    VmixData.Models.MatchModels.VmixData VMIXData;
    string LiverankingGuid;
    string TeamEliminatedGuid;
    static string ApplicationName = "Vmix GT titles";
    static string SpreadsheetId = "16hpBeXg_3PX_eyPEwk5pV0jPa07RgKCgKbxPvr0avpQ"; // Replace with your spreadsheet ID
    static string SheetName = "Live ranking"; // Replace with your sheet name
    static string ApiKey = "AIzaSyArCp-haDhlIEb_zeuy4vZiC9syjyG-H5I"; // Replace with your API key
    public readonly IConfiguration _config = config;

    [AutomaticRetry(Attempts = 0), DisableConcurrentExecution(timeoutInSeconds: 2)]
    public async Task<List<TeamLiveStats>> CreateLiveStats(LivePlayersList playerInfo, TeamInfoList liveTeamInfos, List<LiveTeamPointStats> pastMatchStats)
    {
        using var scope = serviceProvider.CreateScope();
        IConnectionMultiplexer redisConnection = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        List<string> apiCalls = new List<string>();
        var redis = redisConnection.GetDatabase();
        var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
        var liveteams = liveTeamInfos.teamInfoList.Where(x => x.liveMemberNum > 0).Count();

        if (liveteams < 5)
        {
            LiverankingGuid = vmixdata.LiverankingGuid4;
        }
        if (liveTeamInfos.teamInfoList.Count() < 17)
        {
            LiverankingGuid = vmixdata.LiverankingGuid16;
        }
        else if (liveTeamInfos.teamInfoList.Count() < 19)
        {
            LiverankingGuid = vmixdata.LiverankingGuid16;
        }
        else if (liveTeamInfos.teamInfoList.Count() >= 19)
        {
            LiverankingGuid = vmixdata.LiverankingGuid16;
        }

        var oldguid = redis.StringGet("LiveRankingGuid");
        if (oldguid != LiverankingGuid)
        {
            await redis.StringSetAsync("LiveRankingGuid", LiverankingGuid);
            apiCalls.Add($"function=OverlayInput{4}Out&input={oldguid}");
            apiCalls.Add($"function=OverlayInput{4}In&input={LiverankingGuid}");
        }

        try
        {
            string folderPath = _config["SaveToFolder"]!;
            string HeatlhImages = ConfigGlobal.Images!;
            List<TeamLiveStats> teamLiveStats = new List<TeamLiveStats>();

            foreach (var teamdata in pastMatchStats)
            {
                teamdata.totalScore = teamdata.score + liveTeamInfos.teamInfoList.Where(x => x.teamId == teamdata.teamid).Select(x => x.killNum).FirstOrDefault();
            }

            // Get overall rankings for all teams from database
            using var scope2 = serviceProvider.CreateScope();
            //var context = scope2.ServiceProvider.GetRequiredService<vmix_graphicsContext>();

            // Get all teams with their total points and WWCD from database
            var allTeamRanks = await vmix_GraphicsContext.TeamPoints
                .GroupBy(tp => tp.TeamId)
                .Select(g => new
                {
                    TeamId = g.Key,
                    TotalPoints = g.Sum(x => x.KillPoints) + g.Sum(x => x.PlacementPoints),
                    WWCD = g.Sum(x => x.WWCD)
                })
                .OrderByDescending(x => x.TotalPoints)
                .ThenByDescending(x => x.WWCD)
                .ToListAsync();

            // Create a dictionary mapping TeamId to their overall database ranking
            var teamToOverallRank = allTeamRanks
                .Select((team, index) => new { TeamId = team.TeamId, OverallRank = index + 1 })
                .ToDictionary(x => x.TeamId, x => x.OverallRank);

            // Get currently playing teams and sort them by their overall ranking
            var playingTeams = liveTeamInfos.teamInfoList
                .Where(x => teamToOverallRank.ContainsKey(x.teamId))
                .Select(x => new { TeamInfo = x, OverallRank = teamToOverallRank[x.teamId] })
                .OrderBy(x => x.OverallRank)
                .ToList();

            // Create a dictionary for UI positioning (1st playing team gets position 1, 2nd gets position 2, etc.)
            var teamToUIPosition = playingTeams
                .Select((team, index) => new { TeamId = team.TeamInfo.teamId, UIPosition = index + 1 })
                .ToDictionary(x => x.TeamId, x => x.UIPosition);

            var groupedByTeam = playerInfo.PlayerInfoList.ToLookup(info => info.TeamId);

            foreach (var teamGroup in groupedByTeam)
            {
                try
                {
                    int teamId = teamGroup.Key;
                    var currentTeamInfo = pastMatchStats.FirstOrDefault(x => x.teamid == teamGroup.Key);
                    var teamStats = new TeamLiveStats();

                    // Get the overall ranking from database
                    if (!teamToOverallRank.TryGetValue(teamId, out int overallRank))
                    {
                        _logger.LogWarning($"Team {teamId} not found in overall rankings, skipping...");
                        continue;
                    }

                    // Get the UI position for this team
                    if (!teamToUIPosition.TryGetValue(teamId, out int uiPosition))
                    {
                        _logger.LogWarning($"Team {teamId} not found in UI positions, skipping...");
                        continue;
                    }

                    teamStats.Logo = "";
                    teamStats.TotalPoints = 0;

                    int eliminations = 0;
                    int playerCount = 0;
                    bool isEliminated = false;
                    var teamDictionary = liveTeamInfos.teamInfoList.ToDictionary(t => t.teamId);

                    if (liveTeamInfos.teamInfoList.First(x => x.teamId == teamId).liveMemberNum == 0)
                    {
                        isEliminated = true;
                        if (string.IsNullOrEmpty(await redis.StringGetAsync($"{HelperRedis.isEliminated}:{teamId}")))
                        {
                            await redis.StringSetAsync($"{HelperRedis.isEliminated}:{teamId}", "abc");
                            await IsEliminatedAsync(currentTeamInfo.teamName, teamGroup.Key, true, teamGroup.Sum(x => x.KillNumBeforeDie), teamGroup.FirstOrDefault()!.Rank, liveTeamInfos.teamInfoList.Count());
                            _logger.LogInformation($"All players in Team {teamGroup.Key} are dead.");
                        }
                    }
                    else
                    {
                        isEliminated = false;
                    }

                    bool isinBlue = false;
                    foreach (var player in teamGroup)
                    {
                        playerCount++;
                        switch (playerCount)
                        {
                            case 1:
                                teamStats.Player1Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{uiPosition}P1", teamStats.Player1Health));
                                if (player.IsOutsideBlueCircle)
                                    isinBlue = true;
                                break;
                            case 2:
                                teamStats.Player2Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{uiPosition}P2", teamStats.Player2Health));
                                if (player.IsOutsideBlueCircle)
                                    isinBlue = true;
                                break;
                            case 3:
                                teamStats.Player3Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{uiPosition}P3", teamStats.Player3Health));
                                if (player.IsOutsideBlueCircle)
                                    isinBlue = true;
                                break;
                            case 4:
                                teamStats.Player4Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{uiPosition}P4", teamStats.Player4Health));
                                if (player.IsOutsideBlueCircle)
                                    isinBlue = true;
                                break;
                        }

                        eliminations += player.KillNum;
                        teamStats.TotalPoints += player.KillNum;
                    }

                    teamStats.Eliminations = eliminations;
                    teamStats.Tag = currentTeamInfo.teamName;
                    _logger.LogInformation($"Team: {currentTeamInfo.teamName}, Overall Rank: {overallRank}, UI Position: {uiPosition}, Score: {currentTeamInfo.score}");

                    // Use uiPosition for UI elements but display overallRank as the actual rank
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(LiverankingGuid, $"ELIMST{uiPosition}", teamStats.Eliminations.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(LiverankingGuid, $"TOTALT{uiPosition}", currentTeamInfo.score.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(LiverankingGuid, $"TAGT{uiPosition}", currentTeamInfo.teamName));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(LiverankingGuid, $"RANKT{uiPosition}", overallRank.ToString())); // Display actual database rank
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"LOGOT{uiPosition}", $"{ConfigGlobal.LogosImages}" + $"\\0.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"LOGOT{uiPosition}", $"{ConfigGlobal.LogosImages}" + $"\\{currentTeamInfo.teamid}.png"));

                    if (isEliminated)
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"EliminatedBGT{uiPosition}", HeatlhImages + "\\EliminatedBG\\Team Dead.png"));
                    }
                    else if (isinBlue)
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"EliminatedBGT{uiPosition}", HeatlhImages + "\\EliminatedBG\\Team In Zone.png"));
                    }
                    else
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"EliminatedBGT{uiPosition}", HeatlhImages + "\\EliminatedBG\\Team Out Zone.png"));
                    }

                    teamLiveStats.Add(teamStats);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing team {TeamId}: {Message}", teamGroup.Key, ex.Message);
                }
            }

            // Enqueue API calls to Hangfire
            backgroundJobClient.Enqueue<ApiCallProcessor>(HangfireQueues.Default, processor => processor.ProcessApiCalls(apiCalls));

            // Enqueue the GetAllAchievements call to Hangfire
            backgroundJobClient.Enqueue<SetPlayerAchievements>(job => job.GetAllAchievements(playerInfo, pastMatchStats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateLiveStats: {Message}", ex.Message);
        }
        return null;
    }

    public static (string HealthImage, string liveStatus) EvaluateLiveStatus(int liveState, int health, int healthMax)
    {
        string liveStatus;
        float healthPercent = 0.0f;
        string healthImage = "";

        if (healthMax > 0)
        {
            healthPercent = health / (float)healthMax * 100;
        }

        switch (liveState)
        {
            case 0:
            case 1:
            case 2:
            case 3:
                liveStatus = "Alive";
                if (healthMax > 0)
                {
                    healthPercent = health / (float)healthMax * 100;
                }
                healthImage = "\\Alive\\" + GetHealthImage(healthPercent);
                break;
            case 4:
                liveStatus = "Knocked Out";
                if (healthMax > 0)
                {
                    healthPercent = health / (float)healthMax * 100;
                }
                healthImage = "/Knocked/" + GetHealthImage(healthPercent);
                break;
            case 5:
                liveStatus = "Dead";
                healthImage = "/Dead/0.png";
                break;
            case 6:
                liveStatus = "Disconnected";
                break;
            default:
                liveStatus = "Unknown";
                break;
        }

        return (healthImage, liveStatus);
    }

    public static Dictionary<int, string> HealthImageMap = new Dictionary<int, string>()
    {
        { 0, "10.png" },
        { 10, "10.png" },
        { 20, "20.png" },
        { 30, "30.png" },
        { 40, "40.png" },
        { 50, "50.png" },
        { 60, "60.png" },
        { 70, "70.png" },
        { 80, "80.png" },
        { 90, "90.png" },
        { 100, "100.png" },
    };

    public static string GetHealthImage(float healthPercent)
    {
        int healthRange = (int)Math.Floor(healthPercent / 10) * 10; // Group health into 10% ranges
        return HealthImageMap.ContainsKey(healthRange) ? HealthImageMap[healthRange] : "health_0.png"; // Default image if not found
    }

    static string[] Scopes = { Scope.Spreadsheets };

    public async void UploadToGoogleSheets(List<IList<object>> data)
    {
        // Load _serviceProvider account credentials from the JSON file
        ServiceAccountCredential credential;
        using (var stream = new FileStream("C:\\Users\\Bilal\\source\\repos\\client_secret.json", FileMode.Open, FileAccess.Read))
        {
            credential = GoogleCredential.FromStream(stream)
                .CreateScoped(Scope.Spreadsheets)
                .UnderlyingCredential as ServiceAccountCredential;
        }

        var service = new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });

        List<ValueRange> valueRanges = new List<ValueRange>
        {
            new ValueRange
            {
                Range = $"{SheetName}!A2",
                MajorDimension = "ROWS",
                Values = data
            }
        };

        BatchUpdateValuesRequest requestBody = new BatchUpdateValuesRequest
        {
            ValueInputOption = "RAW",
            Data = valueRanges
        };

        var request = service.Spreadsheets.Values.BatchUpdate(requestBody, SpreadsheetId);
        await request.ExecuteAsync();
    }

    public async Task IsEliminatedAsync(string teamName, int teamId, bool isEliminated, int totalEliminations, int rank, int totalTeams)
    {
        using var scope = serviceProvider.CreateScope();

        var redisConnection = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();

        LiveTeamInfo team;
        var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
        TeamEliminatedGuid = vmixdata.TeamEliminatedGuid;
        List<string> apiCalls = new List<string>();
        int ranknum = totalTeams;
        try
        {
            var redis = redisConnection.GetDatabase();

            // Retrieve existing data from Redis
            string existingData = await redis.StringGetAsync($"{HelperRedis.isEliminated}:{teamId}");

            var currentrank = await redis.StringGetAsync($"{HelperRedis.isEliminated}:{teamId}");
            if (string.IsNullOrEmpty(currentrank))
            {
                currentrank = ranknum;
            }
            team = new LiveTeamInfo { TeamName = teamName, TeamId = teamId, IsEliminated = isEliminated };

            apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"elims", totalEliminations.ToString()));
            apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"teamname", teamName));
            apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"rank", "#" + rank));
            apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(TeamEliminatedGuid, $"logo", ConfigGlobal.LogosImages + "\\0.png"));
            apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(TeamEliminatedGuid, $"logo", ConfigGlobal.LogosImages + $"\\{teamId}.png"));
            SetTexts setTexts = new SetTexts();
            backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(TeamEliminatedGuid, 3, true, 10000, apiCalls));

            // Save updated data to Redis
            await redis.StringSetAsync($"{HelperRedis.isEliminated}:{teamId}", rank.ToString());
            _logger.LogInformation($"Team information successfully saved to Redis.");
            await redis.StringSetAsync($"{HelperRedis.isEliminated}:rank", (int.Parse(currentrank) - 1).ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in IsEliminatedAsync: {Message}", ex.Message);
        }
    }

    public class LiveTeamInfo
    {
        public string TeamName { get; set; }
        public int TeamId { get; set; }
        public bool IsEliminated { get; set; }
    }
}