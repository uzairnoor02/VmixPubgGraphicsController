using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmixData.Models;

namespace Pubg_Ranking_System
{
    public class TournamentDataBackupService
    {
        private readonly vmix_graphicsContext _context;
        private readonly ILogger<TournamentDataBackupService> _logger;
        private readonly string _backupPath;

        public TournamentDataBackupService(vmix_graphicsContext context, ILogger<TournamentDataBackupService> logger)
        {
            _context = context;
            _logger = logger;
            _backupPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PubgBackups",
                "TournamentBackups",
                DateTime.Now.ToString("yyyy-MM-dd")
            );
        }

        public async Task<string> CreateFullDatabaseBackupAsync(int? tournamentId = null, int? stageId = null)
        {
            string fileName = "";
            Directory.CreateDirectory(_backupPath);
            if (stageId != null && tournamentId != null)
            {

                var tournament = _context.Tournaments.Where(x => x.TournamentId == tournamentId).FirstOrDefault();
                var stage = _context.Stages.FirstOrDefault(x => x.StageId == stageId);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                fileName = tournamentId.HasValue
                   ? $"Tournament-{tournament.Name}_Stage-{stage.Name}_Backup-{timestamp}.sql"
                   : $"FullDatabase_Backup_{timestamp}.sql";
            }
            else
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                fileName = tournamentId.HasValue
                   ? $"Tournament_{tournamentId}_Backup_{timestamp}.sql"
                   : $"FullDatabase_Backup_{timestamp}.sql";
            }

