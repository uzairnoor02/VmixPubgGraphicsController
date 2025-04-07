using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;
using System.Text.Json;
using VmixData.Models;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness.Utils;
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness.LiveMatch
{
    public class SetPlayerAchievements
    {
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IServiceProvider _serviceProvider;

        // Constructor Injection
        public SetPlayerAchievements(
            IBackgroundJobClient backgroundJobClient,
            IServiceProvider serviceProvider)
        {
            _backgroundJobClient = backgroundJobClient; // Initialize Redis database
            _serviceProvider = serviceProvider;
        }
        [AutomaticRetry(Attempts = 0)]
        public async Task<bool> GetAllAchievements(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats)
        {

            // Enqueue Hangfire Jobs (Runs one after another)
            // var jobId1 = _backgroundJobClient.Enqueue(HangfireQueues.LowPriority, () => FirstBloodAsync(playerInfo, liveTeamPointStats));
            var jobId2 = _backgroundJobClient.Enqueue(HangfireQueues.LowPriority, () => GrenadeEliminationsAsync(playerInfo, liveTeamPointStats));
            var jobId3 = _backgroundJobClient.Enqueue(HangfireQueues.LowPriority, () => AirDropLootedAsync(playerInfo, liveTeamPointStats));
            var jobId4 = _backgroundJobClient.Enqueue(HangfireQueues.LowPriority, () => VehicleEliminationsAsync(playerInfo, liveTeamPointStats));

            return true;
        }

        [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 1, 1 })]
        public async Task VehicleEliminationsAsync(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats)
        {
            using var scope = _serviceProvider.CreateScope();
            var backgroundJobClient = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            var connectionMultiplexer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var _redisDb = connectionMultiplexer.GetDatabase();
            var vmixData = await VmixDataUtils.SetVMIXDataoperations();
            // backgroundJobClient.Enqueue(()=> vmi_layerSetOnOff.PushAnimationAsync(vmixData.VehiclePlayerAcheivmentGuid, 3, false, 300));

            foreach (var player in playerInfo.PlayerInfoList)
            {
                if (player.KillNumInVehicle <= 0) continue;

                var redisKey = $"{Utils.HelperRedis.VehicleEliminationsKey}:{player.UId}";
                var existingData = await _redisDb.StringGetAsync(redisKey);
                var vehicleKills = JsonSerializer.Deserialize<VehicleEliminationInfo>(existingData);

                if (vehicleKills.VehicleKills < player.KillNumInVehicle)
                {
                    var currentTeam = liveTeamPointStats.FirstOrDefault(x => x.teamid == player.TeamId);
                    //var currentPlayer = players.FirstOrDefault(x => x.PlayerUid == player.UId.ToString());

                    if (currentTeam == null) continue;

                    var eliminationInfo = new VehicleEliminationInfo
                    {
                        VehicleKills = player.KillNumInVehicle,
                        DateTime = DateTime.UtcNow,
                        PlayerId = player.UId.ToString()
                    };

                    await _redisDb.StringSetAsync(redisKey, JsonSerializer.Serialize(eliminationInfo));

                    var apiCalls = new List<string>
                    {
                        vmi_layerSetOnOff.GetSetTextApiCall(vmixData.VehiclePlayerAcheivmentGuid, "PNAME", player.PlayerName),
                        vmi_layerSetOnOff.GetSetImageApiCall(vmixData.VehiclePlayerAcheivmentGuid, "TLOGO", $"{ConfigGlobal.LogosImages}\\{currentTeam.teamid}.png"),
                         vmi_layerSetOnOff.GetSetImageApiCall(vmixData.VehiclePlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\0.png"),
                         vmi_layerSetOnOff.GetSetImageApiCall(vmixData.VehiclePlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\{player.UId}.png")
                    };
                    backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(vmixData.VehiclePlayerAcheivmentGuid, 3, true, 4000, apiCalls));

                }
            }
        }

        [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 1, 1 })]
        public async Task GrenadeEliminationsAsync(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats)
        {
            using var scope = _serviceProvider.CreateScope();

            var backgroundJobClient = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            var connectionMultiplexer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var _redisDb = connectionMultiplexer.GetDatabase();
            if (playerInfo?.PlayerInfoList == null || !playerInfo.PlayerInfoList.Any())
                return;

            var vmixData = await VmixDataUtils.SetVMIXDataoperations();
            //backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(vmixData.GrenadePlayerAcheivmentGuid, 3, false, 300));

            foreach (var player in playerInfo.PlayerInfoList)
            {
                if (player.KillNumByGrenade <= 0) continue;

                var redisKey = $"{Utils.HelperRedis.GrenadeEliminationsKey}:{player.UId}";
                var existingData = await _redisDb.StringGetAsync(redisKey);

                if (existingData.IsNullOrEmpty || JsonSerializer.Deserialize<GrenadeEliminationInfo>(existingData).GrenadeKills < player.KillNumByGrenade)
                {
                    var currentTeam = liveTeamPointStats.FirstOrDefault(x => x.teamid == player.TeamId);

                    if (currentTeam == null) continue;

                    var eliminationInfo = new GrenadeEliminationInfo
                    {
                        GrenadeKills = player.KillNumByGrenade,
                        DateTime = DateTime.UtcNow,
                        PlayerId = player.UId.ToString()
                    };

                    await _redisDb.StringSetAsync(redisKey, JsonSerializer.Serialize(eliminationInfo));

                    var apiCalls = new List<string>
                    {
                        vmi_layerSetOnOff.GetSetTextApiCall(vmixData.GrenadePlayerAcheivmentGuid, "PNAME", player.PlayerName ?? "Unknown Player"),
                        vmi_layerSetOnOff.GetSetImageApiCall(vmixData.GrenadePlayerAcheivmentGuid, "LOGO", $"{ConfigGlobal.LogosImages}\\{currentTeam.teamid}.png"),
                         vmi_layerSetOnOff.GetSetImageApiCall(vmixData.GrenadePlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\0.png"),
                         vmi_layerSetOnOff.GetSetImageApiCall(vmixData.GrenadePlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\{player.UId}.png")
                    };

                    backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(vmixData.GrenadePlayerAcheivmentGuid, 3, true, 4000, apiCalls));
                }
            }
        }

        [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 1, 1 })]
        public async Task AirDropLootedAsync(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats)
        {
            using var scope = _serviceProvider.CreateScope();

            var backgroundJobClient = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            var connectionMultiplexer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var _redisDb = connectionMultiplexer.GetDatabase();
            if (playerInfo?.PlayerInfoList == null || !playerInfo.PlayerInfoList.Any())
                return;

            var vmixData = await VmixDataUtils.SetVMIXDataoperations();
            //backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(vmixData.AirDropPlayerAcheivmentGuid, 3, false, 300));

            foreach (var airdropPlayer in playerInfo.PlayerInfoList.Where(x => x.GotAirDropNum > 0))
            {
                if (airdropPlayer == null) return;

                var redisKey = $"{Utils.HelperRedis.AirDropLootedKey}:{airdropPlayer.UId}";
                var existingData = await _redisDb.StringGetAsync(redisKey);

                if (existingData.IsNullOrEmpty)
                {
                    var currentTeam = liveTeamPointStats.FirstOrDefault(x => x.teamid == airdropPlayer.TeamId);
                    //var currentPlayer = players.FirstOrDefault(x => x.PlayerUid == airdropPlayer.UId.ToString());

                    if (currentTeam == null) return;

                    var airdropInfo = new AirDropLootedInfo
                    {
                        AirdropLootedNumber = airdropPlayer.GotAirDropNum,
                        DateTime = DateTime.UtcNow,
                        PlayerId = airdropPlayer.UId.ToString()
                    };

                    await _redisDb.StringSetAsync(redisKey, JsonSerializer.Serialize(airdropInfo));

                    var apiCalls = new List<string>
                {
                    vmi_layerSetOnOff.GetSetTextApiCall(vmixData.AirDropPlayerAcheivmentGuid, "PNAME", airdropPlayer.PlayerName ?? "Unknown"),
                     vmi_layerSetOnOff.GetSetImageApiCall(vmixData.AirDropPlayerAcheivmentGuid, "TLOGO", $"{ConfigGlobal.LogosImages}\\{currentTeam.teamid}.png"),
                         vmi_layerSetOnOff.GetSetImageApiCall(vmixData.AirDropPlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\0.png"),
                         vmi_layerSetOnOff.GetSetImageApiCall(vmixData.AirDropPlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\{airdropPlayer.UId}.png")
                };

                    backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(vmixData.AirDropPlayerAcheivmentGuid, 3, true, 4000, apiCalls));
                }
            }
        }

        [AutomaticRetry(Attempts = 0, DelaysInSeconds = new[] { 1, 1 })]
        public async Task<bool> FirstBloodAsync(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats)
        {
            using var scope = _serviceProvider.CreateScope();

            var backgroundJobClient = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            var connectionMultiplexer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var _redisDb = connectionMultiplexer.GetDatabase();
            var redisKey = HelperRedis.FirstBloodKey;
            var existingData = (await _redisDb.StringGetAsync(redisKey)).ToString();

            if (string.IsNullOrEmpty(existingData))
            {
                if (playerInfo.PlayerInfoList.Any(x => x.KillNum > 0))
                {
                    if (playerInfo?.PlayerInfoList == null || !playerInfo.PlayerInfoList.Any())
                        return false;

                    var vmixData = await VmixDataUtils.SetVMIXDataoperations();
                    // backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(vmixData.FirstBloodPlayerAcheivmentGuid, 3, false, 300));
                    var FirstBloodplayer = playerInfo.PlayerInfoList.OrderByDescending(x => x.KillNum).Take(1).FirstOrDefault();

                    var firstBloodInfo = new FirstBlood
                    {
                        DateTime = DateTime.UtcNow,
                        PlayerId = FirstBloodplayer.UId.ToString()
                    };

                    await _redisDb.StringSetAsync(redisKey, JsonSerializer.Serialize(firstBloodInfo));
                    var currentTeam = liveTeamPointStats.FirstOrDefault(x => x.teamid == FirstBloodplayer.TeamId);

                    if (currentTeam == null) return false;

                    await _redisDb.StringSetAsync(redisKey, JsonSerializer.Serialize(firstBloodInfo));

                    var apiCalls = new List<string>
                {
                    vmi_layerSetOnOff.GetSetTextApiCall(vmixData.FirstBloodPlayerAcheivmentGuid, "PNAME", FirstBloodplayer.PlayerName ?? "Unknown"),
                     vmi_layerSetOnOff.GetSetImageApiCall(vmixData.FirstBloodPlayerAcheivmentGuid, "TLOGO", $"{ConfigGlobal.LogosImages}\\{currentTeam.teamid}.png"),
                         vmi_layerSetOnOff.GetSetImageApiCall(vmixData.FirstBloodPlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\0.png"),
                         vmi_layerSetOnOff.GetSetImageApiCall(vmixData.FirstBloodPlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\{FirstBloodplayer.UId}.png")
                };

                    backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(vmixData.FirstBloodPlayerAcheivmentGuid, 3, true, 4000, apiCalls));
                }
                return true;

            }

            return false;
        }


        [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 1, 1 })]
        public async Task KillDominationAsync(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats)
        {
            List<int> killsindexes = new List<int>() { 3, 5, 7, 10, 13, 15 };
            using var scope = _serviceProvider.CreateScope();

            var backgroundJobClient = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            var connectionMultiplexer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var _redisDb = connectionMultiplexer.GetDatabase();
            if (playerInfo?.PlayerInfoList == null || !playerInfo.PlayerInfoList.Any())
                return;

            var vmixData = await VmixDataUtils.SetVMIXDataoperations();

            foreach (var killDominationPlayer in playerInfo.PlayerInfoList
                .Where(x => killsindexes.Any(k => x.KillNum > k)))
            {
                int? exceededKillIndex = killsindexes
                    .Where(k => killDominationPlayer.KillNum > k)
                    .DefaultIfEmpty(-1)
                    .Max();

                var redisKey = $"{Utils.HelperRedis.KillDominationKey}:{killDominationPlayer.UId}";

                var existingData = JsonSerializer.Deserialize<killDominationInfo>(
                    await _redisDb.StringGetAsync(redisKey)
                ) ?? new killDominationInfo();

                if (exceededKillIndex == -1 || existingData.KillInfo == exceededKillIndex)
                {
                    continue;
                }

                var currentTeam = liveTeamPointStats.FirstOrDefault(x => x.teamid == killDominationPlayer.TeamId);
                if (currentTeam == null) continue;

                var airdropInfo = new killDominationInfo
                {
                    KillInfo = exceededKillIndex,
                    DateTime = DateTime.UtcNow,
                    PlayerId = killDominationPlayer.UId.ToString()
                };

                await _redisDb.StringSetAsync(redisKey, JsonSerializer.Serialize(airdropInfo));

                var apiCalls = new List<string>
                {
                    vmi_layerSetOnOff.GetSetTextApiCall(vmixData.AirDropPlayerAcheivmentGuid, "PNAME", killDominationPlayer.PlayerName ?? "Unknown"),
                     vmi_layerSetOnOff.GetSetImageApiCall(vmixData.AirDropPlayerAcheivmentGuid, "TLOGO", $"{ConfigGlobal.LogosImages}\\{currentTeam.teamid}.png"),
                         vmi_layerSetOnOff.GetSetImageApiCall(vmixData.AirDropPlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\0.png"),
                         vmi_layerSetOnOff.GetSetImageApiCall(vmixData.AirDropPlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\{killDominationPlayer.UId}.png")
                };

                backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(vmixData.AirDropPlayerAcheivmentGuid, 3, true, 4000, apiCalls));
            }
        }
        [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 1, 1 })]
        public async Task DamageDominationAsync(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats)
        {
            List<int> damageIndexes = new List<int>() { 500, 800, 1000, 1200, 1400, 1500, 1600, 2000 };
            using var scope = _serviceProvider.CreateScope();

            var backgroundJobClient = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            var connectionMultiplexer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var _redisDb = connectionMultiplexer.GetDatabase();
            if (playerInfo?.PlayerInfoList == null || !playerInfo.PlayerInfoList.Any())
                return;

            var vmixData = await VmixDataUtils.SetVMIXDataoperations();

            foreach (var damageDominationPlayer in playerInfo.PlayerInfoList
                .Where(x => damageIndexes.Any(d => x.Damage > d)))
            {
                int? exceededDamageIndex = damageIndexes
                    .Where(d => damageDominationPlayer.Damage > d)
                    .DefaultIfEmpty(-1)
                    .Max();

                var redisKey = $"{Utils.HelperRedis.DamageDominationKey}:{damageDominationPlayer.UId}";
                var existingData = JsonSerializer.Deserialize<DamageDominationInfo>(
                    await _redisDb.StringGetAsync(redisKey)
                ) ?? new DamageDominationInfo();

                if (exceededDamageIndex == -1 || existingData.DamageInfo == exceededDamageIndex)
                {
                    continue;
                }

                var currentTeam = liveTeamPointStats.FirstOrDefault(x => x.teamid == damageDominationPlayer.TeamId);
                if (currentTeam == null) continue;

                var damageInfo = new DamageDominationInfo
                {
                    DamageInfo = exceededDamageIndex,
                    DateTime = DateTime.UtcNow,
                    PlayerId = damageDominationPlayer.UId.ToString()
                };

                await _redisDb.StringSetAsync(redisKey, JsonSerializer.Serialize(damageInfo));

                var apiCalls = new List<string>
                {
                    vmi_layerSetOnOff.GetSetTextApiCall(vmixData.AirDropPlayerAcheivmentGuid, "PNAME", damageDominationPlayer.PlayerName ?? "Unknown"),
                     vmi_layerSetOnOff.GetSetImageApiCall(vmixData.AirDropPlayerAcheivmentGuid, "TLOGO", $"{ConfigGlobal.LogosImages}\\{currentTeam.teamid}.png"),
                         vmi_layerSetOnOff.GetSetImageApiCall(vmixData.AirDropPlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\0.png"),
                         vmi_layerSetOnOff.GetSetImageApiCall(vmixData.AirDropPlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\{damageDominationPlayer.UId}.png")
                };

                backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(vmixData.AirDropPlayerAcheivmentGuid, 3, true, 4000, apiCalls));
            }
        }


    }
}

