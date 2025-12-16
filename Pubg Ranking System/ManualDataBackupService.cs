using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmixData.Models;

namespace Pubg_Ranking_System
{
    public class ManualDataBackupService
    {
        private readonly vmix_graphicsContext _context;
        private readonly ILogger _logger;
        private readonly string _backupPath;

        public ManualDataBackupService(vmix_graphicsContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
            _backupPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PubgBackups",
                DateTime.Now.ToString("yyyy-MM-dd")
            );
        }

        public async Task<string> CreateFullMatchBackupAsync(Match match)
        {
            Directory.CreateDirectory(_backupPath);

            string timestamp = DateTime.Now.ToString("HHmmss");
            string fileName = $"Match_Backup_M{match.MatchId}_D{match.MatchDayId}_{timestamp}.sql";
            string fullPath = Path.Combine(_backupPath, fileName);

            var sb = new StringBuilder();
            sb.AppendLine($"-- Match Data Backup");
            sb.AppendLine($"-- Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"-- Match: {match.MatchId}, Day: {match.MatchDayId}, Stage: {match.StageId}");
            sb.AppendLine($"-- Tournament: {match.TournamentId}");
            sb.AppendLine();

            // Backup Player Stats
            var playerStats = await _context.PlayerStats
                .Where(x => x.MatchId == match.MatchId &&
                           x.DayId == match.MatchDayId &&
                           x.StageId == match.StageId)
                .ToListAsync();

            if (playerStats.Any())
            {
                sb.AppendLine("-- ===============================================");
                sb.AppendLine($"-- PLAYER STATS BACKUP ({playerStats.Count} records)");
                sb.AppendLine("-- ===============================================");
                sb.AppendLine();

                foreach (var player in playerStats)
                {
                    sb.AppendLine($@"INSERT INTO `player_stats` 
(`match_id`, `player_uID`, `player_name`, `team_id`, `kill_num`, `damage`, `survival_time`, 
 `rank`, `assists`, `knockouts`, `stage_id`, `day_id`, `health`, `live_state`) 
VALUES 
({player.MatchId}, '{player.PlayerUId}', '{EscapeSql(player.PlayerName)}', '{player.TeamId}', 
 {player.KillNum}, {player.Damage}, {player.SurvivalTime}, {player.Rank}, {player.Assists}, 
 {player.Knockouts}, {player.StageId}, {player.DayId}, {player.Health}, {player.LiveState});");
                    sb.AppendLine();
                }
            }

            // Backup Team Points
            var teamPoints = await _context.TeamPoints
                .Where(x => x.MatchId == match.MatchId &&
                           x.DayId == match.MatchDayId &&
                           x.StageId == match.StageId)
                .ToListAsync();

            if (teamPoints.Any())
            {
                sb.AppendLine("-- ===============================================");
                sb.AppendLine($"-- TEAM POINTS BACKUP ({teamPoints.Count} records)");
                sb.AppendLine("-- ===============================================");
                sb.AppendLine();

                foreach (var team in teamPoints)
                {
                    sb.AppendLine($@"INSERT INTO `team_points` 
(`match_id`, `team_id`, `kill_points`, `placement_points`, `total_points`, 
 `wwcd`, `stage_id`, `day_id`, `Map`) 
VALUES 
({team.MatchId}, '{team.TeamId}', {team.KillPoints}, {team.PlacementPoints}, 
 {team.TotalPoints}, {team.WWCD}, {team.StageId}, {team.DayId}, '{EscapeSql(team.Map)}');");
                    sb.AppendLine();
                }
            }

            await File.WriteAllTextAsync(fullPath, sb.ToString());
            _logger.LogInformation("Backup created: {FullPath}", fullPath);

            return fullPath;
        }

        private string EscapeSql(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
            return input.Replace("'", "''").Replace("\\", "\\\\");
        }
    }
}