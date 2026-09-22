namespace FakePcob;

/// One player's simulated state at one tick. Only the "live-group" fields the real pcob actually
/// exposes while a match is in progress - AfterMatch fields (knockouts, assists, headShotNum,
/// survivalTime, ...) are handled separately in Frame.cs, per the three-route model (Task 4),
/// because they are not part of "what really happened at this tick" - they are 0 until the end.
public sealed class PlayerTick
{
    public long UId;
    public string PlayerName = "";
    public int TeamId;
    public string TeamName = "";
    public SeedLocation Location = new();
    public int Health;
    public int HealthMax;
    public int LiveState;
    public int KillNum;
    public int KillNumBeforeDie;
    public int GotAirDropNum;
    public int MaxKillDistance;
    public int Damage;
    public int KillNumInVehicle;
    public int KillNumByGrenade;
    public int Rank;
    public bool IsOutsideBlueCircle;

    public PlayerTick Clone() => new()
    {
        UId = UId, PlayerName = PlayerName, TeamId = TeamId, TeamName = TeamName,
        Location = Location.Clone(), Health = Health, HealthMax = HealthMax, LiveState = LiveState,
        KillNum = KillNum, KillNumBeforeDie = KillNumBeforeDie, GotAirDropNum = GotAirDropNum,
        MaxKillDistance = MaxKillDistance, Damage = Damage, KillNumInVehicle = KillNumInVehicle,
        KillNumByGrenade = KillNumByGrenade, Rank = Rank, IsOutsideBlueCircle = IsOutsideBlueCircle,
    };
}

public sealed class MatchFrameData
{
    public int Tick;
    public List<PlayerTick> Players = new();
}

/// A death/knock/revive/kill event the scenario overlay (Task 5) and the console logger can
/// narrate in plain English.
public sealed record SimEvent(int Tick, string Kind, long ActorUId, long? OtherUId, string Text);

public sealed class BuiltMatch
{
    public string MatchKey = "";
    public List<SeedPlayer> FinalPlayers = new();
    public List<MatchFrameData> Frames = new(); // index 0..TickCount inclusive; TickCount == final/real end state
    public int TickCount;
    public List<SimEvent> Events = new();

    public MatchFrameData FinalFrame => Frames[TickCount];
}

