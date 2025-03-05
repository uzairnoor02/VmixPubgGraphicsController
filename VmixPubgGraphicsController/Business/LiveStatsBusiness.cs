using Microsoft.AspNetCore.DataProtection.KeyManagement;
using OfficeOpenXml;
using VmixPubgGraphicsController.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

using System;
using System.Collections.Generic;
using System.IO;
using Google.Apis.Util.Store;
using static Google.Apis.Sheets.v4.SheetsService;

namespace VmixPubgGraphicsController.Business;
public class LiveStatsBusiness
{
    VMIXDataoperations VMIXDataoperations;
    Models.VmixData VMIXData;
    string LiverankingGuid;
    static string ApplicationName = "Vmix GT titles";
    static string SpreadsheetId = "16hpBeXg_3PX_eyPEwk5pV0jPa07RgKCgKbxPvr0avpQ"; // Replace with your spreadsheet ID
    static string SheetName = "Live ranking"; // Replace with your sheet name
    static string ApiKey = "AIzaSyArCp-haDhlIEb_zeuy4vZiC9syjyG-H5I"; // Replace with your API key
    public readonly IConfiguration _config;
    public LiveStatsBusiness(IConfiguration config, VMIXDataoperations vMIXDataoperations)
    {
        _config = config;
        VMIXDataoperations = vMIXDataoperations;

    }
    private async Task<bool> setVMIXDataoperations()
    {
        VMIXData = await VMIXDataoperations.GetVMIXData();
        LiverankingGuid = GetlIVElInputKey(VMIXData, "Live Rankings.gtzip");
        return true;
    }
    static string GetlIVElInputKey(VmixData vmixData, string inputTitle)
    {
        // Find the input key where input title is "overall rankings"
        var overallInput = vmixData.Inputs.FirstOrDefault(input =>
            input.Title.Equals(inputTitle, StringComparison.OrdinalIgnoreCase) &&
            input.Type.Equals("GT", StringComparison.OrdinalIgnoreCase));

        return overallInput.Key;
    }
    public async Task<List<TeamLiveStats>> CreateLiveStats(PlayerInfoList playerInfo)
    {
        await setVMIXDataoperations();
        List<string> apiCalls = new List<string>();
        try
        {
            string folderPath = _config.GetValue<string>("SaveToFolder")!;
            string HeatlhImages = _config.GetValue<string>("Images")!;
            // Initialize an empty list to store team live stats
            List<TeamLiveStats> teamLiveStats = new List<TeamLiveStats>();

            // Group players by team ID
            var groupedByTeam = playerInfo.playerInfoList.GroupBy(info => info.TeamId);
            var teamID = 0;
            foreach (var teamGroup in groupedByTeam)
            {
                long teamId = teamGroup.Key;
                var teamStats = new TeamLiveStats(); // Create a new TeamLiveStats object for each team

                // Calculate team stats
                teamID = teamStats.TeamRank = teamID + 1; // Assign a default rank (can be updated later)
                teamStats.Logo = ""; // Set logo (can be populated from external source based on team ID)
                teamStats.TotalPoints = 0; // Initialize points

                int eliminations = 0;
                int playerCount = 0; // Track the number of alive players
                bool isEliminated = false;
                bool allPlayersDead = teamGroup.All(player => player.BHasDied);

                // Use teamGroup.Key to access the TeamId
                if (allPlayersDead)
                {
                    isEliminated = true;
                    // All players in the current team are dead
                    Console.WriteLine($"All players in Team {teamGroup.Key} are dead.");
                }
                else
                {
                    isEliminated = false;
                    // Some players in the current team are still alive
                    Console.WriteLine($"Some players in Team {teamGroup.Key} are still alive.");
                }
                bool isinBlue=false;
                foreach (var player in teamGroup)
                {
                    playerCount++;
                    switch (playerCount) // Update health properties based on player count (adjust as needed)
                    {
                        case 1:
                            teamStats.Player1Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                            apiCalls.Add(GetSetImageApiCall(LiverankingGuid, $"T{teamId}P1", teamStats.Player1Health));
                            if (player.IsOutsideBlueCircle)
                                isinBlue = true;
                            break;
                        case 2:
                            teamStats.Player2Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                            apiCalls.Add(GetSetImageApiCall(LiverankingGuid, $"T{teamId}P2", teamStats.Player2Health));
                            if (player.IsOutsideBlueCircle)
                                isinBlue = true;
                            break;
                        case 3:
                            teamStats.Player3Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                            apiCalls.Add(GetSetImageApiCall(LiverankingGuid, $"T{teamId}P3", teamStats.Player3Health));
                            if (player.IsOutsideBlueCircle)
                                isinBlue = true;
                            break;
                        case 4:
                            teamStats.Player4Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                            apiCalls.Add(GetSetImageApiCall(LiverankingGuid, $"T{teamId}P4", teamStats.Player4Health));
                            if (player.IsOutsideBlueCircle)
                                isinBlue = true;
                            break;
                    }

                    // Update eliminations and potentially points (adjust point logic as needed)
                    eliminations += player.KillNum;
                    teamStats.TotalPoints += player.KillNum;
                }
                teamStats.Eliminations = eliminations;
                teamStats.Tag = teamGroup.First().TeamName;

                apiCalls.Add(GetSetTextApiCall(LiverankingGuid, $"ELIMST{teamId}", teamStats.Eliminations.ToString()));
                apiCalls.Add(GetSetTextApiCall(LiverankingGuid, $"TAGT{teamId}", teamStats.Tag));
                apiCalls.Add(GetSetImageApiCall(LiverankingGuid, $"T{teamId}LOGO", teamStats.Tag));
                if (isEliminated)
                {
                    apiCalls.Add(GetSetImageApiCall(LiverankingGuid, $"T{teamId}EliminatedBG", HeatlhImages + "\\EliminatedBG\\Team Dead.png"));
                }
                else if (isinBlue)
                {
                    apiCalls.Add(GetSetImageApiCall(LiverankingGuid, $"T{teamId}EliminatedBG", HeatlhImages + "\\EliminatedBG\\Team In Zone.png"));
                }
                else
                {
                    apiCalls.Add(GetSetImageApiCall(LiverankingGuid, $"T{teamId}EliminatedBG", ""));
                }
                // Add the calculated TeamLiveStats to the list
                teamLiveStats.Add(teamStats);
            }
            SetTexts setTexts = new SetTexts();
            await setTexts.CallApiAsync(apiCalls);

            // Replace with your desired path
            ExcelCreator excelCreator = new ExcelCreator();

            // excelCreator.CreateExcel(teamLiveStats, folderPath);
            GoogleSheetsUploader googleSheetsUploader = new();
            ;
            // List<IList<object>> excelData = excelCreator.ReadExcelData(folderPath + "\\TeamLiveStats.xlsx");
            UploadToGoogleSheets(googleSheetsUploader.UploadData(teamLiveStats, SpreadsheetId, "Live ranking!A4"));
        }
        catch (Exception)
        {
            throw;
        }
        return null;
    }
    static string GetSetTextApiCall(string input, string selectedName, string value)
    {
        return $"http://127.0.0.1:8088/API/?Function=SetText&Input={input}&SelectedName={selectedName}.Text&Value={Uri.EscapeDataString(value)}";
    }

    static string GetSetImageApiCall(string input, string selectedName, string value)
    {
        return $"http://127.0.0.1:8088/API/?Function=SetImage&Input={input}&SelectedName={selectedName}.Source&Value={Uri.EscapeDataString(value)}";
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
                healthImage = "/Alive/" + GetHealthImage(healthPercent); ;
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
    static string[] Scopes = { SheetsService.Scope.Spreadsheets };


    public async void UploadToGoogleSheets(List<IList<object>> data)
    {
        // Load service account credentials from the JSON file
        ServiceAccountCredential credential;
        using (var stream = new FileStream("C:\\Users\\Bilal\\source\\repos\\client_secret.json", FileMode.Open, FileAccess.Read))
        {
            credential = GoogleCredential.FromStream(stream)
                .CreateScoped(SheetsService.Scope.Spreadsheets)
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

}