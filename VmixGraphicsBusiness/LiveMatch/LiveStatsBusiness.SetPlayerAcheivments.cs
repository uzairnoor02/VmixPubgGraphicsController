using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;
using System.Linq;
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
        public async Task<bool> GetAllAchievements(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats, List<KillInfoRow> newKillEvents, int matchDbId, string currentGameTime = null)
        {
            newKillEvents ??= new List<KillInfoRow>();

            // FirstBlood and Grenade now react to the real getkillinfo event (see KillFeedTracker),
            // instead of guessing from whichever player's KillNum/KillNumByGrenade happened to move
            // first in gettotalplayerlist that tick.
            var jobId0 = _backgroundJobClient.Enqueue(HangfireQueues.LowPriority, () => FirstBloodAsync(newKillEvents, playerInfo, liveTeamPointStats, matchDbId));
            var jobId1 = _backgroundJobClient.Enqueue(HangfireQueues.LowPriority, () => GrenadeEliminationsAsync(newKillEvents, playerInfo, liveTeamPointStats, matchDbId));

            // Airdrop/Vehicle have no per-row event feed to filter (unlike FirstBlood/Grenade above) --
            // they scan every player in playerInfo every single poll off raw counters
            // (GotAirDropNum / KillNumInVehicle). getallinfo's CurrentTime is pcob's own match clock,
            // included in every poll response; if it hasn't advanced since the last time we actually
            // ran these two checks for this match, pcob's own data hasn't moved either, so there's
            // nothing new for them to find. Skip enqueueing both jobs in that case -- one cheap Redis
            // GET (+ a SET when we do proceed) buys back two full-player-list Hangfire jobs per poll
            // whenever the clock is unchanged (duplicate/backed-up poll, or CurrentTime not present).
            if (await ShouldRunCounterBasedAchievementsAsync(matchDbId, currentGameTime))
            {
                var jobId2 = _backgroundJobClient.Enqueue(HangfireQueues.LowPriority, () => AirDropLootedAsync(playerInfo, liveTeamPointStats, matchDbId));
                var jobId3 = _backgroundJobClient.Enqueue(HangfireQueues.LowPriority, () => VehicleEliminationsAsync(playerInfo, liveTeamPointStats, matchDbId));
            }

            return true;
        }

        // Returns true (and records currentGameTime as the new checkpoint) when Airdrop/Vehicle
        // should run this poll: either currentGameTime wasn't provided/parseable (fail open -- never
        // let a missing clock silently stop these achievements from ever firing), or it's strictly
        // greater than the last game-time we ran them at for this match.
        private async Task<bool> ShouldRunCounterBasedAchievementsAsync(int matchDbId, string currentGameTime)
        {
            if (string.IsNullOrEmpty(currentGameTime) || !long.TryParse(currentGameTime, out var currentGameTimeValue))
                return true;

            using var scope = _serviceProvider.CreateScope();
            var connectionMultiplexer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var redisDb = connectionMultiplexer.GetDatabase();

            var redisKey = $"{HelperRedis.AchievementCheckTimeKey}:{matchDbId}";
            var lastChecked = await redisDb.StringGetAsync(redisKey);

            if (!lastChecked.IsNullOrEmpty && long.TryParse(lastChecked, out var lastCheckedValue) && currentGameTimeValue <= lastCheckedValue)
                return false;

            await redisDb.StringSetAsync(redisKey, currentGameTimeValue.ToString());
            return true;
        }

        [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 1, 1 })]
        [DisableConcurrentExecution(timeoutInSeconds: 1)]
        public async Task VehicleEliminationsAsync(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats, int matchDbId)
        {
            using var scope = _serviceProvider.CreateScope();
            var backgroundJobClient = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            var connectionMultiplexer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var _redisDb = connectionMultiplexer.GetDatabase();
            var vmixData = await VmixDataUtils.SetVMIXDataoperations();
            // backgroundJobClient.Enqueue(()=> vmi_layerSetOnOff.PushAnimationAsync(vmixData.VehiclePlayerAcheivmentGuid, 3, false, 300));

            // NOTE: getkillinfo's vehicle-kill ItemIDs (e.g. 1907066/1961061 seen in the 23 Sep
            // capture) aren't confirmed against a full item table yet, so this still triggers off
            // gettotalplayerlist's KillNumInVehicle counter rather than a real kill event. Once an
            // ItemID is confirmed for vehicle kills, this can switch to newKillEvents the same way
            // GrenadeEliminationsAsync did, and drop the per-player Redis bookkeeping below entirely.
            foreach (var player in playerInfo.PlayerInfoList)
            {
                if (player.KillNumInVehicle <= 0) continue;

                // Match-scoped: this used to be a single key per player with no match id, so once a
                // player got their first vehicle kill in match 1 it never fired again for them in any
                // later match (their stored count from match 1 was already >= match 2's count).
                var redisKey = $"{HelperRedis.VehicleEliminationsKey}:{matchDbId}:{player.UId}";
                var existingData = await _redisDb.StringGetAsync(redisKey);

                // Guarded: this used to call Deserialize<T>(null) on a player's first-ever vehicle
                // kill (the Redis key doesn't exist yet), which threw, retried twice against the same
                // missing key, and failed for good -- so the banner never fired for anyone's first one.
                var vehicleKills = existingData.IsNullOrEmpty
                    ? new VehicleEliminationInfo { VehicleKills = 0 }
                    : JsonSerializer.Deserialize<VehicleEliminationInfo>(existingData);

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
        [DisableConcurrentExecution(timeoutInSeconds: 1)]
        public async Task GrenadeEliminationsAsync(List<KillInfoRow> newKillEvents, LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats, int matchDbId)
        {
            var grenadeKillsThisPoll = newKillEvents?.Where(r => r.IsGrenadeKill).OrderBy(r => r.CurGameTimeSeconds).ToList();
            if (grenadeKillsThisPoll == null || grenadeKillsThisPoll.Count == 0) return;

            using var scope = _serviceProvider.CreateScope();
            var backgroundJobClient = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            var connectionMultiplexer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var _redisDb = connectionMultiplexer.GetDatabase();
            var vmixData = await VmixDataUtils.SetVMIXDataoperations();
            //backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(vmixData.GrenadePlayerAcheivmentGuid, 3, false, 300));

            // Watermark on CurGameTime, scoped per match, kept alongside KillFeedTracker's own
            // row-count dedupe: only rows strictly after the last grenade kill we already showed a
            // banner for get evaluated here -- never every grenade kill of the match re-checked
            // against what's already saved.
            var lastTimeKey = $"{HelperRedis.GrenadeLastKillTimeKey}:{matchDbId}";
            var storedLastTime = await _redisDb.StringGetAsync(lastTimeKey);
            long lastGrenadeTime = storedLastTime.IsNullOrEmpty ? -1 : (long)storedLastTime;

            var grenadeKills = grenadeKillsThisPoll.Where(r => r.CurGameTimeSeconds > lastGrenadeTime).ToList();
            if (grenadeKills.Count == 0) return;

            await _redisDb.StringSetAsync(lastTimeKey, grenadeKills.Max(r => r.CurGameTimeSeconds));

            // Two grenade kills by the SAME player land as one banner (collapse consecutive rows for
            // that causer); two grenade kills by DIFFERENT players each get their own banner, staggered
            // 5s apart so they don't overlap on screen.
            var collapsed = new List<KillInfoRow>();
            foreach (var kill in grenadeKills)
            {
                if (collapsed.Count > 0 && collapsed[^1].CauserUID == kill.CauserUID) continue;
                collapsed.Add(kill);
            }

            for (int i = 0; i < collapsed.Count; i++)
            {
                var kill = collapsed[i];
                var player = playerInfo?.PlayerInfoList?.FirstOrDefault(p => p.UId.ToString() == kill.CauserUID);
                var currentTeam = liveTeamPointStats.FirstOrDefault(x => x.teamid == (player?.TeamId ?? -1));
                if (currentTeam == null) continue;

                // Fall back to the row's own name only if we can't resolve the UID against the current
                // player list -- CauserUID is still what we key on (CauserName can read "Playzone").
                var playerName = player?.PlayerName ?? kill.CauserName ?? "Unknown Player";
                var playerUidForImage = player?.UId.ToString() ?? kill.CauserUID;

                var apiCalls = new List<string>
                {
                    vmi_layerSetOnOff.GetSetTextApiCall(vmixData.GrenadePlayerAcheivmentGuid, "PNAME", playerName),
                    vmi_layerSetOnOff.GetSetImageApiCall(vmixData.GrenadePlayerAcheivmentGuid, "TLOGO", $"{ConfigGlobal.LogosImages}\\{currentTeam.teamid}.png"),
                     vmi_layerSetOnOff.GetSetImageApiCall(vmixData.GrenadePlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\0.png"),
                     vmi_layerSetOnOff.GetSetImageApiCall(vmixData.GrenadePlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\{playerUidForImage}.png")
                };

                if (i == 0)
                {
                    backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(vmixData.GrenadePlayerAcheivmentGuid, 3, true, 4000, apiCalls));
                }
                else
                {
                    // Staggered by player index, not wall-clock enqueue time, so a batch of 3+ kills in
                    // one poll still reads 0s/5s/10s apart instead of bunching at "now".
                    backgroundJobClient.Schedule(() => vmi_layerSetOnOff.PushAnimationAsync(vmixData.GrenadePlayerAcheivmentGuid, 3, true, 4000, apiCalls), TimeSpan.FromSeconds(5 * i));
                }
            }
        }

        [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 1, 1 })]
        [DisableConcurrentExecution(timeoutInSeconds: 1)]
        public async Task AirDropLootedAsync(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats, int matchDbId)
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
                // Was `return` here and below -- one player with no resolvable team used to skip every
                // other player's airdrop-loot check for the whole poll, not just their own.
                if (airdropPlayer == null) continue;

                var redisKey = $"{HelperRedis.AirDropLootedKey}:{matchDbId}:{airdropPlayer.UId}";
                var existingData = await _redisDb.StringGetAsync(redisKey);

                if (existingData.IsNullOrEmpty)
                {
                    var currentTeam = liveTeamPointStats.FirstOrDefault(x => x.teamid == airdropPlayer.TeamId);
                    //var currentPlayer = players.FirstOrDefault(x => x.PlayerUid == airdropPlayer.UId.ToString());

                    if (currentTeam == null) continue;

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
        [DisableConcurrentExecution(timeoutInSeconds: 1)]
        public async Task<bool> FirstBloodAsync(List<KillInfoRow> newKillEvents, LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats, int matchDbId)
        {
            // newKillEvents is oldest-first and match-scoped (KillFeedTracker), so the first kill row
            // in it -- the first time this method sees any -- IS the real first blood of the match,
            // by actual CurGameTime. No more `OrderByDescending(x => x.KillNum).Take(1)` over
            // gettotalplayerlist, which just returned whichever player happened to sit first in that
            // poll's array once two players both showed KillNum == 1 -- not whoever actually killed
            // first. Knocks (ResultHealthStatus == "1") are excluded; only a real kill counts.
            var firstKillRow = newKillEvents?.FirstOrDefault(r => r.IsKill);
            if (firstKillRow == null) return false;

            using var scope = _serviceProvider.CreateScope();

            var backgroundJobClient = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            var connectionMultiplexer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var _redisDb = connectionMultiplexer.GetDatabase();

            // Match-scoped: this used to be a single global Redis key ("FirstBlood") with no match id
            // and nothing ever clears it (see the audit doc's note that Reset.ResetAll is never
            // called), so First Blood would only ever fire once per server restart -- not once per
            // match. This key resolves itself: it's set fresh, per match id, the first time it's used.
            var redisKey = $"{HelperRedis.FirstBloodKey}:{matchDbId}";

            // SETNX-style guard: if two ticks somehow raced here, only the one that actually creates
            // the key gets to fire the banner.
            bool wonTheRace = await _redisDb.StringSetAsync(redisKey, firstKillRow.DedupeKey, when: When.NotExists);
            if (!wonTheRace) return false;

            var vmixData = await VmixDataUtils.SetVMIXDataoperations();
            var causer = playerInfo?.PlayerInfoList?.FirstOrDefault(p => p.UId.ToString() == firstKillRow.CauserUID);
            var currentTeam = liveTeamPointStats.FirstOrDefault(x => x.teamid == (causer?.TeamId ?? -1));

            if (currentTeam == null) return false;

            var causerName = causer?.PlayerName ?? firstKillRow.CauserName ?? "Unknown";
            var causerUidForImage = causer?.UId.ToString() ?? firstKillRow.CauserUID;

            var apiCalls = new List<string>
                {
                    vmi_layerSetOnOff.GetSetTextApiCall(vmixData.FirstBloodPlayerAcheivmentGuid, "PNAME", causerName),
                     vmi_layerSetOnOff.GetSetImageApiCall(vmixData.FirstBloodPlayerAcheivmentGuid, "TLOGO", $"{ConfigGlobal.LogosImages}\\{currentTeam.teamid}.png"),
                         vmi_layerSetOnOff.GetSetImageApiCall(vmixData.FirstBloodPlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\0.png"),
                         vmi_layerSetOnOff.GetSetImageApiCall(vmixData.FirstBloodPlayerAcheivmentGuid, $"PICP1", $"{ConfigGlobal.PlayerImages}\\{causerUidForImage}.png")
                };

            backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(vmixData.FirstBloodPlayerAcheivmentGuid, 3, true, 4000, apiCalls));
            return true;
        }


        [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 1, 1 })]
        [DisableConcurrentExecution(timeoutInSeconds: 1)]
        public async Task KillDominationAsync(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats, int matchDbId)
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

                // Match-scoped, same reasoning as VehicleEliminationsAsync above.
                var redisKey = $"{HelperRedis.KillDominationKey}:{matchDbId}:{killDominationPlayer.UId}";
                var existingRedisValue = await _redisDb.StringGetAsync(redisKey);

                // Guarded: `JsonSerializer.Deserialize<T>(null)` throws before `?? new T()` ever runs,
                // so this used to crash on the first threshold any player crossed in a match, same bug
                // as the vehicle-kill one above.
                var existingData = existingRedisValue.IsNullOrEmpty
                    ? new killDominationInfo()
                    : (JsonSerializer.Deserialize<killDominationInfo>(existingRedisValue) ?? new killDominationInfo());

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
        [DisableConcurrentExecution(timeoutInSeconds: 1)]
        public async Task DamageDominationAsync(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats, int matchDbId)
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

                // Match-scoped, same reasoning as VehicleEliminationsAsync above.
                var redisKey = $"{HelperRedis.DamageDominationKey}:{matchDbId}:{damageDominationPlayer.UId}";
                var existingRedisValue = await _redisDb.StringGetAsync(redisKey);

                // Guarded against the same Deserialize<T>(null) crash as KillDominationAsync above.
                var existingData = existingRedisValue.IsNullOrEmpty
                    ? new DamageDominationInfo()
                    : (JsonSerializer.Deserialize<DamageDominationInfo>(existingRedisValue) ?? new DamageDominationInfo());

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
