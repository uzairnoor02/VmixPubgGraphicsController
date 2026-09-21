using System.Text.Json.Nodes;

namespace FakePcob;

public enum RouteMode { Merged, Split }

/// One tick of `getcircleinfo` - field names/types match Recon Task 1 §7 (all strings, parsed
/// with int.Parse by the app).
public sealed record CircleState(string CircleStatus, string CircleIndex, string MaxTime, string Counter)
{
    public static readonly CircleState Idle = new("0", "0", "0", "0");
}

/// Per-tick circle state for a whole match. `Default` gives every plain replay (no scenario) a
/// believable-but-generic shrink pattern; Scenario.cs (Task 5) supplies a more deliberate one that
/// lines up with the scripted beat list.
public sealed class CircleTimeline
{
    private readonly List<CircleState> _byTick;
    public CircleTimeline(List<CircleState> byTick) => _byTick = byTick;

    public CircleState At(int tick) => tick >= 0 && tick < _byTick.Count ? _byTick[tick] : CircleState.Idle;

    public static CircleTimeline Default(int tickCount)
    {
        var states = new List<CircleState>(tickCount + 1);
        const int phaseCount = 8;
        var phaseLen = Math.Max(4, (tickCount + 1) / phaseCount);
        for (int tick = 0; tick <= tickCount; tick++)
        {
            var phase = Math.Min(phaseCount - 1, tick / phaseLen);
            var intoPhase = tick - phase * phaseLen;
            var closingStart = Math.Max(0, phaseLen - 8);
            if (intoPhase >= closingStart && phase < phaseCount - 1)
            {
                var counter = Math.Min(phaseLen, intoPhase - closingStart);
                states.Add(new CircleState("2", (phase + 1).ToString(), phaseLen.ToString(), counter.ToString()));
            }
            else
            {
                states.Add(new CircleState("0", (phase + 1).ToString(), phaseLen.ToString(), "0"));
            }
        }
        return new CircleTimeline(states);
    }
}

/// Builds the actual HTTP response bodies FeedServer hands back, applying the three-route timing
/// model from Task 4. Recon Task 1 found the app calls a single `gettotalplayerlist` GET (no
/// separate push routes exist) - so "route A" vs "route B" is reproduced as two different
/// *tick-offsets into the same response*, not two endpoints: the PlayerBaseInfo group always
/// reflects the current tick, the PlayerRealTimeAPI group is read one tick behind in `split` mode
/// (deliberately stale, per the spec: "the app regularly sees a fresh health with a stale
/// damage"), and the PlayerAfterMatchAPI group is hard-zero until the very last tick. `merged`
/// mode reads every group off the current tick, for isolating whether a bug is caused by the
/// split timing rather than genuinely two routes.
public static class FeedResponses
{
    public static string BuildPlayerListJson(BuiltMatch match, int tick, RouteMode mode)
    {
        var baseFrame = match.Frames[Math.Clamp(tick, 0, match.TickCount)];
        var realtimeTick = mode == RouteMode.Split ? Math.Max(0, tick - 1) : tick;
        var realtimeFrame = match.Frames[Math.Clamp(realtimeTick, 0, match.TickCount)];
        var isFinal = tick >= match.TickCount;

        var arr = new JsonArray();
        foreach (var seedPlayer in match.FinalPlayers)
        {
            var b = baseFrame.Players.First(p => p.UId == seedPlayer.UId);
            var r = realtimeFrame.Players.First(p => p.UId == seedPlayer.UId);
            var after = isFinal ? AfterMatchFields.FromSeed(seedPlayer) : AfterMatchFields.Zero;

            var obj = new JsonObject
            {
                ["uId"] = seedPlayer.UId,
                ["playerName"] = seedPlayer.PlayerName,
                ["playerOpenId"] = seedPlayer.PlayerOpenId,
                ["picUrl"] = seedPlayer.PicUrl,
                ["showPicUrl"] = seedPlayer.ShowPicUrl,
                ["teamId"] = seedPlayer.TeamId,
                ["teamName"] = seedPlayer.TeamName,
                ["character"] = seedPlayer.Character,
                ["isFiring"] = b.LiveState is 0 or 1 or 2 or 3,
                // The real PCOB always sends bHasDied=false, even at match end (Recon Task 1 /
                // seed/SEEDS.md) - reproduced faithfully here so this stays a good regression test
                // for "does the app actually use liveState==5 instead of this field".
                ["bHasDied"] = false,
                ["location"] = new JsonObject { ["x"] = b.Location.X, ["y"] = b.Location.Y, ["z"] = b.Location.Z },
                ["health"] = b.Health,
                ["healthMax"] = b.HealthMax,
                ["liveState"] = b.LiveState,
                ["killNum"] = b.KillNum,
                ["killNumBeforeDie"] = b.KillNumBeforeDie,
                ["playerKey"] = seedPlayer.PlayerKey,
                ["gotAirDropNum"] = r.GotAirDropNum,
                ["maxKillDistance"] = r.MaxKillDistance,
                ["damage"] = r.Damage,
                ["killNumInVehicle"] = r.KillNumInVehicle,
                ["killNumByGrenade"] = r.KillNumByGrenade,
                ["AIKillNum"] = seedPlayer.AIKillNum,
                ["BossKillNum"] = seedPlayer.BossKillNum,
                ["rank"] = r.Rank,
                ["isOutsideBlueCircle"] = r.IsOutsideBlueCircle,
                ["inDamage"] = after.InDamage,
                ["heal"] = after.Heal,
                ["headShotNum"] = after.HeadShotNum,
                ["survivalTime"] = after.SurvivalTime,
                ["driveDistance"] = after.DriveDistance,
                ["marchDistance"] = after.MarchDistance,
                ["assists"] = after.Assists,
                ["outsideBlueCircleTime"] = after.OutsideBlueCircleTime,
                ["knockouts"] = after.Knockouts,
                ["rescueTimes"] = after.RescueTimes,
                ["useSmokeGrenadeNum"] = after.UseSmokeGrenadeNum,
                ["useFragGrenadeNum"] = after.UseFragGrenadeNum,
                ["useBurnGrenadeNum"] = after.UseBurnGrenadeNum,
                ["useFlashGrenadeNum"] = after.UseFlashGrenadeNum,
                ["PoisonTotalDamage"] = seedPlayer.PoisonTotalDamage,
                ["UseSelfRescueTime"] = seedPlayer.UseSelfRescueTime,
                ["UseEmergencyCallTime"] = seedPlayer.UseEmergencyCallTime,
            };
            arr.Add(obj);
        }
        return new JsonObject { ["playerInfoList"] = arr }.ToJsonString();
    }

