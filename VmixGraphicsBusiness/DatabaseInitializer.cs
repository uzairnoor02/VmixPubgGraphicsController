
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VmixData.Models;

namespace VmixGraphicsBusiness
{
    public class DatabaseInitializer
    {
        private readonly vmix_graphicsContext _context;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(vmix_graphicsContext context, ILogger<DatabaseInitializer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                // Ensure database exists
                await _context.Database.EnsureCreatedAsync();
                _logger.LogInformation("Database ensured to exist.");

                // Create tables if they don't exist
                await EnsureTablesExistAsync();
                _logger.LogInformation("All tables ensured to exist.");

                // Load team data from JSON
                await LoadTeamDataFromJsonAsync();
                _logger.LogInformation("Team data loaded from JSON.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing database");
                throw;
            }
        }

        private async Task EnsureTablesExistAsync()
        {
            var tableCreationScripts = new Dictionary<string, string>
            {
                ["tournaments"] = @"
                    CREATE TABLE IF NOT EXISTS tournaments (
                        tournament_id INT AUTO_INCREMENT PRIMARY KEY,
                        name VARCHAR(255) NOT NULL
                    )",
                ["stages"] = @"
                    CREATE TABLE IF NOT EXISTS stages (
                        stage_id INT AUTO_INCREMENT PRIMARY KEY,
                        tournament_id INT,
                        name VARCHAR(255) NOT NULL,
                        num_days INT,
                        num_teams INT,
                        FOREIGN KEY (tournament_id) REFERENCES tournaments(tournament_id)
                    )",
                ["teams"] = @"
                    CREATE TABLE IF NOT EXISTS teams (
                        id INT AUTO_INCREMENT PRIMARY KEY,
                        team_id VARCHAR(45) NOT NULL,
                        team_name VARCHAR(45) NOT NULL,
                        tournament_id INT,
                        stage_id INT,
                        FOREIGN KEY (tournament_id) REFERENCES tournaments(tournament_id)
                    )",
                ["matches"] = @"
                    CREATE TABLE IF NOT EXISTS matches (
                        id INT AUTO_INCREMENT PRIMARY KEY,
                        match_id INT,
                        match_name VARCHAR(64),
                        tournament_id INT,
                        stage_id INT,
                        match_day_id INT,
                        start_time DATETIME,
                        end_time DATETIME
                    )",
                ["players"] = @"
                    CREATE TABLE IF NOT EXISTS players (
                        id INT AUTO_INCREMENT PRIMARY KEY,
                        player_uid VARCHAR(45),
                        player_display_name VARCHAR(45)
                    )",
                ["player_stats"] = @"
                    CREATE TABLE IF NOT EXISTS player_stats (
                        player_stat_id INT AUTO_INCREMENT PRIMARY KEY,
                        player_uID INT,
                        player_name VARCHAR(255),
                        player_open_id VARCHAR(50),
                        team_id INT,
                        match_id INT,
                        stage_id INT,
                        day_id INT,
                        health INT,
                        health_max INT,
                        live_state INT,
                        kill_num INT,
                        damage INT,
                        assists INT,
                        heal INT,
                        knockouts INT,
                        rank INT,
                        survival_time INT,
                        march_distance INT,
                        drive_distance INT,
                        max_kill_distance INT,
                        headshot_num INT,
                        kill_num_before_die INT,
                        kill_num_by_grenade INT,
                        kill_num_in_vehicle INT,
                        got_airdrop_num INT,
                        heal_teammate_num INT,
                        rescue_times INT,
                        is_firing INT,
                        is_outside_blue_circle INT,
                        outside_blue_circle_time INT,
                        use_frag_grenade_num INT,
                        useBurnGrenadeNum INT,
                        use_smoke_grenade_num INT,
                        character VARCHAR(50),
                        cur_weapon_id INT,
                        posX DOUBLE,
                        posY DOUBLE,
                        posZ DOUBLE,
                        in_damage INT,
                        pic_url VARCHAR(500),
                        show_pic_url INT,
                        player_key INT
                    )",
                ["team_points"] = @"
                    CREATE TABLE IF NOT EXISTS team_points (
                        team_point_id INT AUTO_INCREMENT PRIMARY KEY,
                        team_id INT,
                        team_name VARCHAR(255),
                        stage_id INT,
                        day_id INT,
                        match_id INT,
                        kill_points INT,
                        placement_points INT,
                        total_points INT,
                        wwcd INT,
                        Map VARCHAR(255)
                    )",
                ["teams_stages"] = @"
                    CREATE TABLE IF NOT EXISTS teams_stages (
                        team_stage_day_id INT AUTO_INCREMENT PRIMARY KEY,
                        tournament_id INT,
                        stage_id INT,
                        team_id INT,
                        team_name VARCHAR(255) NOT NULL,
                        day_number INT,
                        FOREIGN KEY (tournament_id) REFERENCES tournaments(tournament_id),
                        FOREIGN KEY (stage_id) REFERENCES stages(stage_id)
                    )",
                ["sport_team"] = @"
                    CREATE TABLE IF NOT EXISTS sport_team (
                        id INT AUTO_INCREMENT PRIMARY KEY,
                        name VARCHAR(255) NOT NULL,
                        description TEXT,
                        icon MEDIUMBLOB,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                    )",
                ["mvpmodel"] = @"
                    CREATE TABLE IF NOT EXISTS mvpmodel (
                        Name VARCHAR(100) NOT NULL,
                        PlayerUid VARCHAR(50) NOT NULL
                    )"
            };

            foreach (var script in tableCreationScripts)
            {
                try
                {
                    await _context.Database.ExecuteSqlRawAsync(script.Value);
                    _logger.LogInformation($"Table {script.Key} ensured to exist.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Could not create table {script.Key} - it may already exist.");
                }
            }
        }

