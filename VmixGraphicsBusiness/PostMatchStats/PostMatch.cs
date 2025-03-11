using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using VmixData.Models;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    public partial class PostMatch(vmix_graphicsContext _vmix_GraphicsContext, IConfiguration configuration, vmi_layerSetOnOff vmi_LayerSetOnOff, IConnectionMultiplexer _redisConnection)

    {
        string logos = configuration["LogosImages"];
        public void createPostMtachStats(LivePlayersList livePlayersList, Match match, TeamInfoList teamInfoList)
        {
            savePlayersinfo(livePlayersList, match);
            saveTeamsinfo(teamInfoList, match, livePlayersList);
            SaveMvpInfo(match);
        }
        public void savePlayersinfo(LivePlayersList liveplayerslist, Match match)
        {
            var playerstats = new List<PlayerStat>();
            foreach (var player in liveplayerslist.PlayerInfoList)
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
                    DayId=match.MatchDayId,
                    useBurnGrenadeNum = player.UseBurnGrenadeNum,

                };
                playerstats.Add(playerStat);
            }
            _vmix_GraphicsContext.PlayerStats.AddRange(playerstats);

            _vmix_GraphicsContext.SaveChanges();
        }

        public void saveTeamsinfo(TeamInfoList TeamsinfoList, Match match, LivePlayersList liveplayerslist)
        {
            var teamPoints = new List<TeamPoint>();
            foreach (var team in TeamsinfoList.teamInfoList)
            {
                int placementpoints = 0;
                TeamPoint teamPoint = new TeamPoint()
                {
                    DayId = match.MatchDayId,
                    KillPoints = team.killNum,
                    MatchId = match.MatchId,
                    StageId = match.StageId,
                    TeamId = team.teamId,
                };
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
                teamPoint.PlacementPoints = placementpoints;
                teamPoint.TotalPoints = placementpoints + teamPoint.KillPoints;

                teamPoints.Add(teamPoint);

            }
            _vmix_GraphicsContext.AddRangeAsync(teamPoints);

            _vmix_GraphicsContext.SaveChanges();
        }

        public async Task<List<LiveTeamPointStats>> fetchTeamPointsAsync(Match match)
        {
            var teampoints = await _vmix_GraphicsContext.TeamPoints.Where(x => x.StageId == match.StageId).GroupBy(x => x.TeamId).ToListAsync();
            var stage = await _vmix_GraphicsContext.Stages.Where(x => x.StageId == match.StageId).FirstOrDefaultAsync();
            var Teams = await _vmix_GraphicsContext.Teams.Where(x => x.StageId == match.StageId).ToListAsync();

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
                    StageId = match.StageId ==0? _vmix_GraphicsContext.Stages.Where(x => x.TournamentId == match.TournamentId).Select(x => x.StageId).FirstOrDefault():0,
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