    /// Team aggregation kept in lock-step with the player list (Recon Task 1 §4): `killNum` is the
    /// live sum of member kills and `liveMemberNum` is the count of members with liveState<=4, so
    /// every call site - whether it reads teams or sums players - sees the same numbers.
    public static string BuildTeamListJson(BuiltMatch match, int tick, RouteMode mode)
    {
        var baseFrame = match.Frames[Math.Clamp(tick, 0, match.TickCount)];
        var realtimeTick = mode == RouteMode.Split ? Math.Max(0, tick - 1) : tick;
        var realtimeFrame = match.Frames[Math.Clamp(realtimeTick, 0, match.TickCount)];

        var teams = match.FinalPlayers.GroupBy(p => p.TeamId).OrderBy(g => g.Key);
        var arr = new JsonArray();
        foreach (var team in teams)
        {
            var memberIds = team.Select(p => p.UId).ToHashSet();
            var liveMembers = baseFrame.Players.Count(p => memberIds.Contains(p.UId) && p.LiveState is >= 0 and <= 4);
            var killSum = realtimeFrame.Players.Where(p => memberIds.Contains(p.UId)).Sum(p => p.KillNum);
            arr.Add(new JsonObject
            {
                ["teamId"] = team.Key,
                ["teamName"] = team.First().TeamName,
                ["killNum"] = killSum,
                ["liveMemberNum"] = liveMembers,
                ["memberNum"] = team.Count(),
            });
        }
        return new JsonObject { ["teamInfoList"] = arr }.ToJsonString();
    }

    public static string BuildCircleInfoJson(CircleState state) => new JsonObject
    {
        ["CircleInfo"] = new JsonObject
        {
            ["CircleStatus"] = state.CircleStatus,
            ["CircleIndex"] = state.CircleIndex,
            ["MaxTime"] = state.MaxTime,
            ["Counter"] = state.Counter,
        },
    }.ToJsonString();

    public static string BuildIsInGameJson(bool inGame) => new JsonObject { ["IsInGame"] = inGame }.ToJsonString();

    /// A valid but empty envelope, per B0 rule 5: this tool implements no `getkillinfo` logic
    /// (the app doesn't use it - kills come from the per-player killNum counters) - it exists only
    /// so a real app build that DOES poll it never throws on a missing/malformed response.
    public static string BuildEmptyKillInfoJson() => new JsonObject { ["killInfoList"] = new JsonArray() }.ToJsonString();
}
