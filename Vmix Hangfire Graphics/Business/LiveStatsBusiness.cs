using Microsoft.AspNetCore.DataProtection.KeyManagement;
using OfficeOpenXml;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

using System;
using System.Collections.Generic;
using System.IO;
using Google.Apis.Util.Store;
using static Google.Apis.Sheets.v4.SheetsService;
using Microsoft.Extensions.Configuration;
using Vmix_Hangfire_Graphics.Models;

namespace Vmix_Hangfire_Graphics.Business;
public class LiveStatsBusiness
{
    public readonly IConfiguration _config;

    public string ApplicationName { get; private set; }
    public string SpreadsheetId { get; private set; }
    public string SheetName { get; private set; }
    public string ApiKey { get; private set; }

    public LiveStatsBusiness(IConfiguration configuration)
    {
        ApplicationName = configuration["HangfireJobSettings:ApplicationName"]!;
        SpreadsheetId = configuration["HangfireJobSettings:SpreadsheetId"]!;
        SheetName = configuration["HangfireJobSettings:SheetName"]!;
        ApiKey = configuration["HangfireJobSettings:ApiKey"]!;
        _config = configuration;
    }
    public List<TeamLiveStats> CreateLiveStats(PlayerInfoList playerInfo)
    {
        try
        {
            string folderPath = _config.GetValue<string>("SaveToFolder");
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
                bool isEliminated = true;
                foreach (var player in teamGroup)
                {
                    // Update team health based on alive players
                    if (player.LiveState == 0 && !player.BHasDied) // Check if player is alive
                    {
                        playerCount++;
                        switch (playerCount) // Update health properties based on player count (adjust as needed)
                        {
                            case 1:
                                teamStats.Player1Health = folderPath + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                                break;
                            case 2:
                                teamStats.Player2Health = folderPath + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                                break;
                            case 3:
                                teamStats.Player3Health = folderPath + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                                break;
                            case 4:
                                teamStats.Player4Health = folderPath + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                                break;
                        }
                        isEliminated = false;
                    }

                    // Update eliminations and potentially points (adjust point logic as needed)
                    eliminations += player.KillNum;
                    teamStats.TotalPoints += player.KillNum;
                }

                teamStats.Eliminations = eliminations;
                teamStats.Tag = teamGroup.First().TeamName;
                // Add the calculated TeamLiveStats to the list
                teamLiveStats.Add(teamStats);
            }

            // Replace with your desired path
            ExcelCreator excelCreator = new ExcelCreator();

            excelCreator.CreateExcel(teamLiveStats, folderPath);
            List<IList<object>> excelData = excelCreator.ReadExcelData(folderPath + "\\TeamLiveStats.xlsx");
            UploadToGoogleSheets(excelData);
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
                Range = $"{SheetName}!A1",
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