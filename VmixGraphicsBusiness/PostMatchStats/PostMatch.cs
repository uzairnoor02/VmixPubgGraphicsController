using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Threading.Tasks;
using VmixData.Models;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    public partial class PostMatch(vmix_graphicsContext _vmix_GraphicsContext, IConfiguration configuration,ILogger<PostMatch> logger)
    {
        string logos = configuration["LogosImages"];
        public async Task createPostMtachStats(LivePlayersList livePlayersList, Match match, TeamInfoList teamInfoList)
        {
            savePlayersinfo(livePlayersList, match);
            saveTeamsinfo(teamInfoList, match, livePlayersList);
            await SaveMvpInfo(match);
            await WWCDStatsAsync(match);
            await MatchMvp(match);
            await MatchRankings(match);
            await OverallRankings(match);
            await TeamsToWatch(match);
        }
        public void savePlayersinfo(LivePlayersList liveplayerslist, Match match)
        {
            try
            {
                var playerstats = new List<PlayerStat>();

                foreach (var player in liveplayerslist.PlayerInfoList)
                {
                    var dbplayerdata = _vmix_GraphicsContext.PlayerStats
                        .Where(x => x.PlayerUId == player.UId && x.MatchId == match.MatchId && x.DayId == match.MatchDayId && x.StageId == match.StageId)
                        .FirstOrDefault();

                    if (dbplayerdata == null)
                    {
                        PlayerStat playerStat = new PlayerStat()
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
                        playerstats.Add(playerStat);
                    }
                    else
                    {
                        // Update existing entity directly - no need to add to separate collection
                        dbplayerdata.Assists = player.Assists;
                        dbplayerdata.Character = player.Character;
                        dbplayerdata.Damage = player.Damage;
                        dbplayerdata.DriveDistance = player.DriveDistance;
                        dbplayerdata.GotAirdropNum = player.GotAirDropNum;
                        dbplayerdata.HeadshotNum = player.HeadShotNum;
                        dbplayerdata.Heal = player.Heal;
                        dbplayerdata.HealTeammateNum = player.RescueTimes;
                        dbplayerdata.Health = player.Health;
                        dbplayerdata.HealthMax = player.HealthMax;
                        dbplayerdata.RescueTimes = player.RescueTimes;
                        dbplayerdata.InDamage = player.InDamage;
                        dbplayerdata.IsFiring = player.IsFiring;
                        dbplayerdata.IsOutsideBlueCircle = player.IsOutsideBlueCircle;
                        dbplayerdata.KillNum = player.KillNum;
                        dbplayerdata.KillNumBeforeDie = player.KillNumBeforeDie;
                        dbplayerdata.KillNumByGrenade = player.KillNumByGrenade;
                        dbplayerdata.KillNumInVehicle = player.KillNumInVehicle;
                        dbplayerdata.Knockouts = player.Knockouts;
                        dbplayerdata.LiveState = player.LiveState;
                        dbplayerdata.MarchDistance = player.MarchDistance;
                        dbplayerdata.MaxKillDistance = player.MaxKillDistance;
                        dbplayerdata.PicUrl = player.PicUrl;
                        dbplayerdata.PlayerKey = player.PlayerKey;
                        dbplayerdata.PlayerName = player.PlayerName;
                        dbplayerdata.PlayerOpenId = player.PlayerOpenId;
                        dbplayerdata.PosX = player.Location.X;
                        dbplayerdata.PosY = player.Location.Y;
                        dbplayerdata.PosZ = player.Location.Z;
                        dbplayerdata.Rank = player.Rank;
                        dbplayerdata.SurvivalTime = player.SurvivalTime;
                        dbplayerdata.UseFragGrenadeNum = player.UseFragGrenadeNum;
                        dbplayerdata.TeamId = player.TeamId;
                        dbplayerdata.PlayerUId = player.UId;
                        dbplayerdata.UseSmokeGrenadeNum = player.UseSmokeGrenadeNum;
                        dbplayerdata.ShowPicUrl = player.ShowPicUrl;
                        dbplayerdata.MatchId = match.MatchId;
                        dbplayerdata.StageId = match.StageId;
                        dbplayerdata.DayId = match.MatchDayId;
                        dbplayerdata.useBurnGrenadeNum = player.UseBurnGrenadeNum;
                        
                        // Mark entity as modified
                        _vmix_GraphicsContext.Entry(dbplayerdata).State = EntityState.Modified;
                    }
                }

                // Add new records if any
                if (playerstats.Any())
                {
                    _vmix_GraphicsContext.PlayerStats.AddRange(playerstats);
                }

                _vmix_GraphicsContext.SaveChanges();
                
                logger.LogInformation($"Successfully processed {playerstats.Count} new player records for Match {match.MatchId}, Day {match.MatchDayId}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in savePlayersinfo: {Message}", ex.Message);
            }
        }

        public void saveTeamsinfo(TeamInfoList TeamsinfoList, Match match, LivePlayersList liveplayerslist)
        {
            string Map = "Erangel";
            try
            {
                var teamPointsToAdd = new List<TeamPoint>();
                var teamPointsToUpdate = new List<TeamPoint>();

                foreach (var team in TeamsinfoList.teamInfoList)
                {
                    var dbteamdata = _vmix_GraphicsContext.TeamPoints
                        .Where(x => x.TeamId == team.teamId &&
                                   x.MatchId == match.MatchId &&
                                   x.DayId == match.MatchDayId &&
                                   x.StageId == match.StageId)
                        .FirstOrDefault();

                    var wwcd = liveplayerslist.PlayerInfoList.Where(x => x.TeamId == team.teamId).Any(x => x.Rank == 1);
                    int placementpoints = 0;

                    var rank = liveplayerslist.PlayerInfoList.Where(x => x.TeamId == team.teamId).Select(x => x.Rank).FirstOrDefault();
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

                    if (dbteamdata != null)
                    {
                        // Update existing record
                        dbteamdata.KillPoints = team.killNum;
                        dbteamdata.WWCD = wwcd ? 1 : 0;
                        dbteamdata.PlacementPoints = placementpoints;
                        dbteamdata.TotalPoints = placementpoints + team.killNum;
                        dbteamdata.Map = Map;

                        teamPointsToUpdate.Add(dbteamdata);
                    }
                    else
                    {
                        // Create new record
                        TeamPoint teamPoint = new TeamPoint()
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

                // Add new records if any
                if (teamPointsToAdd.Any())
                {
                    _vmix_GraphicsContext.TeamPoints.AddRange(teamPointsToAdd);
                }

                // Save all changes (both updates and new records)
                _vmix_GraphicsContext.SaveChanges();

                logger.LogInformation($"Successfully processed {teamPointsToAdd.Count} new team records and {teamPointsToUpdate.Count} updated team records for Match {match.MatchId}, Day {match.MatchDayId}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in saveTeamsinfo: {Message}", ex.Message);
            }
        }
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