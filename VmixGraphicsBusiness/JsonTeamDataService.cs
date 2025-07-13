
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VmixData.Models;

namespace VmixGraphicsBusiness
{
    public class JsonTeamDataService
    {
        private readonly vmix_graphicsContext _context;
        private readonly ILogger<JsonTeamDataService> _logger;

        public JsonTeamDataService(vmix_graphicsContext context, ILogger<JsonTeamDataService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task ReloadTeamDataAsync()
        {
            string jsonFilePath = Path.Combine(Directory.GetCurrentDirectory(), "teams_data.json");
            
            if (!File.Exists(jsonFilePath))
            {
                _logger.LogWarning($"Teams data JSON file not found at {jsonFilePath}");
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
                _logger.LogInformation("Team data successfully reloaded from JSON");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading team data from JSON");
                throw;
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
    }
}