/// Builds a plausible tick-by-tick match timeline BACKWARDS from a real end-of-match snapshot
/// (Task 3). Nothing here invents match data: every value a client ever sees either rises toward,
/// or is copied verbatim from, a real recorded number.
public static class MatchBuilder
{
    public static BuiltMatch Build(string matchKey, int randomSeed)
    {
        var seedPlayers = Seeds.Load(matchKey);
        var rng = new Random(randomSeed);

        var matchLengthSeconds = seedPlayers.Max(p => p.SurvivalTime);
        var tickCount = Math.Max(1, matchLengthSeconds / 2);

        // True death order/timing, ascending by survivalTime (Task 3 "Death order and timing").
        var deathOrder = seedPlayers.OrderBy(p => p.SurvivalTime).ToList();

        // Per-player derived timeline parameters.
        var deathTick = new Dictionary<long, int>();
        var isSurvivor = new Dictionary<long, bool>();
        var knockStart = new Dictionary<long, int>();
        var revive = new Dictionary<long, (int start, int end)?>(); // an EARLIER knock->revive window, doesn't move the real death

        foreach (var p in seedPlayers)
        {
            var dt = Math.Min(tickCount, p.SurvivalTime / 2);
            var survivor = dt >= tickCount; // effectively alive through the whole match
            deathTick[p.UId] = dt;
            isSurvivor[p.UId] = survivor;

            var window = 3 + rng.Next(0, 4); // 3-6 ticks
            knockStart[p.UId] = Math.Max(0, dt - window);

            // Roughly one in six knocked (non-immediate, i.e. dt > ~6) players gets an earlier
            // revive instead of/in addition to their final knock window - their real death is
            // untouched, so this can never push a death past max(survivalTime).
            if (!survivor && dt > 12 && rng.Next(0, 6) == 0)
            {
                var reviveEnd = Math.Max(2, knockStart[p.UId] - 2 - rng.Next(0, 4));
                var reviveStart = Math.Max(0, reviveEnd - (3 + rng.Next(0, 3)));
                if (reviveStart < reviveEnd)
                {
                    revive[p.UId] = (reviveStart, reviveEnd);
                }
            }
        }

        // Bounding box for spawn/interpolation, from the match's own real final locations (a
        // superset of "somewhere on the map" - real coordinates, never invented ranges).
        var minX = seedPlayers.Min(p => p.Location.X); var maxX = seedPlayers.Max(p => p.Location.X);
        var minY = seedPlayers.Min(p => p.Location.Y); var maxY = seedPlayers.Max(p => p.Location.Y);
        var minZ = seedPlayers.Min(p => p.Location.Z); var maxZ = seedPlayers.Max(p => p.Location.Z);
        long RandRange(long lo, long hi) => hi <= lo ? lo : lo + (long)(rng.NextDouble() * (hi - lo));

        var spawn = new Dictionary<long, SeedLocation>();
        foreach (var p in seedPlayers)
        {
            spawn[p.UId] = new SeedLocation { X = RandRange(minX, maxX), Y = RandRange(minY, maxY), Z = RandRange(minZ, maxZ) };
        }

        // Schedule kill-type counter increments: each of a player's killNum/killNumByGrenade/
        // killNumInVehicle increments lands on a tick where SOME OTHER PLAYER actually dies
        // (Task 3 "kill increment always coincides with a real death"), and never after the
        // killer's own death.
        var deathTicksAscending = deathOrder.Select(p => deathTick[p.UId]).Distinct().OrderBy(t => t).ToList();
        List<int> PickDeathTicksBefore(int count, int beforeExclusive, long excludeUId)
        {
            var candidates = deathOrder.Where(p => p.UId != excludeUId && deathTick[p.UId] < beforeExclusive)
                .Select(p => deathTick[p.UId]).Distinct().OrderBy(t => t).ToList();
            if (candidates.Count == 0) candidates.Add(Math.Max(1, beforeExclusive - 1));
            var picked = new List<int>();
            for (int i = 0; i < count; i++)
            {
                picked.Add(candidates[i % candidates.Count]);
            }
            picked.Sort();
            return picked;
        }

        var killTicks = new Dictionary<long, List<int>>();
        var grenadeTicks = new Dictionary<long, HashSet<int>>();
        var vehicleTicks = new Dictionary<long, HashSet<int>>();
        var airdropTicks = new Dictionary<long, List<int>>();
        var damageBurstTicks = new Dictionary<long, List<int>>();

        foreach (var p in seedPlayers)
        {
            var limit = Math.Max(1, deathTick[p.UId]);
            var kt = p.KillNum > 0 ? PickDeathTicksBefore(p.KillNum, limit, p.UId) : new List<int>();
            killTicks[p.UId] = kt;
            grenadeTicks[p.UId] = kt.Take(p.KillNumByGrenade).ToHashSet();
            vehicleTicks[p.UId] = kt.Skip(p.KillNumByGrenade).Take(p.KillNumInVehicle).ToHashSet();

            var alive = Math.Max(1, limit);
            airdropTicks[p.UId] = Enumerable.Range(0, p.GotAirDropNum)
                .Select(i => (int)((double)(i + 1) / (p.GotAirDropNum + 1) * alive)).ToList();
            damageBurstTicks[p.UId] = kt.Count > 0 ? kt : Enumerable.Range(0, Math.Min(4, alive)).Select(i => (i + 1) * alive / 5).ToList();
        }

        var frames = new List<MatchFrameData>();
        var events = new List<SimEvent>();
        var runningKill = seedPlayers.ToDictionary(p => p.UId, p => 0);
        var runningGrenade = seedPlayers.ToDictionary(p => p.UId, p => 0);
        var runningVehicle = seedPlayers.ToDictionary(p => p.UId, p => 0);
        var runningAirdrop = seedPlayers.ToDictionary(p => p.UId, p => 0);
        var runningDamage = seedPlayers.ToDictionary(p => p.UId, p => 0);
        var runningMaxDist = seedPlayers.ToDictionary(p => p.UId, p => 0);
        var reportedDeath = new HashSet<long>();

        for (int tick = 0; tick <= tickCount; tick++)
        {
            var frame = new MatchFrameData { Tick = tick };
            foreach (var p in seedPlayers)
            {
                var pt = new PlayerTick
                {
                    UId = p.UId, PlayerName = p.PlayerName, TeamId = p.TeamId, TeamName = p.TeamName,
                    HealthMax = p.HealthMax,
                };

                var dt = deathTick[p.UId];
                var dead = tick >= dt && !isSurvivor[p.UId];

                // liveState: plane/parachute drop for the first two ticks, then normal, then the
                // knock window, then dead. An earlier revive window (if any) briefly interrupts
                // "normal" with knocked -> back to normal at low health.
                int liveState;
                int health = p.HealthMax; // definite-assignment fallback only - every real path below overwrites this
                if (dead)
                {
                    liveState = 5;
                    health = 0;
                }
                else if (tick >= knockStart[p.UId] && tick < dt)
                {
                    liveState = 4;
                    var span = Math.Max(1, dt - knockStart[p.UId]);
                    var progress = (double)(tick - knockStart[p.UId]) / span;
                    health = Math.Max(0, (int)(p.HealthMax * 0.35 * (1 - progress)));
                }
                else if (revive.TryGetValue(p.UId, out var rv) && rv is { } r && tick >= r.start && tick < r.end)
                {
                    liveState = 4;
                    health = Math.Max(1, (int)(p.HealthMax * 0.2));
                }
                else if (tick == 0) liveState = 1;
                else if (tick == 1) liveState = 2;
                else liveState = 0;

                if (!dead && liveState != 4)
                {
                    // Health drift outside fights (+-0..4/tick), sharper drop inside a "fight"
                    // window (near one of this player's own kill ticks or just after a revive).
                    var nearFight = killTicks[p.UId].Any(k => Math.Abs(k - tick) <= 2)
                        || (revive.TryGetValue(p.UId, out var rv2) && rv2 is { } r2 && tick >= r2.end && tick < r2.end + 3);
                    var prevHealth = tick == 0 ? p.HealthMax : frames[tick - 1].Players.First(x => x.UId == p.UId).Health;
                    if (revive.TryGetValue(p.UId, out var rv3) && rv3 is { } r3 && tick == r3.end)
                    {
                        health = Math.Max(1, (int)(p.HealthMax * (0.10 + rng.NextDouble() * 0.10)));
                    }
                    else if (nearFight)
                    {
                        health = Math.Clamp(prevHealth - rng.Next(8, 36), 1, p.HealthMax);
                    }
                    else
                    {
                        health = Math.Clamp(prevHealth + rng.Next(-4, 5), 1, p.HealthMax);
                    }
                }

                pt.LiveState = liveState;
                pt.Health = health;

                // Location: interpolate spawn -> real final location across the alive span, with
                // small jitter; frozen at the real final value once dead.
                if (dead)
                {
                    var prevLoc = frames.Count > 0 ? frames[tick - 1].Players.First(x => x.UId == p.UId).Location : p.Location;
                    pt.Location = tick == dt ? p.Location.Clone() : prevLoc;
                }
                else
                {
                    var span = Math.Max(1, dt);
                    var t = Math.Min(1.0, (double)tick / span);
                    var sp = spawn[p.UId];
                    var jitterX = rng.Next(-150, 151);
                    var jitterY = rng.Next(-150, 151);
                    pt.Location = new SeedLocation
                    {
                        X = sp.X + (long)((p.Location.X - sp.X) * t) + jitterX,
                        Y = sp.Y + (long)((p.Location.Y - sp.Y) * t) + jitterY,
                        Z = sp.Z + (long)((p.Location.Z - sp.Z) * t),
                    };
                }

                // Counters: monotonic, land on their scheduled ticks, clamp to the real final
                // value so an off-by-one scheduling quirk can never overshoot before the final
                // tick's verbatim copy takes over.
                if (killTicks[p.UId].Count(k => k <= tick) is var kc && kc > runningKill[p.UId])
                {
                    runningKill[p.UId] = Math.Min(p.KillNum, kc);
                }
                if (grenadeTicks[p.UId].Count(k => k <= tick) is var gc && gc > runningGrenade[p.UId])
                {
                    runningGrenade[p.UId] = Math.Min(p.KillNumByGrenade, gc);
                }
                if (vehicleTicks[p.UId].Count(k => k <= tick) is var vc && vc > runningVehicle[p.UId])
                {
                    runningVehicle[p.UId] = Math.Min(p.KillNumInVehicle, vc);
                }
                if (airdropTicks[p.UId].Count(k => k <= tick) is var ac && ac > runningAirdrop[p.UId])
                {
                    runningAirdrop[p.UId] = Math.Min(p.GotAirDropNum, ac);
                }
                if (p.Damage > 0)
                {
                    var burstsSoFar = damageBurstTicks[p.UId].Count(k => k <= tick);
                    var target = burstsSoFar == 0 ? 0 : p.Damage * burstsSoFar / Math.Max(1, damageBurstTicks[p.UId].Count);
                    runningDamage[p.UId] = Math.Max(runningDamage[p.UId], Math.Min(p.Damage, target));
                }
                if (p.MaxKillDistance > 0 && runningKill[p.UId] > 0)
                {
                    runningMaxDist[p.UId] = Math.Max(runningMaxDist[p.UId], Math.Min(p.MaxKillDistance, p.MaxKillDistance * runningKill[p.UId] / Math.Max(1, p.KillNum)));
                }

                pt.KillNum = runningKill[p.UId];
                pt.KillNumBeforeDie = runningKill[p.UId];
                pt.KillNumByGrenade = runningGrenade[p.UId];
                pt.KillNumInVehicle = runningVehicle[p.UId];
                pt.GotAirDropNum = runningAirdrop[p.UId];
                pt.Damage = runningDamage[p.UId];
                pt.MaxKillDistance = runningMaxDist[p.UId];
                pt.IsOutsideBlueCircle = p.IsOutsideBlueCircle && tick > tickCount - 5 && tick % 4 == 0;

                // Rank: 0 while alive, the seed's real placement once dead (spec, Task 3 "rank").
                pt.Rank = dead ? p.Rank : 0;

                frame.Players.Add(pt);

                if (dead && reportedDeath.Add(p.UId))
                {
                    events.Add(new SimEvent(tick, "death", p.UId, null, $"{p.PlayerName} ({p.TeamName}) is eliminated"));
                }
            }
            frames.Add(frame);
        }

        // Final frame (tick == tickCount) is forced to equal the seed exactly for every
        // live-group field - the one invariant that matters most, and the simplest way to
        // guarantee it for both eliminated players and match survivors alike (a survivor's real
        // `rank` is only known once the match truly ends).
        var finalFrame = frames[tickCount];
        foreach (var pt in finalFrame.Players)
        {
            var p = seedPlayers.First(x => x.UId == pt.UId);
            pt.Location = p.Location.Clone();
            pt.Health = p.Health;
            pt.HealthMax = p.HealthMax;
            pt.LiveState = p.LiveState;
            pt.KillNum = p.KillNum;
            pt.KillNumBeforeDie = p.KillNumBeforeDie;
            pt.GotAirDropNum = p.GotAirDropNum;
            pt.MaxKillDistance = p.MaxKillDistance;
            pt.Damage = p.Damage;
            pt.KillNumInVehicle = p.KillNumInVehicle;
            pt.KillNumByGrenade = p.KillNumByGrenade;
            pt.Rank = p.Rank;
            pt.IsOutsideBlueCircle = p.IsOutsideBlueCircle;
        }

        // Post-pass: derive kill/grenade/vehicle/airdrop/team-eliminated events by diffing
        // consecutive frames, so Scenario.cs (Task 5) has real ticks to hang beats on instead of
        // re-deriving the same logic a second time.
        var teamMemberIds = seedPlayers.GroupBy(p => p.TeamId).ToDictionary(g => g.Key, g => g.Select(p => p.UId).ToHashSet());
        var teamWasAlive = teamMemberIds.Keys.ToDictionary(id => id, _ => true);
        for (int tick = 1; tick <= tickCount; tick++)
        {
            var prev = frames[tick - 1];
            var cur = frames[tick];
            foreach (var p in seedPlayers)
            {
                var pPrev = prev.Players.First(x => x.UId == p.UId);
                var pCur = cur.Players.First(x => x.UId == p.UId);
                if (pCur.KillNumByGrenade > pPrev.KillNumByGrenade)
                {
                    events.Add(new SimEvent(tick, "grenade", p.UId, null, $"GRENADE ELIMINATION popup - {p.PlayerName} ({p.TeamName})"));
                }
                else if (pCur.KillNumInVehicle > pPrev.KillNumInVehicle)
                {
                    events.Add(new SimEvent(tick, "vehicle", p.UId, null, $"VEHICLE KILL popup - {p.PlayerName} ({p.TeamName})"));
                }
                else if (pCur.KillNum > pPrev.KillNum)
                {
                    events.Add(new SimEvent(tick, "kill", p.UId, null, $"{p.PlayerName} ({p.TeamName}) gets a kill"));
                }
                if (pCur.GotAirDropNum > pPrev.GotAirDropNum)
                {
                    events.Add(new SimEvent(tick, "airdrop", p.UId, null, $"AIRDROP popup - {p.PlayerName} ({p.TeamName}) loots an airdrop"));
                }
            }
            foreach (var (teamId, members) in teamMemberIds)
            {
                var aliveNow = cur.Players.Any(p => members.Contains(p.UId) && p.LiveState is >= 0 and <= 4);
                if (teamWasAlive[teamId] && !aliveNow)
                {
                    var teamName = seedPlayers.First(p => p.TeamId == teamId).TeamName;
                    events.Add(new SimEvent(tick, "teamEliminated", members.First(), null, $"TEAM ELIMINATED popup - {teamName}"));
                }
                teamWasAlive[teamId] = aliveNow;
            }
            var teamsAliveCount = teamMemberIds.Count(kv => cur.Players.Any(p => kv.Value.Contains(p.UId) && p.LiveState is >= 0 and <= 4));
            var teamsAlivePrevCount = teamMemberIds.Count(kv => prev.Players.Any(p => kv.Value.Contains(p.UId) && p.LiveState is >= 0 and <= 4));
            if (teamsAliveCount <= 4 && teamsAlivePrevCount > 4)
            {
                events.Add(new SimEvent(tick, "last4Switch", 0, null, "Live rankings hide, Last-4 bar appears top-middle"));
            }
        }
        events.Sort((a, b) => a.Tick.CompareTo(b.Tick));

        AssertInvariants(seedPlayers, frames, tickCount);

        return new BuiltMatch { MatchKey = matchKey, FinalPlayers = seedPlayers, Frames = frames, TickCount = tickCount, Events = events };
    }