            string fullPath = Path.Combine(_backupPath, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("-- =====================================================");
            sb.AppendLine("-- PUBG Tournament Database Backup");
            sb.AppendLine($"-- Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"-- Tournament ID: {tournamentId?.ToString() ?? "ALL"}");
            sb.AppendLine($"-- Stage ID: {stageId?.ToString() ?? "ALL"}");
            sb.AppendLine("-- =====================================================");
            sb.AppendLine();

            try
            {
                // 1. Backup Tournaments
                await BackupTournamentsAsync(sb, tournamentId);

                // 2. Backup Stages
                await BackupStagesAsync(sb, tournamentId, stageId);

                // 3. Backup Teams
                await BackupTeamsAsync(sb, stageId);

                // 4. Backup Players
                await BackupPlayersAsync(sb);

                // 5. Backup Matches
                await BackupMatchesAsync(sb, tournamentId, stageId);

                // 6. Backup Player Stats
                await BackupPlayerStatsAsync(sb, stageId);

                // 7. Backup Team Points
                await BackupTeamPointsAsync(sb, stageId);



                // 9. Backup Auth Keys (optional)
                await BackupAuthKeysAsync(sb);

                // 10. Backup Teams_Stages relationship
                await BackupTeamsStagesAsync(sb, stageId);

                // Write to file
                await File.WriteAllTextAsync(fullPath, sb.ToString());
                _logger.LogInformation("Backup created: {FullPath}", fullPath);

                return fullPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating backup");
                throw;
            }
        }

        private async Task BackupTournamentsAsync(StringBuilder sb, int? tournamentId)
        {
            var query = _context.Tournaments.AsQueryable();
            if (tournamentId.HasValue)
                query = query.Where(x => x.TournamentId == tournamentId.Value);

            var tournaments = await query.ToListAsync();

            if (!tournaments.Any()) return;

            sb.AppendLine("-- =====================================================");
            sb.AppendLine($"-- TOURNAMENTS ({tournaments.Count} records)");
            sb.AppendLine("-- =====================================================");
            sb.AppendLine();

            foreach (var t in tournaments)
            {
                sb.AppendLine($@"INSERT INTO `tournaments` (`tournament_id`, `name`) 
VALUES ({t.TournamentId}, '{EscapeSql(t.Name)}')
ON DUPLICATE KEY UPDATE `name` = '{EscapeSql(t.Name)}';");
                sb.AppendLine();
            }
        }

        private async Task BackupStagesAsync(StringBuilder sb, int? tournamentId, int? stageId)
        {
            var query = _context.Stages.AsQueryable();
            if (tournamentId.HasValue)
                query = query.Where(x => x.TournamentId == tournamentId.Value);
            if (stageId.HasValue)
                query = query.Where(x => x.StageId == stageId.Value);

            var stages = await query.ToListAsync();

            if (!stages.Any()) return;

            sb.AppendLine("-- =====================================================");
            sb.AppendLine($"-- STAGES ({stages.Count} records)");
            sb.AppendLine("-- =====================================================");
            sb.AppendLine();

            foreach (var s in stages)
            {
                sb.AppendLine($@"INSERT INTO `stages` (`stage_id`, `name`, `tournament_id`) 
VALUES ({s.StageId}, '{EscapeSql(s.Name)}', {s.TournamentId})
ON DUPLICATE KEY UPDATE 
    `name` = '{EscapeSql(s.Name)}',
    `tournament_id` = {s.TournamentId};");
                sb.AppendLine();
            }
        }

        private async Task BackupTeamsAsync(StringBuilder sb, int? stageId)
        {
            var query = _context.Teams.AsQueryable();
            if (stageId.HasValue)
                query = query.Where(x => x.StageId == stageId.Value);

            var teams = await query.ToListAsync();

            if (!teams.Any()) return;

            sb.AppendLine("-- =====================================================");
            sb.AppendLine($"-- TEAMS ({teams.Count} records)");
            sb.AppendLine("-- =====================================================");
            sb.AppendLine();

            foreach (var t in teams)
            {
                sb.AppendLine($@"INSERT INTO `teams` (`id`, `team_id`, `team_name`, `tournament_id`, `stage_id`) 
VALUES ({t.Id}, '{EscapeSql(t.TeamId)}', '{EscapeSql(t.TeamName)}', {(t.TournamentId.HasValue ? t.TournamentId.Value.ToString() : "NULL")}, {t.StageId})
ON DUPLICATE KEY UPDATE 
    `team_name` = '{EscapeSql(t.TeamName)}',
    `tournament_id` = {(t.TournamentId.HasValue ? t.TournamentId.Value.ToString() : "NULL")};");
                sb.AppendLine();
            }
        }

        private async Task BackupPlayersAsync(StringBuilder sb)
        {
            var players = await _context.Players.ToListAsync();

            if (!players.Any()) return;

            sb.AppendLine("-- =====================================================");
            sb.AppendLine($"-- PLAYERS ({players.Count} records)");
            sb.AppendLine("-- =====================================================");
            sb.AppendLine();

            foreach (var p in players)
            {
                sb.AppendLine($@"INSERT INTO `players` 
(`player_uid`, `player_display_name`) 
VALUES 
('{EscapeSql(p.PlayerUid)}', '{EscapeSql(p.PlayerDisplayName)}')
ON DUPLICATE KEY UPDATE 
    `player_display_name` = '{EscapeSql(p.PlayerDisplayName)}';");
                sb.AppendLine();
            }
        }

        private async Task BackupMatchesAsync(StringBuilder sb, int? tournamentId, int? stageId)
        {
            var query = _context.Matches.AsQueryable();
            if (tournamentId.HasValue)
                query = query.Where(x => x.TournamentId == tournamentId.Value);
            if (stageId.HasValue)
                query = query.Where(x => x.StageId == stageId.Value);

            var matches = await query.ToListAsync();

            if (!matches.Any()) return;

            sb.AppendLine("-- =====================================================");
            sb.AppendLine($"-- MATCHES ({matches.Count} records)");
            sb.AppendLine("-- =====================================================");
            sb.AppendLine();

            foreach (var m in matches)
            {
                var startTime = m.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
                var endTime = m.EndTime.HasValue ? m.EndTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "NULL";

                sb.AppendLine($@"INSERT INTO `matches` 
(`id`, `match_id`, `match_name`, `tournament_id`, `match_day_id`, `start_time`, `end_time`, `stage_id`) 
VALUES 
({m.Id}, {m.MatchId}, {(m.MatchName != null ? $"'{EscapeSql(m.MatchName)}'" : "NULL")}, 
{m.TournamentId}, {m.MatchDayId}, 
'{startTime}', 
{(m.EndTime.HasValue ? $"'{endTime}'" : "NULL")}, 
{m.StageId})
ON DUPLICATE KEY UPDATE 
    `match_name` = {(m.MatchName != null ? $"'{EscapeSql(m.MatchName)}'" : "NULL")},
    `start_time` = '{startTime}',
    `end_time` = {(m.EndTime.HasValue ? $"'{endTime}'" : "NULL")};");
                sb.AppendLine();
            }
        }

        private async Task BackupPlayerStatsAsync(StringBuilder sb, int? stageId)
        {
            var query = _context.PlayerStats.AsQueryable();
            if (stageId.HasValue)
                query = query.Where(x => x.StageId == stageId.Value);

            var playerStats = await query.ToListAsync();

            if (!playerStats.Any()) return;

            sb.AppendLine("-- =====================================================");
            sb.AppendLine($"-- PLAYER_STATS ({playerStats.Count} records)");
            sb.AppendLine("-- =====================================================");
            sb.AppendLine();

            foreach (var ps in playerStats)
            {
                var isFiring = ps.IsFiring.HasValue ? (ps.IsFiring.Value ? 1 : 0) : 0;
                var isOutsideBlue = ps.IsOutsideBlueCircle.HasValue ? (ps.IsOutsideBlueCircle.Value ? 1 : 0) : 0;
                var showPicUrl = ps.ShowPicUrl.HasValue ? (ps.ShowPicUrl.Value ? 1 : 0) : 0;

                sb.AppendLine($@"INSERT INTO `player_stats` 
(`match_id`, `player_uID`, `player_open_id`, `player_name`, `pic_url`, `show_pic_url`, 
`team_id`, `character`, `is_firing`, `posX`, `posY`, `posZ`, `health`, `health_max`, 
`live_state`, `kill_num`, `kill_num_before_die`, `player_key`, `got_airdrop_num`, 
`max_kill_distance`, `damage`, `in_damage`, `heal`, `headshot_num`, `kill_num_in_vehicle`, 
`survival_time`, `drive_distance`, `march_distance`, `assists`, `kill_num_by_grenade`, 
`rank`, `is_outside_blue_circle`, `outside_blue_circle_time`, `use_frag_grenade_num`, 
`use_smoke_grenade_num`, `heal_teammate_num`, `cur_weapon_id`, `knockouts`, `rescue_times`, 
`stage_id`, `day_id`, `useBurnGrenadeNum`) 
VALUES 
({ps.MatchId}, {ps.PlayerUId}, '{EscapeSql(ps.PlayerOpenId)}', 
'{EscapeSql(ps.PlayerName)}', {(ps.PicUrl != null ? $"'{EscapeSql(ps.PicUrl)}'" : "NULL")}, 
{showPicUrl}, {ps.TeamId}, 
'{EscapeSql(ps.Character)}', {isFiring}, {ps.PosX ?? 0}, {ps.PosY ?? 0}, {ps.PosZ ?? 0}, 
{ps.Health ?? 0}, {ps.HealthMax ?? 0}, {ps.LiveState ?? 0}, {ps.KillNum ?? 0}, 
{ps.KillNumBeforeDie ?? 0}, {(ps.PlayerKey.HasValue ? ps.PlayerKey.Value.ToString() : "NULL")}, {ps.GotAirdropNum ?? 0}, 
{ps.MaxKillDistance ?? 0}, {ps.Damage ?? 0}, {ps.InDamage ?? 0}, {ps.Heal ?? 0}, 
{ps.HeadshotNum ?? 0}, {ps.KillNumInVehicle ?? 0}, {ps.SurvivalTime}, {ps.DriveDistance}, 
{ps.MarchDistance}, {ps.Assists ?? 0}, {ps.KillNumByGrenade ?? 0}, {ps.Rank ?? 0}, 
{isOutsideBlue}, {ps.OutsideBlueCircleTime ?? 0}, {ps.UseFragGrenadeNum}, 
{ps.UseSmokeGrenadeNum}, {ps.HealTeammateNum ?? 0}, {ps.CurWeaponId ?? 0}, 
{ps.Knockouts ?? 0}, {ps.RescueTimes ?? 0}, {ps.StageId}, {ps.DayId}, {ps.useBurnGrenadeNum})
ON DUPLICATE KEY UPDATE 
    `kill_num` = {ps.KillNum ?? 0},
    `damage` = {ps.Damage ?? 0},
    `rank` = {ps.Rank ?? 0};");
                sb.AppendLine();
            }
        }

        private async Task BackupTeamPointsAsync(StringBuilder sb, int? stageId)
        {
            var query = _context.TeamPoints.AsQueryable();
            if (stageId.HasValue)
                query = query.Where(x => x.StageId == stageId.Value);

            var teamPoints = await query.ToListAsync();

            if (!teamPoints.Any()) return;

            sb.AppendLine("-- =====================================================");
            sb.AppendLine($"-- TEAM_POINTS ({teamPoints.Count} records)");
            sb.AppendLine("-- =====================================================");
            sb.AppendLine();

            foreach (var tp in teamPoints)
            {
                sb.AppendLine($@"INSERT INTO `team_points` 
(`team_point_id`, `match_id`, `day_id`, `stage_id`, `team_id`, `team_name`, 
`placement_points`, `kill_points`, `total_points`, `wwcd`, `Map`) 
VALUES 
({tp.TeamPointId}, {tp.MatchId}, {tp.DayId}, {tp.StageId}, {tp.TeamId}, 
{tp.TeamName}, 
{tp.PlacementPoints}, {tp.KillPoints}, {tp.TotalPoints}, 
{tp.WWCD}, {(tp.Map != null ? $"'{EscapeSql(tp.Map)}'" : "NULL")})
ON DUPLICATE KEY UPDATE 
    `placement_points` = {tp.PlacementPoints},
    `kill_points` = {tp.KillPoints},
    `total_points` = {tp.TotalPoints},
    `wwcd` = {tp.WWCD};");
                sb.AppendLine();
            }
        }

        private async Task BackupAuthKeysAsync(StringBuilder sb)
        {
            var authKeys = await _context.AuthKeys.ToListAsync();

            if (!authKeys.Any()) return;

            sb.AppendLine("-- =====================================================");
            sb.AppendLine($"-- AUTH_KEYS ({authKeys.Count} records) - SENSITIVE DATA");
            sb.AppendLine("-- =====================================================");
            sb.AppendLine();

            foreach (var ak in authKeys)
            {
                sb.AppendLine($@"INSERT INTO `auth_keys` (`id`, `key_value`, `created_at`) 
VALUES ({ak.Id}, '{EscapeSql(ak.KeyValue)}', '{ak.CreatedAt:yyyy-MM-dd HH:mm:ss}')
ON DUPLICATE KEY UPDATE `key_value` = '{EscapeSql(ak.KeyValue)}';");
                sb.AppendLine();
            }
        }

        private async Task BackupTeamsStagesAsync(StringBuilder sb, int? stageId)
        {
            var query = _context.TeamsStages.AsQueryable();
            if (stageId.HasValue)
                query = query.Where(x => x.StageId == stageId.Value);

            var teamsStages = await query.ToListAsync();

            if (!teamsStages.Any()) return;

            sb.AppendLine("-- =====================================================");
            sb.AppendLine($"-- TEAMS_STAGES ({teamsStages.Count} records)");
            sb.AppendLine("-- =====================================================");
            sb.AppendLine();

            foreach (var ts in teamsStages)
            {
                sb.AppendLine($@"INSERT INTO `teams_stages` (`team_id`, `stage_id`) 
VALUES ({ts.TeamId}, {ts.StageId})
ON DUPLICATE KEY UPDATE `stage_id` = {ts.StageId};");
                sb.AppendLine();
            }
        }

        private string EscapeSql(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
            return input.Replace("'", "''").Replace("\\", "\\\\");
        }
    }
}