using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness.Utils;

namespace VmixGraphicsBusiness.LiveMatch
{
    /// <summary>
    /// Detects player achievements from counter changes between ticks and publishes each one to
    /// the overlay (MatchStateStore.PublishAchievement -> "achievement.*" banner).
    ///
    /// Called inline once per tick from LiveStatsBusiness.CreateLiveStats. This used to enqueue
    /// four Hangfire jobs per tick, and each job read vMix's input list and animated a vMix
    /// Title - so with vMix closed, no achievement ever reached the web overlay. Detection logic
    /// (what counts as "new") is unchanged.
    /// </summary>
    public class SetPlayerAchievements
    {
        private readonly IServiceProvider _serviceProvider;

        public SetPlayerAchievements(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<bool> GetAllAchievements(LivePlayersList playerInfo, List<LiveTeamPointStats> liveTeamPointStats)
        {
            if (playerInfo?.PlayerInfoList == null || !playerInfo.PlayerInfoList.Any())
                return false;

            using var scope = _serviceProvider.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<MatchStateStore>();

            // Each detector is isolated so one bad record can't suppress the others.
            await RunAsync(() => FirstBloodAsync(store, playerInfo, liveTeamPointStats));
            await RunAsync(() => GrenadeEliminationsAsync(store, playerInfo, liveTeamPointStats));
            await RunAsync(() => AirDropLootedAsync(store, playerInfo, liveTeamPointStats));
            await RunAsync(() => VehicleEliminationsAsync(store, playerInfo, liveTeamPointStats));
            return true;
        }

        private static async Task RunAsync(Func<Task> detector)
        {
            try { await detector(); }
            catch (Exception ex) { Console.WriteLine($"Achievement detector failed: {ex.Message}"); }
        }

        private static T? ReadState<T>(string raw) where T : class
        {
            if (string.IsNullOrEmpty(raw)) return null;
            try { return JsonSerializer.Deserialize<T>(raw); }
            catch { return null; }
        }

        private static void Publish(MatchStateStore store, string type, LivePlayerInfo player, LiveTeamPointStats team, bool withVictim)
        {
            // The victim comes from getkillinfo when it's running (see MatchStateStore.RecordKill);
            // without it the banner simply shows the player.
            var victim = withVictim ? store.LastVictimOf(player.UId.ToString(), player.PlayerName) : null;
            store.PublishAchievement(new LiveAchievementEvent(
                type,
                player.PlayerName ?? "Unknown Player",
                team.teamName ?? team.teamid.ToString(),
                player.UId.ToString(),
                victim,
                team.teamid));
        }

        // First blood waits at most this many ticks for getkillinfo to deliver the victim's name
        // (it lags the player list by up to ~2 s). Only when the kill feed is running at all.
        private const int FirstBloodMaxWaitTicks = 3;
        private const string FirstBloodWaitKey = "FirstBloodWait";

        private static async Task VehicleEliminationsAsync(MatchStateStore store, LivePlayersList playerInfo, List<LiveTeamPointStats> teams)
        {
            foreach (var player in playerInfo.PlayerInfoList)
            {
                if (player.KillNumInVehicle <= 0) continue;

                var key = $"{HelperRedis.VehicleEliminationsKey}:{player.UId}";
                // Previously deserialised a missing key straight away, which threw on the first
                // vehicle kill of every match - so this achievement could never fire.
                var seen = ReadState<VehicleEliminationInfo>(await store.StringGetAsync(key));
                if (seen != null && seen.VehicleKills >= player.KillNumInVehicle) continue;

                var team = teams.FirstOrDefault(x => x.teamid == player.TeamId);
                if (team == null) continue;

                await store.StringSetAsync(key, JsonSerializer.Serialize(new VehicleEliminationInfo
                {
                    VehicleKills = player.KillNumInVehicle,
                    DateTime = DateTime.UtcNow,
                    PlayerId = player.UId.ToString()
                }));
                Publish(store, "achievement.vehicleKill", player, team, withVictim: true);
            }
        }

        private static async Task GrenadeEliminationsAsync(MatchStateStore store, LivePlayersList playerInfo, List<LiveTeamPointStats> teams)
        {
            foreach (var player in playerInfo.PlayerInfoList)
            {
                if (player.KillNumByGrenade <= 0) continue;

                var key = $"{HelperRedis.GrenadeEliminationsKey}:{player.UId}";
                var seen = ReadState<GrenadeEliminationInfo>(await store.StringGetAsync(key));
                if (seen != null && seen.GrenadeKills >= player.KillNumByGrenade) continue;

                var team = teams.FirstOrDefault(x => x.teamid == player.TeamId);
                if (team == null) continue;

                await store.StringSetAsync(key, JsonSerializer.Serialize(new GrenadeEliminationInfo
                {
                    GrenadeKills = player.KillNumByGrenade,
                    DateTime = DateTime.UtcNow,
                    PlayerId = player.UId.ToString()
                }));
                Publish(store, "achievement.grenadeElim", player, team, withVictim: true);
            }
        }

        private static async Task AirDropLootedAsync(MatchStateStore store, LivePlayersList playerInfo, List<LiveTeamPointStats> teams)
        {
            // Once per player per match, on their first airdrop (same rule as before).
            foreach (var player in playerInfo.PlayerInfoList.Where(x => x.GotAirDropNum > 0))
            {
                var key = $"{HelperRedis.AirDropLootedKey}:{player.UId}";
                if (!string.IsNullOrEmpty(await store.StringGetAsync(key))) continue;

                var team = teams.FirstOrDefault(x => x.teamid == player.TeamId);
                // Was "return" - one player with an unknown team stopped every later player's
                // airdrop from being checked that tick.
                if (team == null) continue;

                await store.StringSetAsync(key, JsonSerializer.Serialize(new AirDropLootedInfo
                {
                    AirdropLootedNumber = player.GotAirDropNum,
                    DateTime = DateTime.UtcNow,
                    PlayerId = player.UId.ToString()
                }));
                Publish(store, "achievement.airdropLoot", player, team, withVictim: false);
            }
        }

        private static async Task FirstBloodAsync(MatchStateStore store, LivePlayersList playerInfo, List<LiveTeamPointStats> teams)
        {
            var key = HelperRedis.FirstBloodKey;
            if (!string.IsNullOrEmpty(await store.StringGetAsync(key))) return;

            var firstBloodPlayer = playerInfo.PlayerInfoList
                .Where(x => x.KillNum > 0)
                .OrderByDescending(x => x.KillNum)
                .FirstOrDefault();
            if (firstBloodPlayer == null) return;

            // Give the kill feed a moment to name the victim, so the banner can read
            // "PLAYER >> VICTIM" like the real broadcast.
            if (store.KillFeedActive && store.LastVictimOf(firstBloodPlayer.UId.ToString(), firstBloodPlayer.PlayerName) is null)
            {
                int.TryParse(await store.StringGetAsync(FirstBloodWaitKey), out var waited);
                if (waited < FirstBloodMaxWaitTicks)
                {
                    await store.StringSetAsync(FirstBloodWaitKey, (waited + 1).ToString(), TimeSpan.FromMinutes(5));
                    return;
                }
            }

            await store.StringSetAsync(key, JsonSerializer.Serialize(new FirstBlood
            {
                DateTime = DateTime.UtcNow,
                PlayerId = firstBloodPlayer.UId.ToString()
            }));

            var team = teams.FirstOrDefault(x => x.teamid == firstBloodPlayer.TeamId);
            if (team == null) return;
            Publish(store, "achievement.firstKill", firstBloodPlayer, team, withVictim: true);
        }
    }
}