    /// Task 3's invariants, asserted in code while building - throws with a clear message on
    /// violation rather than silently shipping a broken timeline.
    private static void AssertInvariants(List<SeedPlayer> seedPlayers, List<MatchFrameData> frames, int tickCount)
    {
        var byUId = seedPlayers.ToDictionary(p => p.UId);
        var prevHealthMax = new Dictionary<long, int>();
        var prevKill = new Dictionary<long, int>();
        var prevGrenade = new Dictionary<long, int>();
        var prevVehicle = new Dictionary<long, int>();
        var prevAirdrop = new Dictionary<long, int>();
        var prevDamage = new Dictionary<long, int>();
        var wasDead = new HashSet<long>();

        foreach (var frame in frames)
        {
            foreach (var pt in frame.Players)
            {
                if (prevHealthMax.TryGetValue(pt.UId, out var phm) && phm != pt.HealthMax)
                {
                    throw new InvalidOperationException($"healthMax changed for uId {pt.UId} at tick {frame.Tick}");
                }
                prevHealthMax[pt.UId] = pt.HealthMax;

                if (wasDead.Contains(pt.UId) && pt.LiveState != 5)
                {
                    throw new InvalidOperationException($"uId {pt.UId} came back from liveState 5 at tick {frame.Tick} (dead must stay dead)");
                }
                if (pt.LiveState == 5)
                {
                    if (pt.Health != 0) throw new InvalidOperationException($"uId {pt.UId} is dead but health={pt.Health} at tick {frame.Tick}");
                    wasDead.Add(pt.UId);
                }

                void CheckNonDecreasing(string name, Dictionary<long, int> prev, int value)
                {
                    if (prev.TryGetValue(pt.UId, out var pv) && value < pv)
                    {
                        throw new InvalidOperationException($"{name} decreased for uId {pt.UId} at tick {frame.Tick} ({pv} -> {value})");
                    }
                    prev[pt.UId] = value;
                }
                CheckNonDecreasing("killNum", prevKill, pt.KillNum);
                CheckNonDecreasing("killNumByGrenade", prevGrenade, pt.KillNumByGrenade);
                CheckNonDecreasing("killNumInVehicle", prevVehicle, pt.KillNumInVehicle);
                CheckNonDecreasing("gotAirDropNum", prevAirdrop, pt.GotAirDropNum);
                CheckNonDecreasing("damage", prevDamage, pt.Damage);
            }
        }

        var final = frames[tickCount];
        foreach (var pt in final.Players)
        {
            var p = byUId[pt.UId];
            if (pt.Health != p.Health || pt.LiveState != p.LiveState || pt.KillNum != p.KillNum
                || pt.KillNumByGrenade != p.KillNumByGrenade || pt.KillNumInVehicle != p.KillNumInVehicle
                || pt.GotAirDropNum != p.GotAirDropNum || pt.Damage != p.Damage || pt.Rank != p.Rank
                || pt.Location.X != p.Location.X || pt.Location.Y != p.Location.Y || pt.Location.Z != p.Location.Z)
            {
                throw new InvalidOperationException($"Final frame for uId {pt.UId} does not match the seed record exactly.");
            }
        }
    }
}
