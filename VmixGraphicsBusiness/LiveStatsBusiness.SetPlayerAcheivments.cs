using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Text.Json;
using VmixData.Models;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness
{
    public class SetPlayerAchievements
    {
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly vmix_graphicsContext _vmixGraphicsContext;
        private readonly IDatabase _redisDb;
        private readonly IServiceProvider _serviceProvider;

        // Constructor Injection
        public SetPlayerAchievements(
            vmix_graphicsContext vmixGraphicsContext,
            IBackgroundJobClient backgroundJobClient,
            IConnectionMultiplexer redisConnection,
            IServiceProvider serviceProvider)
        {
            _vmixGraphicsContext = vmixGraphicsContext;
            _backgroundJobClient = backgroundJobClient;
            _redisDb = redisConnection.GetDatabase(); // Initialize Redis database
            _serviceProvider = serviceProvider;
        }

        public async Task<bool> GetAllAchievements(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats)
        {
            var players = await _vmixGraphicsContext.Players.ToListAsync();

            // Enqueue Hangfire Jobs (Runs one after another)
            var jobId1 = _backgroundJobClient.Enqueue(() => VehicleEliminationsAsync(playerInfo, liveTeamPointStats, players));
            var jobId2 = _backgroundJobClient.ContinueJobWith(jobId1, () => GrenadeEliminationsAsync(playerInfo, liveTeamPointStats, players));
            var jobId3 = _backgroundJobClient.ContinueJobWith(jobId2, () => AirDropLootedAsync(playerInfo, liveTeamPointStats, players));
            var jobId4 = _backgroundJobClient.ContinueJobWith(jobId3, () => FirstBloodAsync(playerInfo, liveTeamPointStats, players));

            return true;
        }

        [AutomaticRetry(Attempts = 0)]
        public async Task VehicleEliminationsAsync(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats, List<Player> players)
        {
            var _vmiLayerSetOnOff = _serviceProvider.GetService<vmi_layerSetOnOff>();
            var vmixDataOperations = new VmixDataUtils();
            var vmixData = await vmixDataOperations.SetVMIXDataoperations();
            await _vmiLayerSetOnOff.PushAnimationAsync(vmixData.VehiclePlayerAcheivmentGuid, 1, false, 300);

            foreach (var player in playerInfo.PlayerInfoList)
            {
                if (player.KillNumInVehicle <= 0) continue;

                var redisKey = $"{Utils.HelperRedis.VehicleEliminationsKey}:{player.UId}:{player.UId}";
                var existingData = await _redisDb.StringGetAsync(redisKey);

                if (existingData.IsNullOrEmpty || JsonSerializer.Deserialize<VehicleEliminationInfo>(existingData).VehicleKills < player.KillNumInVehicle)
                {
                    var currentTeam = liveTeamPointStats.FirstOrDefault(x => x.teamid == player.TeamId);
                    var currentPlayer = players.FirstOrDefault(x => x.PlayerUid == player.UId.ToString());

                    if (currentTeam == null || currentPlayer == null) continue;

                    var eliminationInfo = new VehicleEliminationInfo
                    {
                        VehicleKills = player.KillNumInVehicle,
                        DateTime = DateTime.UtcNow,
                        PlayerId = player.UId.ToString()
                    };

                    await _redisDb.StringSetAsync(redisKey, JsonSerializer.Serialize(eliminationInfo));

                    var apiCalls = new List<string>
                    {
                        _vmiLayerSetOnOff.GetSetTextApiCall(vmixData.VehiclePlayerAcheivmentGuid, "PNAME", currentPlayer.PlayerDisplayName),
                        _vmiLayerSetOnOff.GetSetImageApiCall(vmixData.VehiclePlayerAcheivmentGuid, "LOGO", currentTeam.teamImage)
                    };

                    var setTexts = new SetTexts();
                    await setTexts.CallApiAsync(apiCalls);
                    await _vmiLayerSetOnOff.PushAnimationAsync(vmixData.VehiclePlayerAcheivmentGuid, 1, true, 2000);
                }
            }
        }

        [AutomaticRetry(Attempts = 0)]
        public async Task GrenadeEliminationsAsync(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats, List<Player> players)
        {
            if (playerInfo?.PlayerInfoList == null || !playerInfo.PlayerInfoList.Any())
                return;

            var _vmiLayerSetOnOff = _serviceProvider.GetService<vmi_layerSetOnOff>();
            var vmixDataOperations = new VmixDataUtils();
            var vmixData = await vmixDataOperations.SetVMIXDataoperations();
            await _vmiLayerSetOnOff.PushAnimationAsync(vmixData.GrenadePlayerAcheivmentGuid, 1, false, 300);

            foreach (var player in playerInfo.PlayerInfoList)
            {
                if (player.KillNumByGrenade <= 0) continue;

                var redisKey = $"{Utils.HelperRedis.GrenadeEliminationsKey}:{player.UId}";
                var existingData = await _redisDb.StringGetAsync(redisKey);

                if (existingData.IsNullOrEmpty || JsonSerializer.Deserialize<GrenadeEliminationInfo>(existingData).GrenadeKills < player.KillNumByGrenade)
                {
                    var currentTeam = liveTeamPointStats.FirstOrDefault(x => x.teamid == player.TeamId);
                    var currentPlayer = players.FirstOrDefault(x => x.PlayerUid == player.UId.ToString());

                    if (currentTeam == null || currentPlayer == null) continue;

                    var eliminationInfo = new GrenadeEliminationInfo
                    {
                        GrenadeKills = player.KillNumByGrenade,
                        DateTime = DateTime.UtcNow,
                        PlayerId = player.UId.ToString()
                    };

                    await _redisDb.StringSetAsync(redisKey, JsonSerializer.Serialize(eliminationInfo));

                    var apiCalls = new List<string>
                    {
                        _vmiLayerSetOnOff.GetSetTextApiCall(vmixData.GrenadePlayerAcheivmentGuid, "PLayerName", player.PlayerName ?? "Unknown Player"),
                        _vmiLayerSetOnOff.GetSetImageApiCall(vmixData.GrenadePlayerAcheivmentGuid, "LOGO", currentTeam.teamImage ?? string.Empty)
                    };

                    var setTexts = new SetTexts();
                    await setTexts.CallApiAsync(apiCalls);
                    await _vmiLayerSetOnOff.PushAnimationAsync(vmixData.GrenadePlayerAcheivmentGuid, 1, true, 2000);
                }
            }
        }

        [AutomaticRetry(Attempts = 0)]
        public async Task AirDropLootedAsync(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats, List<Player> players)
        {
            if (playerInfo?.PlayerInfoList == null || !playerInfo.PlayerInfoList.Any())
                return;

            var _vmiLayerSetOnOff = _serviceProvider.GetService<vmi_layerSetOnOff>();
            var vmixDataOperations = new VmixDataUtils();
            var vmixData = await vmixDataOperations.SetVMIXDataoperations();
            var airdropPlayer = playerInfo.PlayerInfoList.FirstOrDefault(p => p.GotAirDropNum > 0);
            if (airdropPlayer == null) return;

            var redisKey = $"{Utils.HelperRedis.AirDropLootedKey}:{airdropPlayer.UId}";
            var existingData = await _redisDb.StringGetAsync(redisKey);

            if (existingData.IsNullOrEmpty)
            {
                var currentTeam = liveTeamPointStats.FirstOrDefault(x => x.teamid == airdropPlayer.TeamId);
                //var currentPlayer = players.FirstOrDefault(x => x.PlayerUid == airdropPlayer.UId.ToString());

                if (currentTeam == null ) return;

                var airdropInfo = new AirDropLootedInfo
                {
                    AirdropLootedNumber = airdropPlayer.GotAirDropNum,
                    DateTime = DateTime.UtcNow,
                    PlayerId = airdropPlayer.UId.ToString()
                };

                await _redisDb.StringSetAsync(redisKey, JsonSerializer.Serialize(airdropInfo));

                var apiCalls = new List<string>
                {
                    _vmiLayerSetOnOff.GetSetTextApiCall(vmixData.AirDropPlayerAcheivmentGuid, "PNAME", airdropPlayer.PlayerName ?? "Unknown"),
                    _vmiLayerSetOnOff.GetSetImageApiCall(vmixData.AirDropPlayerAcheivmentGuid, "LOGO", currentTeam.teamImage ?? "")
                };

                var setTexts = new SetTexts();
                await setTexts.CallApiAsync(apiCalls);
                await _vmiLayerSetOnOff.PushAnimationAsync(vmixData.AirDropPlayerAcheivmentGuid, 1, true, 2000);
            }
        }

        [AutomaticRetry(Attempts = 0)]
        public async Task<bool> FirstBloodAsync(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats, List<Player> players)
        {
            if (playerInfo?.PlayerInfoList == null || !playerInfo.PlayerInfoList.Any())
                return false;

            var _vmiLayerSetOnOff = _serviceProvider.GetService<vmi_layerSetOnOff>();
            var vmixDataOperations = new VmixDataUtils();
            var vmixData = await vmixDataOperations.SetVMIXDataoperations();
            var firstBloodPlayer = playerInfo.PlayerInfoList.FirstOrDefault(p => p.KillNum > 0);
            if (firstBloodPlayer == null) return false;

            var redisKey = "FirstBlood";
            var existingData = await _redisDb.StringGetAsync(redisKey);

            if (existingData.IsNullOrEmpty)
            {
                var currentTeam = liveTeamPointStats.FirstOrDefault(x => x.teamid == firstBloodPlayer.TeamId);
                var currentPlayer = players.FirstOrDefault(x => x.PlayerUid == firstBloodPlayer.UId.ToString());

                if (currentTeam == null || currentPlayer == null) return false;

                var firstBloodInfo = new FirstBlood
                {
                    DateTime = DateTime.UtcNow,
                    PlayerId = firstBloodPlayer.UId.ToString()
                };

                await _redisDb.StringSetAsync(redisKey, JsonSerializer.Serialize(firstBloodInfo));

                var apiCalls = new List<string>
                {
                    _vmiLayerSetOnOff.GetSetTextApiCall(vmixData.FirstBloodPlayerAcheivmentGuid, "PNAME", firstBloodPlayer.PlayerName ?? "Unknown"),
                    _vmiLayerSetOnOff.GetSetImageApiCall(vmixData.FirstBloodPlayerAcheivmentGuid, "LOGO", currentTeam.teamImage ?? "")
                };

                var setTexts = new SetTexts();
                await setTexts.CallApiAsync(apiCalls);
                await _vmiLayerSetOnOff.PushAnimationAsync(vmixData.FirstBloodPlayerAcheivmentGuid, 1, true, 2000);

                return true;
            }

            return false;
        }
    }
}

