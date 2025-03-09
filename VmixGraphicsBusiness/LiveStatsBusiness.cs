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

namespace VmixGraphicsBusiness;
public class LiveStatsBusiness(IConfiguration config, IBackgroundJobClient backgroundJobClient, VMIXDataoperations vMIXDataoperations, vmi_layerSetOnOff _vmi_LayerSetOnOff, SetPlayerAchievements setPlayerAcheivments, IServiceProvider service, ILogger<LiveStatsBusiness> _logger, IConnectionMultiplexer redisConnection
)
{
    VmixData.Models.MatchModels.VmixData VMIXData;
    string LiverankingGuid;
    string TeamEliminatedGuid;
    static string ApplicationName = "Vmix GT titles";
    static string SpreadsheetId = "16hpBeXg_3PX_eyPEwk5pV0jPa07RgKCgKbxPvr0avpQ"; // Replace with your spreadsheet ID
    static string SheetName = "Live ranking"; // Replace with your sheet name
    static string ApiKey = "AIzaSyArCp-haDhlIEb_zeuy4vZiC9syjyG-H5I"; // Replace with your API key
    public readonly IConfiguration _config = config;
    vmi_layerSetOnOff vmi_LayerSetOnOff = _vmi_LayerSetOnOff;

    [AutomaticRetry(Attempts =0)]
    public async Task<List<TeamLiveStats>> CreateLiveStats(LivePlayersList playerInfo, TeamInfoList teamInfos, List<LiveTeamPointStats> liveTeamPointStats)
    {
        
        var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
        if (teamInfos.teamInfoList.Count() < 17)
        {
            LiverankingGuid = vmixdata.LiverankingGuid16;
        }
        else if (teamInfos.teamInfoList.Count() < 19)
        {
            LiverankingGuid = vmixdata.LiverankingGuid18;
        }
        else if (teamInfos.teamInfoList.Count() >= 19)
        {
            LiverankingGuid = vmixdata.LiverankingGuid20;
        }
        int teamcount = 1;
        List<string> apiCalls = new List<string>();
        try
        {
            string folderPath = _config["SaveToFolder"]!;
            string HeatlhImages = _config["Images"]!;
            List<TeamLiveStats> teamLiveStats = new List<TeamLiveStats>();

            var groupedByTeam = playerInfo.PlayerInfoList.ToLookup(info => info.TeamId);
            var teamID = 0;
            foreach (var teamGroup in groupedByTeam)
            {
                long teamId = teamGroup.Key;
                var currentTeamInfo = liveTeamPointStats.FirstOrDefault(x => x.teamid == teamGroup.Key);
                var teamStats = new TeamLiveStats();

                teamID = teamcount;
                teamStats.Logo = "";
                teamStats.TotalPoints = 0;

                int eliminations = 0;
                int playerCount = 0;
                bool isEliminated = false;

                if (teamInfos.teamInfoList.First(x => x.teamId == teamId).liveMemberNum == 0)
                {
                    isEliminated = true;
                    await IsEliminatedAsync(teamGroup.First().TeamName, teamID, true, teamGroup.Sum(x => x.KillNum), teamGroup.First().Rank);
                    _logger.LogInformation($"All players in Team {teamGroup.Key} are dead.");
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
                            apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{teamcount}P1", teamStats.Player1Health));
                            if (player.IsOutsideBlueCircle)
                                isinBlue = true;
                            break;
                        case 2:
                            teamStats.Player2Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                            apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{teamcount}P2", teamStats.Player2Health));
                            if (player.IsOutsideBlueCircle)
                                isinBlue = true;
                            break;
                        case 3:
                            teamStats.Player3Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                            apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{teamcount}P3", teamStats.Player3Health));
                            if (player.IsOutsideBlueCircle)
                                isinBlue = true;
                            break;
                        case 4:
                            teamStats.Player4Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                            apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{teamcount}P4", teamStats.Player4Health));
                            if (player.IsOutsideBlueCircle)
                                isinBlue = true;
                            break;
                    }

                    eliminations += player.KillNum;
                    teamStats.TotalPoints += player.KillNum;
                }
                teamStats.Eliminations = eliminations;
                teamStats.Tag = currentTeamInfo.teamName;

                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(LiverankingGuid, $"ELIMST{teamcount}", teamStats.Eliminations.ToString()));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(LiverankingGuid, $"TOTALT{teamcount}", currentTeamInfo.score.ToString()));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(LiverankingGuid, $"TAGT{teamcount}", currentTeamInfo.teamName));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"LOGOT{teamcount}", $"{currentTeamInfo.teamImage}.png"));
                if (isEliminated)
                {
                    apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"EliminatedBGT{teamcount}", HeatlhImages + "\\EliminatedBG\\Team Dead.png"));
                }
                else if (isinBlue)
                {
                    apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"EliminatedBGT{teamcount}", HeatlhImages + "\\EliminatedBG\\Team In Zone.png"));
                }
                else
                {
                    apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"EliminatedBGT{teamcount}", HeatlhImages + "\\EliminatedBG\\Team Out Zone.png"));
                }
                teamLiveStats.Add(teamStats);
                teamcount++;
            }

            // Enqueue API calls to Hangfire
            backgroundJobClient.Enqueue<ApiCallProcessor>(processor => processor.ProcessApiCalls(apiCalls));

            ExcelCreator excelCreator = new ExcelCreator();

            // Enqueue the GetAllAchievements call to Hangfire
            backgroundJobClient.Enqueue(() => setPlayerAcheivments.GetAllAchievements(playerInfo, liveTeamPointStats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
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
                healthImage = "\\Alive\\" + GetHealthImage(healthPercent); ;
                break;
            case 4:
                liveStatus = "Knocked Out";
                if (healthMax > 0)
                {
                    healthPercent = health / (float)healthMax * 100;

                }
                healthImage = "/Knocked/" + GetHealthImage(healthPercent); ;
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
        // Load service account credentials from the JSON file
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

    public async Task IsEliminatedAsync(string teamName, int teamId, bool isEliminated, int totalEliminations, int rank)
    {
        TeamInfo team;
        var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
        await vmi_LayerSetOnOff.PushAnimationAsync(TeamEliminatedGuid, 1, false, 100);
        List<string> apiCalls = new List<string>();

        try
        {

            var redis = redisConnection.GetDatabase();

            // Retrieve existing data from Redis
            string existingData = await redis.StringGetAsync($"{HelperRedis.isEliminated}:{teamId}");
            TeamInfo existingTeam = string.IsNullOrEmpty(existingData) ? new TeamInfo() : JsonSerializer.Deserialize<TeamInfo>(existingData) ?? new TeamInfo();

            if (!string.IsNullOrEmpty(existingTeam.TeamName))
            {
                _logger.LogInformation($"Team with ID {teamId} already exists in Redis.");
                existingTeam.IsEliminated = isEliminated;
            }
            else
            {
                team = new TeamInfo { TeamName = teamName, TeamId = teamId, IsEliminated = isEliminated };

                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"elims", totalEliminations.ToString()));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"teamname", "team" + teamId));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"rank", "#" + rank));
                SetTexts setTexts = new SetTexts();
                await setTexts.CallApiAsync(apiCalls);
                await vmi_LayerSetOnOff.PushAnimationAsync(TeamEliminatedGuid, 1, true, 2000);
                // Save updated data to Redis
                await redis.StringSetAsync($"{HelperRedis.isEliminated}:{teamId}", JsonSerializer.Serialize(team, new JsonSerializerOptions { WriteIndented = true }));

                _logger.LogInformation($"Team information successfully saved to Redis.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
    }


    public class TeamInfo
    {
        public string TeamName { get; set; }
        public int TeamId { get; set; }
        public bool IsEliminated { get; set; }
    }


}