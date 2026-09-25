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

namespace VmixGraphicsBusiness.LiveMatch;
public partial class LiveStatsBusiness(
        IConfiguration config,
        IServiceProvider serviceProvider,
        ILogger<LiveStatsBusiness> _logger,
        vmix_graphicsContext vmix_GraphicsContext)
{
    static string ApplicationName = "Vmix GT titles";
    static string SpreadsheetId = "16hpBeXg_3PX_eyPEwk5pV0jPa07RgKCgKbxPvr0avpQ"; // Replace with your spreadsheet ID
    static string SheetName = "Live ranking"; // Replace with your sheet name
    static string ApiKey = "AIzaSyArCp-haDhlIEb_zeuy4vZiC9syjyG-H5I"; // Replace with your API key
    public readonly IConfiguration _config = config;

    /// <summary>The exact field subset GetLiveData.FetchAndPostData used to trim a raw
    /// gettotalplayerlist response down to before calling CreateDynamicLiveStats - extracted here
    /// so the Ingest API (VmixIngestAgent pushing pcob data from a customer PC that can't be
    /// reached directly) can run the identical mapping instead of duplicating it.</summary>
    public static LivePlayersList FilterPlayerInfo(LivePlayersList livePlayerInfo)
    {
        return new LivePlayersList
        {
            PlayerInfoList = livePlayerInfo.PlayerInfoList.Select(player => new LivePlayerInfo
            {
                UId = player.UId,
                PlayerName = player.PlayerName,
                TeamId = player.TeamId,
                TeamName = player.TeamName,
                Health = player.Health,
                HealthMax = player.HealthMax,
                LiveState = player.LiveState,
                KillNum = player.KillNum,
                KillNumByGrenade = player.KillNumByGrenade,
                KillNumInVehicle = player.KillNumInVehicle,
                GotAirDropNum = player.GotAirDropNum,
                UseFragGrenadeNum = player.UseFragGrenadeNum,
                UseSmokeGrenadeNum = player.UseSmokeGrenadeNum,
                UseBurnGrenadeNum = player.UseBurnGrenadeNum,
                BHasDied = player.BHasDied,
                IsOutsideBlueCircle = player.IsOutsideBlueCircle,
                Rank = player.Rank,
                Assists = player.Assists,
                KillNumBeforeDie = player.KillNumBeforeDie,
            }).ToList()
        };
    }

    [AutomaticRetry(Attempts = 0), DisableConcurrentExecution(timeoutInSeconds: 2)]
    public async Task<List<TeamLiveStats>> CreateLiveStats(Match match, LivePlayersList playerInfo, TeamInfoList liveTeamInfos, List<LiveTeamPointStats> pastMatchStats)
    {
        using var scope = serviceProvider.CreateScope();
        var redis = scope.ServiceProvider.GetRequiredService<MatchStateStore>();

        // Computes the live standings board and returns it; GetLiveData / IngestApi hand it to
        // MatchStateStore.PublishLiveTeams, which is what the overlay renders. This method used
        // to also build ~200 vMix Title-field API calls per tick (text/image per team slot) and
        // read vMix's input list first - which is why the whole live pipeline died whenever
        // vMix wasn't running. None of that exists any more.
        try
        {
            string HeatlhImages = ConfigGlobal.Images!;
            List<TeamLiveStats> teamLiveStats = new List<TeamLiveStats>();

            foreach (var teamdata in pastMatchStats)
            {
                teamdata.totalScore = teamdata.score + liveTeamInfos.teamInfoList.Where(x => x.teamId == teamdata.teamid).Select(x => x.killNum).FirstOrDefault();
            }

            // Get overall rankings for all teams from database
            using var scope2 = serviceProvider.CreateScope();

            // Get all teams with their total points and WWCD from database
            var allTeamRanks = pastMatchStats
                .GroupBy(tp => tp.teamid)
                .Select(g => new
                {
                    TeamId = g.Key,
                    TotalPoints = g.Sum(x => x.score),
                    totalpoints2=g.Sum(x=>x.totalScore)
                    //WWCD = g.Sum(x => x.WWCD)
                })
                .OrderByDescending(x => x.totalpoints2)
                //.ThenByDescending(x => x.WWCD)
                .ToList();

            // Create a dictionary mapping TeamId to their overall database ranking
            var teamToOverallRank = allTeamRanks
                .Select((team, index) => new { TeamId = team.TeamId, OverallRank = index + 1 })
                .ToDictionary(x => x.TeamId, x => x.OverallRank);

            // Get ALL teams that are playing today (from liveTeamInfos)
            var playingTeams = liveTeamInfos.teamInfoList
                .Select(x => new
                {
                    TeamInfo = x,
                    OverallRank = teamToOverallRank.ContainsKey(x.teamId) ? teamToOverallRank[x.teamId] : (allTeamRanks.Count + 1)
                })
                .OrderBy(x => x.OverallRank)
                .ThenBy(x => x.TeamInfo.teamId)
                .ToList();
            // Create a dictionary for UI positioning (1st team gets position 1, 2nd gets position 2, etc.)
            // This ensures NO GAPS in UI positioning for display order
            var teamToUIPosition = playingTeams
                .Select((team, index) => new { TeamId = team.TeamInfo.teamId, UIPosition = index + 1 })
                .ToDictionary(x => x.TeamId, x => x.UIPosition);

            var groupedByTeam = playerInfo.PlayerInfoList.ToLookup(info => info.TeamId);

            // Process ALL teams that are playing today
            foreach (var teamData in playingTeams)
            {
                try
                {
                    int teamId = teamData.TeamInfo.teamId;
                    var teamGroup = groupedByTeam[teamId];
                    var currentTeamInfo = pastMatchStats.FirstOrDefault(x => x.teamid == teamId);

                    // Skip if no team info found
                    if (currentTeamInfo == null)
                    {
                        _logger.LogWarning($"Team {teamId} not found in pastMatchStats, skipping...");
                        continue;
                    }

                    var teamStats = new TeamLiveStats();

                    // Get the overall ranking from database (or use a default if not found)

                    // Get the UI position for this team
                    int uiPosition = teamToUIPosition[teamId];
                    int overallRank = teamToOverallRank.ContainsKey(teamId) ? teamToOverallRank[teamId] : uiPosition;

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
                            // killNum, not KillNumBeforeDie: a knock that bleeds out after the knocker
                            // died counts for the team (matches getteaminfolist's killNum).
                            await IsEliminatedAsync(currentTeamInfo.teamName, teamId, true, teamGroup.Sum(x => x.KillNum), teamGroup.FirstOrDefault()?.Rank ?? 0, liveTeamInfos.teamInfoList.Count());
                            _logger.LogInformation($"All players in Team {teamId} are dead.");
                        }
                    }
                    else
                    {
                        isEliminated = false;
                    }

                    bool isinBlue = false;

                    // Process players if they exist
                    if (teamGroup.Any())
                    {
                        foreach (var player in teamGroup)
                        {
                            playerCount++;
                            switch (playerCount)
                            {
                                case 1:
                                    teamStats.Player1Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                                    teamStats.Player1LiveState = player.LiveState;
                                    teamStats.Player1HealthPercent = HealthPercent(player.Health, player.HealthMax);
                                    if (player.IsOutsideBlueCircle)
                                        isinBlue = true;
                                    break;
                                case 2:
                                    teamStats.Player2Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                                    teamStats.Player2LiveState = player.LiveState;
                                    teamStats.Player2HealthPercent = HealthPercent(player.Health, player.HealthMax);
                                    if (player.IsOutsideBlueCircle)
                                        isinBlue = true;
                                    break;
                                case 3:
                                    teamStats.Player3Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                                    teamStats.Player3LiveState = player.LiveState;
                                    teamStats.Player3HealthPercent = HealthPercent(player.Health, player.HealthMax);
                                    if (player.IsOutsideBlueCircle)
                                        isinBlue = true;
                                    break;
                                case 4:
                                    teamStats.Player4Health = HeatlhImages + EvaluateLiveStatus(player.LiveState, player.Health, player.HealthMax).HealthImage;
                                    teamStats.Player4LiveState = player.LiveState;
                                    teamStats.Player4HealthPercent = HealthPercent(player.Health, player.HealthMax);
                                    if (player.IsOutsideBlueCircle)
                                        isinBlue = true;
                                    break;
                            }

                            eliminations += player.KillNum;
                            teamStats.TotalPoints += player.KillNum;
                        }
                    }
                    teamStats.Eliminations = eliminations;
                    teamStats.Tag = currentTeamInfo.teamName;
                    teamStats.TeamName = currentTeamInfo.teamName;

                    // Rank = display order (overall standing incl. this match's kills so far,
                    // ties broken by team id) - unique and gap-free, which is what the overlay
                    // sorts on. These three were never set before (TeamRank stayed 0, Logo "",
                    // TotalPoints only this match's kills) because the real values only went to
                    // vMix Title fields; the web board is now the only output, so they're filled.
                    teamStats.TeamRank = uiPosition;
                    teamStats.PlayerCount = playerCount;
                    teamStats.TotalPoints = currentTeamInfo.totalScore;
                    teamStats.TeamEliminated = isEliminated;
                    teamStats.Logo = MediaUrls.TeamLogo(teamId);
                    teamStats.TeamBackground = isEliminated ? "dead" : isinBlue ? "outsideZone" : "insideZone";
                    _logger.LogDebug("Team {TeamName} ({TeamId}): overall rank {OverallRank}, position {Position}, points {Points}",
                        currentTeamInfo.teamName, teamId, overallRank, uiPosition, currentTeamInfo.totalScore);

                    teamLiveStats.Add(teamStats);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing team {TeamId}: {Message}", teamData.TeamInfo.teamId, ex.Message);
                }
            }

            // Achievements run inline, in tick order, instead of as four Hangfire jobs per tick.
            // They're in-memory counter diffs - far cheaper than the queue hop - and running them
            // here means a slow tick can no longer have achievement jobs pile up and fire late.
            try
            {
                var achievements = serviceProvider.GetRequiredService<SetPlayerAchievements>();
                await achievements.GetAllAchievements(playerInfo, pastMatchStats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Achievement detection failed this tick: {Message}", ex.Message);
            }

            return teamLiveStats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateLiveStats: {Message}", ex.Message);
        }
        return null;
    }
    /// <summary>0-100 integer health percent, guarding the same healthMax==0 case
    /// EvaluateLiveStatus already guards below. Shared so TeamLiveStats' new numeric
    /// PlayerNHealthPercent fields use the exact same math as the image selection does.</summary>
    public static int HealthPercent(int health, int healthMax)
    {
        if (healthMax <= 0) return 0;
        return (int)Math.Round(health / (float)healthMax * 100);
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

    /// <summary>Fires once per team, the tick it's confirmed fully eliminated: publishes the
    /// "TEAM ELIMINATED" banner event to the overlay. (Used to fill and animate the vMix
    /// eliminated.gtzip Title, and fetched vMix's input list outside any try/catch first - so
    /// with vMix closed the banner never reached the web either.)</summary>
    public async Task IsEliminatedAsync(string teamName, int teamId, bool isEliminated, int totalEliminations, int rank, int totalTeams)
    {
        using var scope = serviceProvider.CreateScope();
        var redis = scope.ServiceProvider.GetRequiredService<MatchStateStore>();

        try
        {
            string currentrank = await redis.StringGetAsync($"{HelperRedis.isEliminated}:rank");
            if (string.IsNullOrEmpty(currentrank) || !int.TryParse(currentrank, out _))
            {
                currentrank = totalTeams.ToString();
            }

            redis.PublishTeamEliminated(new LiveTeamEliminatedEvent(teamName, teamId, totalEliminations, rank));

            await redis.StringSetAsync($"{HelperRedis.isEliminated}:{teamId}", rank.ToString());
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