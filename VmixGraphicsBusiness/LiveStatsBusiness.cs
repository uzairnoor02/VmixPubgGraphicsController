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

namespace VmixGraphicsBusiness;
public class LiveStatsBusiness(IConfiguration config, IBackgroundJobClient backgroundJobClient, VMIXDataoperations vMIXDataoperations, vmi_layerSetOnOff _vmi_LayerSetOnOff, SetPlayerAchievements setPlayerAcheivments)
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


    public async Task<List<TeamLiveStats>> CreateLiveStats(LivePlayersList playerInfo, TeamInfoList teamInfos, List<LiveTeamPointStats> liveTeamPointStats)
    {
        var vmixdataoperations = new VmixDataUtils();
        var vmixdata = await vmixdataoperations.SetVMIXDataoperations();
        if (teamInfos.teamInfoList.Count() < 17)
        {
            LiverankingGuid = vmixdata.LiverankingGuid16;
        }
        else if (teamInfos.teamInfoList.Count() <19)
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
            // Initialize an empty list to store team live stats
            List<TeamLiveStats> teamLiveStats = new List<TeamLiveStats>();

            // Group players by team ID
            var groupedByTeam = playerInfo.PlayerInfoList.GroupBy(info => info.TeamId);
            var teamID = 0;
            groupedByTeam.OrderBy(x => x.Sum(y => y.KillNum));
            foreach (var teamGroup in groupedByTeam)
            {
                long teamId = teamGroup.Key;
                var currentTeamInfo = liveTeamPointStats.Where(x => x.teamid == teamGroup.Key).FirstOrDefault();
                var teamStats = new TeamLiveStats(); // Create a new TeamLiveStats object for each team

                // Calculate team stats
                teamID = teamcount; // Assign a default rank (can be updated later)
                teamStats.Logo = ""; // Set logo (can be populated from external source based on team ID)
                teamStats.TotalPoints = 0; // Initialize points

                int eliminations = 0;
                int playerCount = 0; // Track the number of alive players
                bool isEliminated = false;

                // Use teamGroup.Key to access the TeamId
                if (teamInfos.teamInfoList.Where(x => x.teamId == teamId).First().liveMemberNum == 0)
                {
                    isEliminated = true;
                    IsEliminatedAsync(teamGroup.First().TeamName, teamID, true, teamGroup.Select(x => x.KillNum).Sum(), teamGroup.Select(x => x.Rank).FirstOrDefault());
                    // All players in the current team are dead
                    Console.WriteLine($"All players in Team {teamGroup.Key} are dead.");
                }
                else
                {
                    isEliminated = false;
                    // Some players in the current team are still alive
                    Console.WriteLine($"Some players in Team {teamGroup.Key} are still alive.");
                }
                bool isinBlue = false;
                foreach (var player in teamGroup)
                {
                    playerCount++;
                    switch (playerCount) // Update health properties based on player count (adjust as needed)
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

                    // Update eliminations and potentially points (adjust point logic as needed)
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
                // Add the calculated TeamLiveStats to the list
                teamLiveStats.Add(teamStats);
                teamcount++;
            }
            SetTexts setTexts = new SetTexts();
            await setTexts.CallApiAsync(apiCalls);

            // Replace with your desired path
            ExcelCreator excelCreator = new ExcelCreator();

            bool firstblood = false;
            firstblood = await setPlayerAcheivments.GetAllAchievements(playerInfo, liveTeamPointStats);
        }
        catch (Exception)
        {
            throw;
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
        var vmixdataoperations = new VmixDataUtils();
        var vmixdata = await vmixdataoperations.SetVMIXDataoperations();
        await vmi_LayerSetOnOff.PushAnimationAsync(TeamEliminatedGuid, 1, false, 100);
        List<string> apiCalls = new List<string>();
        string outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "resources");

        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        string outputFilePath = Path.Combine(outputFolder, "isEliminated.json");
        try
        {
            List<TeamInfo> teams = new List<TeamInfo>();

            if (File.Exists(outputFilePath))
            {
                try
                {
                    string existingData = File.ReadAllText(outputFilePath);
                    teams = JsonSerializer.Deserialize<List<TeamInfo>>(existingData) ?? new List<TeamInfo>();
                }
                catch (JsonException)
                {
                    Console.WriteLine("Error reading JSON file. Initializing a new list.");
                    teams = new List<TeamInfo>();
                }
            }

            TeamInfo existingTeam = teams.Find(t => t.TeamId == teamId);
            if (existingTeam != null)
            {
                Console.WriteLine($"Team with ID {teamId} already exists in the file.");
                existingTeam.IsEliminated = isEliminated;
            }
            else
            {
                teams.Add(new TeamInfo { TeamName = teamName, TeamId = teamId, IsEliminated = isEliminated });
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"elims", totalEliminations.ToString()));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"teamname", "team" + teamId));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"rank", "#" + rank));
                SetTexts setTexts = new SetTexts();
                await setTexts.CallApiAsync(apiCalls);
                await vmi_LayerSetOnOff.PushAnimationAsync(TeamEliminatedGuid, 1, true, 2000);
            }

            File.WriteAllText(outputFilePath, JsonSerializer.Serialize(teams, new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine($"Team information successfully saved to {outputFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }


    public class TeamInfo
    {
        public string TeamName { get; set; }
        public int TeamId { get; set; }
        public bool IsEliminated { get; set; }
    }


}