        private async Task LoadTeamDataFromJsonAsync()
        {
            string jsonFilePath = Path.Combine(Directory.GetCurrentDirectory(), "teams_data.json");
            
            if (!File.Exists(jsonFilePath))
            {
                _logger.LogWarning($"Teams data JSON file not found at {jsonFilePath}");
                await CreateSampleJsonFileAsync(jsonFilePath);
                return;
            }

            try
            {
                string jsonContent = await File.ReadAllTextAsync(jsonFilePath);
                var teamsData = JsonSerializer.Deserialize<TeamsDataRoot>(jsonContent);

                if (teamsData?.Tournaments == null)
                {
                    _logger.LogWarning("No tournament data found in JSON file");
                    return;
                }

                foreach (var tournamentData in teamsData.Tournaments)
                {
                    await ProcessTournamentDataAsync(tournamentData);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Team data successfully loaded from JSON");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading team data from JSON");
            }
        }

        private async Task ProcessTournamentDataAsync(TeamData tournamentData)
        {
            // Find or create tournament
            var tournament = await _context.Tournaments
                .FirstOrDefaultAsync(t => t.Name == tournamentData.TournamentName);

            if (tournament == null)
            {
                tournament = new Tournament { Name = tournamentData.TournamentName };
                _context.Tournaments.Add(tournament);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Created tournament: {tournamentData.TournamentName}");
            }

            // Find or create stage
            var stage = await _context.Stages
                .FirstOrDefaultAsync(s => s.Name == tournamentData.StageName && s.TournamentId == tournament.TournamentId);

            if (stage == null)
            {
                stage = new Stage
                {
                    Name = tournamentData.StageName,
                    TournamentId = tournament.TournamentId,
                    NumTeams = tournamentData.Teams?.Count ?? 0,
                    NumDays = 1 // Default value
                };
                _context.Stages.Add(stage);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Created stage: {tournamentData.StageName}");
            }

            // Process teams
            if (tournamentData.Teams != null)
            {
                foreach (var teamInfo in tournamentData.Teams)
                {
                    var existingTeam = await _context.Teams
                        .FirstOrDefaultAsync(t => t.TeamId == teamInfo.TeamId && 
                                           t.TournamentId == tournament.TournamentId && 
                                           t.StageId == stage.StageId);

                    if (existingTeam == null)
                    {
                        var newTeam = new Team
                        {
                            TeamId = teamInfo.TeamId,
                            TeamName = teamInfo.TeamName,
                            TournamentId = tournament.TournamentId,
                            StageId = stage.StageId
                        };
                        _context.Teams.Add(newTeam);
                        _logger.LogInformation($"Created team: {teamInfo.TeamName} ({teamInfo.TeamId})");
                    }
                    else
                    {
                        // Update team name if it has changed
                        if (existingTeam.TeamName != teamInfo.TeamName)
                        {
                            existingTeam.TeamName = teamInfo.TeamName;
                            _logger.LogInformation($"Updated team name for {teamInfo.TeamId}: {teamInfo.TeamName}");
                        }
                    }
                }
            }
        }

        private async Task CreateSampleJsonFileAsync(string filePath)
        {
            var sampleData = new TeamsDataRoot
            {
                Tournaments = new List<TeamData>
                {
                    new TeamData
                    {
                        TournamentName = "Sample Tournament",
                        StageName = "Sample Stage",
                        Teams = new List<TeamInfo>
                        {
                            new TeamInfo { TeamId = "1", TeamName = "Team Alpha" },
                            new TeamInfo { TeamId = "2", TeamName = "Team Beta" },
                            new TeamInfo { TeamId = "3", TeamName = "Team Gamma" }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(sampleData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation($"Created sample teams data file at {filePath}");
        }
    }
}
