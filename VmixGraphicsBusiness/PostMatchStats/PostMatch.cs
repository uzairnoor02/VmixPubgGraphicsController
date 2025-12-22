using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text;
using System.Threading.Tasks;
using VmixData.Models;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    public partial class PostMatch(vmix_graphicsContext _vmix_GraphicsContext, IConfiguration configuration,ILogger<PostMatch> logger,IServiceProvider _serviceProvider)
    {
        string logos = configuration["LogosImages"];
        private readonly string _sqlBackupPath = configuration["SqlBackupPath"] ?? "sql_backup";

        public async Task createPostMtachStats(LivePlayersList livePlayersList, Match match, TeamInfoList teamInfoList)
        {
            using var scope = _serviceProvider.CreateScope();

            var backgroundJobClient = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();

            //backgroundJobClient.Enqueue(() =>savePlayersinfo(livePlayersList, match));
            //backgroundJobClient.Enqueue(() =>saveTeamsinfo(teamInfoList, match, livePlayersList));
            await savePlayersinfo(livePlayersList, match);
            await Task.Delay(1000);
            await saveTeamsinfo(teamInfoList, match, livePlayersList);

            await SaveMvpInfo(match);
            await WWCDStatsAsync(match);
            await MatchMvp(match);
            await MatchRankings(match);
            await OverallRankings(match);
            await TeamsToWatch(match);
        }

        public async Task savePlayersinfo(LivePlayersList liveplayerslist, Match match)
        {
            try
            {
                // Create separate collections for add and update operations
                var playersToAdd = new List<PlayerStat>();
                var playersToUpdate = new List<PlayerStat>();

                // Get existing player UIDs for this match
                var existingPlayerUIDs = await _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.MatchId == match.MatchId &&
                               x.DayId == match.MatchDayId &&
                               x.StageId == match.StageId)
                    .Select(x => x.PlayerUId)
                    .ToListAsync();

                foreach (var player in liveplayerslist.PlayerInfoList)
                {
                    if (!existingPlayerUIDs.Contains(player.UId))
                    {
                        // Create new player stat
                        var playerStat = new PlayerStat()
                        {
                            Assists = player.Assists,
                            Character = player.Character,
                            Damage = player.Damage,
                            DriveDistance = player.DriveDistance,
                            GotAirdropNum = player.GotAirDropNum,
                            HeadshotNum = player.HeadShotNum,
                            Heal = player.Heal,
                            HealTeammateNum = player.RescueTimes,
                            Health = player.Health,
                            HealthMax = player.HealthMax,
                            RescueTimes = player.RescueTimes,
                            InDamage = player.InDamage,
                            IsFiring = player.IsFiring,
                            IsOutsideBlueCircle = player.IsOutsideBlueCircle,
                            KillNum = player.KillNum,
                            KillNumBeforeDie = player.KillNumBeforeDie,
                            KillNumByGrenade = player.KillNumByGrenade,
                            KillNumInVehicle = player.KillNumInVehicle,
                            Knockouts = player.Knockouts,
                            LiveState = player.LiveState,
                            MarchDistance = player.MarchDistance,
                            MaxKillDistance = player.MaxKillDistance,
                            PicUrl = player.PicUrl,
                            PlayerKey = player.PlayerKey,
                            PlayerName = player.PlayerName,
                            PlayerOpenId = player.PlayerOpenId,
                            PosX = player.Location.X,
                            PosY = player.Location.Y,
                            PosZ = player.Location.Z,
                            Rank = player.Rank,
                            SurvivalTime = player.SurvivalTime,
                            UseFragGrenadeNum = player.UseFragGrenadeNum,
                            TeamId = player.TeamId,
                            PlayerUId = player.UId,
                            UseSmokeGrenadeNum = player.UseSmokeGrenadeNum,
                            ShowPicUrl = player.ShowPicUrl,
                            MatchId = match.MatchId,
                            StageId = match.StageId,
                            DayId = match.MatchDayId,
                            useBurnGrenadeNum = player.UseBurnGrenadeNum,
                        };
                        playersToAdd.Add(playerStat);
                    }
                }

                // Process adds first
                if (playersToAdd.Count > 0)
                {
                    try
                    {
                        await _vmix_GraphicsContext.PlayerStats.AddRangeAsync(playersToAdd);
                        await _vmix_GraphicsContext.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to save new player stats to database, creating SQL backup");
                        await CreatePlayerStatsBackupSql(playersToAdd, $"PlayerStats_Insert_{match.MatchId}_{DateTime.Now:yyyyMMdd_HHmmss}.sql");
                    }
                }

                // Process updates separately to avoid tracking conflicts
                foreach (var player in liveplayerslist.PlayerInfoList)
                {
                    if (existingPlayerUIDs.Contains(player.UId))
                    {
                        try
                        {
                            // Use ExecuteUpdateAsync for bulk updates without tracking
                            await _vmix_GraphicsContext.PlayerStats
                                .Where(x => x.PlayerUId == player.UId &&
                                           x.MatchId == match.MatchId &&
                                           x.DayId == match.MatchDayId &&
                                           x.StageId == match.StageId)
                                .ExecuteUpdateAsync(setters => setters
                                    .SetProperty(p => p.Assists, player.Assists)
                                    .SetProperty(p => p.Character, player.Character)
                                    .SetProperty(p => p.Damage, player.Damage)
                                    .SetProperty(p => p.DriveDistance, player.DriveDistance)
                                    .SetProperty(p => p.GotAirdropNum, player.GotAirDropNum)
                                    .SetProperty(p => p.HeadshotNum, player.HeadShotNum)
                                    .SetProperty(p => p.Heal, player.Heal)
                                    .SetProperty(p => p.HealTeammateNum, player.RescueTimes)
                                    .SetProperty(p => p.Health, player.Health)
                                    .SetProperty(p => p.HealthMax, player.HealthMax)
                                    .SetProperty(p => p.RescueTimes, player.RescueTimes)
                                    .SetProperty(p => p.InDamage, player.InDamage)
                                    .SetProperty(p => p.IsFiring, player.IsFiring)
                                    .SetProperty(p => p.IsOutsideBlueCircle, player.IsOutsideBlueCircle)
                                    .SetProperty(p => p.KillNum, player.KillNum)
                                    .SetProperty(p => p.KillNumBeforeDie, player.KillNumBeforeDie)
                                    .SetProperty(p => p.KillNumByGrenade, player.KillNumByGrenade)
                                    .SetProperty(p => p.KillNumInVehicle, player.KillNumInVehicle)
                                    .SetProperty(p => p.Knockouts, player.Knockouts)
                                    .SetProperty(p => p.LiveState, player.LiveState)
                                    .SetProperty(p => p.MarchDistance, player.MarchDistance)
                                    .SetProperty(p => p.MaxKillDistance, player.MaxKillDistance)
                                    .SetProperty(p => p.PicUrl, player.PicUrl)
                                    .SetProperty(p => p.PlayerKey, player.PlayerKey)
                                    .SetProperty(p => p.PlayerName, player.PlayerName)
                                    .SetProperty(p => p.PlayerOpenId, player.PlayerOpenId)
                                    .SetProperty(p => p.PosX, player.Location.X)
                                    .SetProperty(p => p.PosY, player.Location.Y)
                                    .SetProperty(p => p.PosZ, player.Location.Z)
                                    .SetProperty(p => p.Rank, player.Rank)
                                    .SetProperty(p => p.SurvivalTime, player.SurvivalTime)
                                    .SetProperty(p => p.UseFragGrenadeNum, player.UseFragGrenadeNum)
                                    .SetProperty(p => p.TeamId, player.TeamId)
                                    .SetProperty(p => p.UseSmokeGrenadeNum, player.UseSmokeGrenadeNum)
                                    .SetProperty(p => p.ShowPicUrl, player.ShowPicUrl)
                                    .SetProperty(p => p.useBurnGrenadeNum, player.UseBurnGrenadeNum));
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to update player stats for PlayerUID: {PlayerUID}", player.UId);
                            // Create backup SQL for update statement
                            await CreatePlayerStatsUpdateBackupSql(player, match, $"PlayerStats_Update_{player.UId}_{DateTime.Now:yyyyMMdd_HHmmss}.sql");
                        }
                        finally
                        {
                            await CreatePlayerStatsUpdateBackupSql(player, match, $"PlayerStats_Update_{player.UId}_{DateTime.Now:yyyyMMdd_HHmmss}.sql");

                        }
                    }
                }

                logger.LogInformation($"Successfully processed {playersToAdd.Count} new player records and {existingPlayerUIDs.Count - playersToAdd.Count} updated player records for Match {match.MatchId}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in savePlayersinfo: {Message}", ex.Message);
            }
        }

        public async Task saveTeamsinfo(TeamInfoList TeamsinfoList, Match match, LivePlayersList liveplayerslist)
        {
            string Map = "Erangel";
            try
            {
                // Get existing team IDs for this match
                var existingTeamIds = await _vmix_GraphicsContext.TeamPoints
                    .Where(x => x.MatchId == match.MatchId &&
                               x.DayId == match.MatchDayId &&
                               x.StageId == match.StageId)
                    .Select(x => x.TeamId)
                    .ToListAsync();

                var teamPointsToAdd = new List<TeamPoint>();

                foreach (var team in TeamsinfoList.teamInfoList)
                {
                    var wwcd = liveplayerslist.PlayerInfoList
                        .Where(x => x.TeamId == team.teamId)
                        .Any(x => x.Rank == 1);

                    int placementpoints = 0;
                    var rank = liveplayerslist.PlayerInfoList
                        .Where(x => x.TeamId == team.teamId)
                        .Select(x => x.Rank)
                        .FirstOrDefault();

                    switch (rank)
                    {
                        case 1:
                            placementpoints = 10;
                            break;
                        case 2:
                            placementpoints = 6;
                            break;
                        case 3:
                            placementpoints = 5;
                            break;
                        case 4:
                            placementpoints = 4;
                            break;
                        case 5:
                            placementpoints = 3;
                            break;
                        case 6:
                            placementpoints = 2;
                            break;
                        case 7:
                        case 8:
                            placementpoints = 1;
                            break;
                        default:
                            placementpoints = 0;
                            break;
                    }

                    switch (match.MatchId)
                    {
                        case 1:
                        case 5:
                            Map = "Erangel";
                            break;
                        case 2:
                        case 4:
                            Map = "Miramar";
                            break;
                        case 3:
                            Map = "Sanhok";
                            break;
                    }

                    if (!existingTeamIds.Contains(team.teamId))
                    {
                        // Create new record
                        var teamPoint = new TeamPoint()
                        {
                            DayId = match.MatchDayId,
                            KillPoints = team.killNum,
                            MatchId = match.MatchId,
                            StageId = match.StageId,
                            TeamId = team.teamId,
                            WWCD = wwcd ? 1 : 0,
                            Map = Map,
                            PlacementPoints = placementpoints,
                            TotalPoints = placementpoints + team.killNum
                        };
                        teamPointsToAdd.Add(teamPoint);
                    }
                }

                // Add new team points in bulk
                if (teamPointsToAdd.Count > 0)
                {
                    try
                    {
                        await _vmix_GraphicsContext.TeamPoints.AddRangeAsync(teamPointsToAdd);
                        await _vmix_GraphicsContext.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to save new team points to database, creating SQL backup");
                        await CreateTeamPointsBackupSql(teamPointsToAdd, $"TeamPoints_Insert_Match_{match.MatchId}_Day{match.MatchDayId}.sql");
                    }
                    finally
                    {
                        await CreateTeamPointsBackupSql(teamPointsToAdd, $"TeamPoints_Insert_Match_{match.MatchId}_Day{match.MatchDayId}.sql");

                    }
                }

                // Process updates separately using ExecuteUpdateAsync to avoid tracking conflicts
                foreach (var team in TeamsinfoList.teamInfoList)
                {
                    if (existingTeamIds.Contains(team.teamId))
                    {
                        var wwcd = liveplayerslist.PlayerInfoList
                            .Where(x => x.TeamId == team.teamId)
                            .Any(x => x.Rank == 1);

                        int placementpoints = 0;
                        var rank = liveplayerslist.PlayerInfoList
                            .Where(x => x.TeamId == team.teamId)
                            .Select(x => x.Rank)
                            .FirstOrDefault();

                        switch (rank)
                        {
                            case 1:
                                placementpoints = 10;
                                break;
                            case 2:
                                placementpoints = 6;
                                break;
                            case 3:
                                placementpoints = 5;
                                break;
                            case 4:
                                placementpoints = 4;
                                break;
                            case 5:
                                placementpoints = 3;
                                break;
                            case 6:
                                placementpoints = 2;
                                break;
                            case 7:
                            case 8:
                                placementpoints = 1;
                                break;
                            default:
                                placementpoints = 0;
                                break;
                        }

                        switch (match.MatchId)
                        {
                            case 1:
                            case 5:
                                Map = "Erangel";
                                break;
                            case 2:
                            case 4:
                                Map = "Miramar";
                                break;
                            case 3:
                                Map = "Sanhok";
                                break;
                        }

                        try
                        {
                            // Use ExecuteUpdateAsync for bulk updates without tracking
                            await _vmix_GraphicsContext.TeamPoints
                                .Where(x => x.TeamId == team.teamId &&
                                           x.MatchId == match.MatchId &&
                                           x.DayId == match.MatchDayId &&
                                           x.StageId == match.StageId)
                                .ExecuteUpdateAsync(setters => setters
                                    .SetProperty(t => t.KillPoints, team.killNum)
                                    .SetProperty(t => t.WWCD, wwcd ? 1 : 0)
                                    .SetProperty(t => t.PlacementPoints, placementpoints)
                                    .SetProperty(t => t.TotalPoints, placementpoints + team.killNum)
                                    .SetProperty(t => t.Map, Map));
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to update team points for TeamID: {TeamID}", team.teamId);
                            // Create backup SQL for update statement
                            //await CreateTeamPointsUpdateBackupSql(team, match, Map, placementpoints, wwcd, $"TeamPoints_Update_{team.teamId}_{DateTime.Now:yyyyMMdd_HHmmss}.sql");
                        }
                        finally
                        {
                            //await CreateTeamPointsUpdateBackupSql(team, match, Map, placementpoints, wwcd, $"TeamPoints_Update_{team.teamId}_{DateTime.Now:yyyyMMdd_HHmmss}.sql");
                        }
                    }
                }

                logger.LogInformation($"Successfully processed {teamPointsToAdd.Count} new team records and {existingTeamIds.Count - teamPointsToAdd.Count} updated team records for Match {match.MatchId}, Day {match.MatchDayId}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in saveTeamsinfo: {Message}", ex.Message);
                //throw; // Re-throw to let calling method handle it
            }
        }

        private async Task CreatePlayerStatsBackupSql(List<PlayerStat> playerStats, string fileName)
        {
            try
            {
                var sqlBuilder = new StringBuilder();
                sqlBuilder.AppendLine("-- Player Stats Backup SQL - Generated on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sqlBuilder.AppendLine("-- Execute this SQL script to manually insert the data if needed");
                sqlBuilder.AppendLine();

                foreach (var player in playerStats)
                {
                    var sql = $@"INSERT INTO `vmix_graphics`.`player_stats` 
(
    `match_id`, `player_uID`, `player_open_id`, `player_name`, `pic_url`, `show_pic_url`, 
    `team_id`, `character`, `is_firing`, `posX`, `posY`, `posZ`, `health`, `health_max`, 
    `live_state`, `kill_num`, `kill_num_before_die`, `player_key`, `got_airdrop_num`, 
    `max_kill_distance`, `damage`, `in_damage`, `heal`, `headshot_num`, `kill_num_in_vehicle`, 
    `survival_time`, `drive_distance`, `march_distance`, `assists`, `kill_num_by_grenade`, 
    `rank`, `is_outside_blue_circle`, `outside_blue_circle_time`, `use_frag_grenade_num`, 
    `use_smoke_grenade_num`, `heal_teammate_num`, `cur_weapon_id`, `knockouts`, `rescue_times`, 
    `stage_id`, `day_id`, `useBurnGrenadeNum`
) 
VALUES 
(
    {player.MatchId}, '{player.PlayerUId}', '{EscapeSqlString(player.PlayerOpenId)}', 
    '{EscapeSqlString(player.PlayerName)}', '{EscapeSqlString(player.PicUrl)}', '{player.ShowPicUrl}', 
    '{player.TeamId}', '{EscapeSqlString(player.Character)}', {1}, 
    {player.PosX}, {player.PosY}, {player.PosZ}, {player.Health}, {player.HealthMax}, 
    {player.LiveState}, {player.KillNum}, {player.KillNumBeforeDie}, '{player.PlayerKey}', 
    {player.GotAirdropNum}, {player.MaxKillDistance}, {player.Damage}, {player.InDamage}, 
    {player.Heal}, {player.HeadshotNum}, {player.KillNumInVehicle}, {player.SurvivalTime}, 
    {player.DriveDistance}, {player.MarchDistance}, {player.Assists}, {player.KillNumByGrenade}, 
    {player.Rank}, {1}, 0, {player.UseFragGrenadeNum}, 
    {player.UseSmokeGrenadeNum}, {player.HealTeammateNum}, 0, {player.Knockouts}, 
    {player.RescueTimes}, {player.StageId}, {player.DayId}, {player.useBurnGrenadeNum}
);";

                    sqlBuilder.AppendLine(sql);
                    sqlBuilder.AppendLine();
                }

                await WriteSqlBackupFile(fileName, sqlBuilder.ToString());
                logger.LogInformation($"Player stats backup SQL created: {fileName}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create player stats backup SQL: {Message}", ex.Message);
            }
        }

        private async Task CreatePlayerStatsUpdateBackupSql(dynamic player, Match match, string fileName)
        {
            try
            {
                var sql = $@"-- Player Stats Update Backup SQL - Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}
-- Execute this SQL script to manually update the data if needed

UPDATE `vmix_graphics`.`player_stats` 
SET 
    `assists` = {player.Assists},
    `character` = '{player.Character}',
    `damage` = {player.Damage},
    `drive_distance` = {player.DriveDistance},
    `got_airdrop_num` = {player.GotAirDropNum},
    `headshot_num` = {player.HeadShotNum},
    `heal` = {player.Heal},
    `heal_teammate_num` = {player.RescueTimes},
    `health` = {player.Health},
    `health_max` = {player.HealthMax},
    `rescue_times` = {player.RescueTimes},
    `in_damage` = {player.InDamage},
    `is_firing` = {(player.IsFiring ? 1 : 0)},
    `is_outside_blue_circle` = {(player.IsOutsideBlueCircle ? 1 : 0)},
    `kill_num` = {player.KillNum},
    `kill_num_before_die` = {player.KillNumBeforeDie},
    `kill_num_by_grenade` = {player.KillNumByGrenade},
    `kill_num_in_vehicle` = {player.KillNumInVehicle},
    `knockouts` = {player.Knockouts},
    `live_state` = {player.LiveState},
    `march_distance` = {player.MarchDistance},
    `max_kill_distance` = {player.MaxKillDistance},
    `pic_url` = '{player.PicUrl}',
    `player_key` = '{player.PlayerKey}',
    `player_name` = '{player.PlayerName}',
    `player_open_id` = '{player.PlayerOpenId}',
    `posX` = {player.Location.X},
    `posY` = {player.Location.Y},
    `posZ` = {player.Location.Z},
    `rank` = {player.Rank},
    `survival_time` = {player.SurvivalTime},
    `use_frag_grenade_num` = {player.UseFragGrenadeNum},
    `team_id` = '{player.TeamId}',
    `use_smoke_grenade_num` = {player.UseSmokeGrenadeNum},
    `show_pic_url` = '{player.ShowPicUrl}',
    `useBurnGrenadeNum` = {player.UseBurnGrenadeNum}
WHERE 
    `player_uID` = '{player.UId}' 
    AND `match_id` = {match.MatchId} 
    AND `day_id` = {match.MatchDayId} 
    AND `stage_id` = {match.StageId};";

                await WriteSqlBackupFile(fileName, sql);
                logger.LogInformation($"Player stats update backup SQL created: {fileName}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create player stats update backup SQL: {Message}", ex.Message);
            }
        }

        private async Task CreateTeamPointsBackupSql(List<TeamPoint> teamPoints, string fileName)
        {
            try
            {
                var sqlBuilder = new StringBuilder();
                sqlBuilder.AppendLine("-- Team Points Backup SQL - Generated on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sqlBuilder.AppendLine("-- Execute this SQL script to manually insert the data if needed");
                sqlBuilder.AppendLine();

                foreach (var team in teamPoints)
                {
                    var sql = $@"INSERT INTO `vmix_graphics`.`team_points` 
(
    `match_id`, `day_id`, `stage_id`, `team_id`, `team_name`, `placement_points`, 
    `kill_points`, `total_points`, `wwcd`, `Map`
) 
VALUES 
(
    {team.MatchId}, {team.DayId}, {team.StageId}, '{team.TeamId}', 
    '{"0"}', {team.PlacementPoints}, {team.KillPoints}, 
    {team.TotalPoints}, {team.WWCD}, '{team.Map}'
);";

                    sqlBuilder.AppendLine(sql);
                    sqlBuilder.AppendLine();
                }

                await WriteSqlBackupFile(fileName, sqlBuilder.ToString());
                logger.LogInformation($"Team points backup SQL created: {fileName}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create team points backup SQL: {Message}", ex.Message);
            }
        }

        private async Task CreateTeamPointsUpdateBackupSql(dynamic team, Match match, string map, int placementPoints, bool wwcd, string fileName)
        {
            try
            {
                var sql = $@"-- Team Points Update Backup SQL - Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}
-- Execute this SQL script to manually update the data if needed

UPDATE `vmix_graphics`.`team_points` 
SET 
    `kill_points` = {team.killNum},
    `wwcd` = {(wwcd ? 1 : 0)},
    `placement_points` = {placementPoints},
    `total_points` = {placementPoints + team.killNum},
    `Map` = '{EscapeSqlString(map)}'
WHERE 
    `team_id` = '{EscapeSqlString(team.teamId)}' 
    AND `match_id` = {match.MatchId} 
    AND `day_id` = {match.MatchDayId} 
    AND `stage_id` = {match.StageId};";

                await WriteSqlBackupFile(fileName, sql);
                logger.LogInformation($"Team points update backup SQL created: {fileName}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create team points update backup SQL: {Message}", ex.Message);
            }
        }

        private async Task WriteSqlBackupFile(string fileName, string content)
        {
            try
            {
                // Ensure backup directory exists
                if (!Directory.Exists(_sqlBackupPath))
                {
                    Directory.CreateDirectory(_sqlBackupPath);
                }

                var filePath = Path.Combine(_sqlBackupPath, fileName);
                await File.WriteAllTextAsync(filePath, content);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write SQL backup file: {FileName}", fileName);
            }
        }

        private string EscapeSqlString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            return input.Replace("'", "''").Replace("\\", "\\\\");
        }
        #region Comment region player and teams
        //public void savePlayersinfo(LivePlayersList liveplayerslist, Match match)
        //{
        //    try
        //    {
        //        var playerstats = new List<PlayerStat>();
        //        foreach (var player in liveplayerslist.PlayerInfoList)
        //        {
        //           var dbplayerdata= _vmix_GraphicsContext.PlayerStats.Where(x => x.PlayerUId == player.UId & x.MatchId == match.MatchId & x.DayId == match.MatchDayId & x.StageId == match.StageId).FirstOrDefault();

        //            if (dbplayerdata == null)
        //            {
        //                PlayerStat playerStat = new PlayerStat()
        //                {
        //                    Assists = player.Assists,
        //                    Character = player.Character,
        //                    Damage = player.Damage,
        //                    DriveDistance = player.DriveDistance,
        //                    GotAirdropNum = player.GotAirDropNum,
        //                    HeadshotNum = player.HeadShotNum,
        //                    Heal = player.Heal,
        //                    HealTeammateNum = player.RescueTimes,
        //                    Health = player.Health,
        //                    HealthMax = player.HealthMax,
        //                    RescueTimes = player.RescueTimes,
        //                    InDamage = player.InDamage,
        //                    IsFiring = player.IsFiring,
        //                    IsOutsideBlueCircle = player.IsOutsideBlueCircle,
        //                    KillNum = player.KillNum,
        //                    KillNumBeforeDie = player.KillNumBeforeDie,
        //                    KillNumByGrenade = player.KillNumByGrenade,
        //                    KillNumInVehicle = player.KillNumInVehicle,
        //                    Knockouts = player.Knockouts,
        //                    LiveState = player.LiveState,
        //                    MarchDistance = player.MarchDistance,
        //                    MaxKillDistance = player.MaxKillDistance,
        //                    PicUrl = player.PicUrl,
        //                    PlayerKey = player.PlayerKey,
        //                    PlayerName = player.PlayerName,
        //                    PlayerOpenId = player.PlayerOpenId,
        //                    PosX = player.Location.X,
        //                    PosY = player.Location.Y,
        //                    PosZ = player.Location.Z,
        //                    Rank = player.Rank,
        //                    SurvivalTime = player.SurvivalTime,
        //                    UseFragGrenadeNum = player.UseFragGrenadeNum,
        //                    TeamId = player.TeamId,
        //                    PlayerUId = player.UId,
        //                    UseSmokeGrenadeNum = player.UseSmokeGrenadeNum,
        //                    ShowPicUrl = player.ShowPicUrl,
        //                    MatchId = match.MatchId,
        //                    StageId = match.StageId,
        //                    DayId = match.MatchDayId,
        //                    useBurnGrenadeNum = player.UseBurnGrenadeNum,

        //                };
        //                playerstats.Add(playerStat);
        //            }
        //            else
        //            {
        //                dbplayerdata.Assists = player.Assists;
        //                    dbplayerdata.Character = player.Character;
        //                    dbplayerdata.Damage = player.Damage;
        //                    dbplayerdata.DriveDistance = player.DriveDistance;
        //                    dbplayerdata.GotAirdropNum = player.GotAirDropNum;
        //                    dbplayerdata.HeadshotNum = player.HeadShotNum;
        //                    dbplayerdata.Heal = player.Heal;
        //                    dbplayerdata.HealTeammateNum = player.RescueTimes;
        //                    dbplayerdata.Health = player.Health;
        //                    dbplayerdata.HealthMax = player.HealthMax;
        //                    dbplayerdata.RescueTimes = player.RescueTimes;
        //                    dbplayerdata.InDamage = player.InDamage;
        //                    dbplayerdata.IsFiring = player.IsFiring;
        //                    dbplayerdata.IsOutsideBlueCircle = player.IsOutsideBlueCircle;
        //                    dbplayerdata.KillNum = player.KillNum;
        //                    dbplayerdata.KillNumBeforeDie = player.KillNumBeforeDie;
        //                    dbplayerdata.KillNumByGrenade = player.KillNumByGrenade;
        //                    dbplayerdata.KillNumInVehicle = player.KillNumInVehicle;
        //                    dbplayerdata.Knockouts = player.Knockouts;
        //                    dbplayerdata.LiveState = player.LiveState;
        //                    dbplayerdata.MarchDistance = player.MarchDistance;
        //                    dbplayerdata.MaxKillDistance = player.MaxKillDistance;
        //                    dbplayerdata.PicUrl = player.PicUrl;
        //                    dbplayerdata.PlayerKey = player.PlayerKey;
        //                    dbplayerdata.PlayerName = player.PlayerName;
        //                    dbplayerdata.PlayerOpenId = player.PlayerOpenId;
        //                    dbplayerdata.PosX = player.Location.X;
        //                    dbplayerdata.PosY = player.Location.Y;
        //                    dbplayerdata.PosZ = player.Location.Z;
        //                    dbplayerdata.Rank = player.Rank;
        //                    dbplayerdata.SurvivalTime = player.SurvivalTime;
        //                    dbplayerdata.UseFragGrenadeNum = player.UseFragGrenadeNum;
        //                    dbplayerdata.TeamId = player.TeamId;
        //                    dbplayerdata.PlayerUId = player.UId;
        //                    dbplayerdata.UseSmokeGrenadeNum = player.UseSmokeGrenadeNum;
        //                    dbplayerdata.ShowPicUrl = player.ShowPicUrl;
        //                    dbplayerdata.MatchId = match.MatchId;
        //                    dbplayerdata.StageId = match.StageId;
        //                    dbplayerdata.DayId = match.MatchDayId;
        //                    dbplayerdata.useBurnGrenadeNum = player.UseBurnGrenadeNum;
        //            }
        //        }
        //        _vmix_GraphicsContext.PlayerStats.AddRange(playerstats);

        //        _vmix_GraphicsContext.SaveChanges();
        //    }
        //    catch (Exception ex)
        //    {

        //        logger.LogError("error in getting player stats.", ex);
        //    }
        //}

        //public void saveTeamsinfo(TeamInfoList TeamsinfoList, Match match, LivePlayersList liveplayerslist)
        //{
        //    string Map = "Erangel";
        //    try
        //    {
        //        var teamPointsToAdd = new List<TeamPoint>();
        //        var teamPointsToUpdate = new List<TeamPoint>();

        //        foreach (var team in TeamsinfoList.teamInfoList)
        //        {
        //            var dbteamdata = _vmix_GraphicsContext.TeamPoints
        //                .Where(x => x.TeamId == team.teamId &&
        //                           x.MatchId == match.MatchId &&
        //                           x.DayId == match.MatchDayId &&
        //                           x.StageId == match.StageId)
        //                .FirstOrDefault();

        //            var wwcd = liveplayerslist.PlayerInfoList.Where(x => x.TeamId == team.teamId).Any(x => x.Rank == 1);
        //            int placementpoints = 0;

        //            var rank = liveplayerslist.PlayerInfoList.Where(x => x.TeamId == team.teamId).Select(x => x.Rank).FirstOrDefault();
        //            switch (rank)
        //            {
        //                case 1:
        //                    placementpoints = 10;
        //                    break;
        //                case 2:
        //                    placementpoints = 6;
        //                    break;
        //                case 3:
        //                    placementpoints = 5;
        //                    break;
        //                case 4:
        //                    placementpoints = 4;
        //                    break;
        //                case 5:
        //                    placementpoints = 3;
        //                    break;
        //                case 6:
        //                    placementpoints = 2;
        //                    break;
        //                case 7:
        //                case 8:
        //                    placementpoints = 1;
        //                    break;
        //                default:
        //                    placementpoints = 0;
        //                    break;
        //            }

        //            switch (match.MatchId)
        //            {
        //                case 1:
        //                case 5:
        //                    Map = "Erangel";
        //                    break;
        //                case 2:
        //                case 4:
        //                    Map = "Miramar";
        //                    break;
        //                case 3:
        //                    Map = "Sanhok";
        //                    break;
        //            }

        //            if (dbteamdata != null)
        //            {
        //                // Update existing record
        //                dbteamdata.KillPoints = team.killNum;
        //                dbteamdata.WWCD = wwcd ? 1 : 0;
        //                dbteamdata.PlacementPoints = placementpoints;
        //                dbteamdata.TotalPoints = placementpoints + team.killNum;
        //                dbteamdata.Map = Map;

        //                teamPointsToUpdate.Add(dbteamdata);
        //            }
        //            else
        //            {
        //                // Create new record
        //                TeamPoint teamPoint = new TeamPoint()
        //                {
        //                    DayId = match.MatchDayId,
        //                    KillPoints = team.killNum,
        //                    MatchId = match.MatchId,
        //                    StageId = match.StageId,
        //                    TeamId = team.teamId,
        //                    WWCD = wwcd ? 1 : 0,
        //                    Map = Map,
        //                    PlacementPoints = placementpoints,
        //                    TotalPoints = placementpoints + team.killNum
        //                };

        //                teamPointsToAdd.Add(teamPoint);
        //            }
        //        }

        //        // Add new records if any
        //        if (teamPointsToAdd.Any())
        //        {
        //            _vmix_GraphicsContext.TeamPoints.AddRange(teamPointsToAdd);
        //        }

        //        // Save all changes (both updates and new records)
        //        _vmix_GraphicsContext.SaveChanges();

        //        logger.LogInformation($"Successfully processed {teamPointsToAdd.Count} new team records and {teamPointsToUpdate.Count} updated team records for Match {match.MatchId}, Day {match.MatchDayId}");
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.LogError(ex, "Error in saveTeamsinfo: {Message}", ex.Message);
        //    }
        //}

        #endregion


        public async Task<List<LiveTeamPointStats>> fetchTeamPointsAsync(Match match)
        {
            var teampoints = await _vmix_GraphicsContext.TeamPoints.Where(x => x.StageId == match.StageId).GroupBy(x => x.TeamId).AsNoTracking().ToListAsync();
            var stage = await _vmix_GraphicsContext.Stages.Where(x => x.StageId == match.StageId).AsNoTracking().FirstOrDefaultAsync();
            var Teams = await _vmix_GraphicsContext.Teams.Where(x => x.StageId == match.StageId).AsNoTracking().ToListAsync();

            List<LiveTeamPointStats> liveTeamPointStats = new List<LiveTeamPointStats>();
            foreach (var team in Teams)
            {
                liveTeamPointStats.Add(new LiveTeamPointStats()
                {
                    score = teampoints.Where(x => x.Key.ToString() == team.TeamId).Select(x => x.Sum(x => x.TotalPoints)).FirstOrDefault(),
                    teamid = int.Parse(team.TeamId),
                    teamName = team.TeamName,
                    teamImage = logos + "\\" + stage.Name + "\\" + team.TeamId.FirstOrDefault()
                });
            }
            return liveTeamPointStats;
        }
        //public async void savePlayerInfo(int stageid)
        //{
        //    var playerstats=await _vmix_GraphicsContext.PlayerStats.Where(x=>x.StageId==stageid).GroupBy(x=>x.PlayerUId).ToListAsync();
        //    List<Mvpmodel> mVPModels = new List<Mvpmodel>();
        //    foreach(var model in playerstats)
        //    {
        //        mVPModels.Add(new Mvpmodel()
        //        {
        //            PlayerUid = model.Key.ToString(),
        //            Damage = model.Sum(x => x.Damage),
        //            Eliminations = model.Sum(x => x.KillNum),
        //            Name=_vmix_GraphicsContext.Players.Where(x=>x.PlayerUid==model.Key.ToString()).Select(x=>x.PlayerDisplayName).FirstOrDefault()?? model.Select(x=>x.PlayerName).FirstOrDefault(),
        //            StageId = stageid,
        //            SurvivalTime = model.Average(x => x.SurvivalTime),
        //            TeamId = model.Select(x => x.TeamId).FirstOrDefault(),
        //            TournamentId = _vmix_GraphicsContext.Stages.Where(x => x.StageId == stageid).Select(x => x.TournamentId).FirstOrDefault(),
        //        });
        //    }


        //}
        public async Task SaveMvpInfo(Match match)
        {
            var playerStats = await _vmix_GraphicsContext.PlayerStats
                .Where(x => x.StageId == match.StageId)
                .GroupBy(x => x.PlayerUId)
                .ToListAsync();

            List<Mvpmodel> mvpModels = new List<Mvpmodel>();

            foreach (var model in playerStats)
            {
                var playerUid = model.Key.ToString();

                var mvpModel = new Mvpmodel
                {
                    PlayerUid = playerUid,
                    Damage = model.Sum(x => x.Damage),
                    Eliminations = model.Sum(x => x.KillNum),
                    Name = _vmix_GraphicsContext.Players.Where(x => x.PlayerUid == model.Key.ToString()).Select(x => x.PlayerDisplayName).FirstOrDefault() ?? model.Select(x => x.PlayerName).FirstOrDefault(),
                    StageId = match.StageId == 0 ? _vmix_GraphicsContext.Stages.Where(x => x.TournamentId == match.TournamentId).Select(x => x.StageId).FirstOrDefault() : 0,
                    SurvivalTime = model.Average(x => x.SurvivalTime),
                    TeamId = model.Select(x => x.TeamId).FirstOrDefault(),
                    TournamentId = match.TournamentId
                };

                var existingRecord = await _vmix_GraphicsContext.Mvpmodels
                    .FirstOrDefaultAsync(x => x.PlayerUid == mvpModel.PlayerUid &&
                                              x.StageId == match.StageId! &&
                                              x.TournamentId == mvpModel.TournamentId);

                if (existingRecord != null)
                {
                    existingRecord.Damage = mvpModel.Damage;
                    existingRecord.Eliminations = mvpModel.Eliminations;
                    existingRecord.Name = mvpModel.Name;
                    existingRecord.SurvivalTime = mvpModel.SurvivalTime;
                    existingRecord.TeamId = mvpModel.TeamId;
                }
                else
                {
                    await _vmix_GraphicsContext.Mvpmodels.AddAsync(mvpModel);
                }
            }

            await _vmix_GraphicsContext.SaveChangesAsync();
        }


    }
}