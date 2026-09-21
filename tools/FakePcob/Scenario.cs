namespace FakePcob;

public sealed record ExpectedEvent(int Tick, string Text);

/// A ready-to-use scenario: the underlying reconstructed match, a circle timeline with explicit
/// closing phases, and the ordered list of things a human should see on screen while it plays.
public sealed class Scenario
{
    public required BuiltMatch Match { get; init; }
    public required CircleTimeline Circle { get; init; }
    public required List<ExpectedEvent> ExpectedEvents { get; init; }
    public required string Mode { get; init; } // "replay" | "scripted" | "random"
}

/// Task 5 - overlays a guaranteed beat list on top of a real reverse-replayed match so every
/// graphic is exercised even if the real match did not happen to produce every achievement. Never
/// invents match data: it only (a) builds an explicit circle-closing timeline (the real
/// getcircleinfo feed has no equivalent in the seed captures - Recon Task 1 §2 - so this is the
/// one thing that was always going to be synthesized) and (b) picks *which* of the real,
/// already-reconstructed events (Task 3's diff-derived kill/grenade/vehicle/airdrop/team-eliminated
/// list) get called out in the checklist. It never reorders ticks or changes counters - doing that
/// would violate Task 3's "counters never decrease" / "final frame equals the seed" invariants.
public static class ScenarioBuilder
{
    public static Scenario Build(BuiltMatch match, string mode, int seed)
    {
        var rng = new Random(seed);
        var circle = BuildCircleTimeline(match.TickCount, rng, phaseCountOverride: mode == "random" ? 6 + rng.Next(0, 3) : 8);
        var events = new List<ExpectedEvent>();

        events.Add(new ExpectedEvent(0, "Plane -> parachute -> drop: live rankings show, all teams 4/4"));
        events.Add(new ExpectedEvent(Math.Min(match.TickCount, 6), "Idle drift: health bars move every update"));

        // First Blood is always the earliest kill-type event in the real timeline (never moved).
        var killLike = match.Events.Where(e => e.Kind is "kill" or "grenade" or "vehicle").OrderBy(e => e.Tick).ToList();
        if (killLike.Count > 0)
        {
            var fb = killLike[0];
            var player = match.FinalPlayers.First(p => p.UId == fb.ActorUId);
            events.Add(new ExpectedEvent(fb.Tick, $"FIRST BLOOD popup - {player.PlayerName} ({player.TeamName})"));
        }

        // Second grenade kill by the same player, if this seed's real distribution has one -
        // otherwise this is a data-availability gap, not a bug (see REPORT.md Decisions): the
        // simulator never invents an extra grenade kill because that would push a counter above
        // the seed's real total and violate the "never decrease before the final tick" invariant.
        var grenadeByPlayer = match.Events.Where(e => e.Kind == "grenade").GroupBy(e => e.ActorUId).ToList();
        var repeatGrenadier = grenadeByPlayer.FirstOrDefault(g => g.Count() >= 2);
        foreach (var g in match.Events.Where(e => e.Kind == "grenade"))
        {
            var player = match.FinalPlayers.First(p => p.UId == g.ActorUId);
            var isSecondForSamePlayer = repeatGrenadier is not null && repeatGrenadier.Key == g.ActorUId
                && repeatGrenadier.OrderBy(e => e.Tick).Skip(1).First().Tick == g.Tick;
            var label = isSecondForSamePlayer ? "GRENADE ELIMINATION popup (again) - " : "GRENADE ELIMINATION popup - ";
            events.Add(new ExpectedEvent(g.Tick, label + $"{player.PlayerName} ({player.TeamName})"));
        }
        foreach (var v in match.Events.Where(e => e.Kind == "vehicle"))
        {
            var player = match.FinalPlayers.First(p => p.UId == v.ActorUId);
            events.Add(new ExpectedEvent(v.Tick, $"VEHICLE KILL popup - {player.PlayerName} ({player.TeamName})"));
        }
        foreach (var a in match.Events.Where(e => e.Kind == "airdrop"))
        {
            var player = match.FinalPlayers.First(p => p.UId == a.ActorUId);
            events.Add(new ExpectedEvent(a.Tick, $"AIRDROP popup - {player.PlayerName} ({player.TeamName})"));
        }

        // Kill/damage domination: soft expectations. FakePcob only has to serve rising
        // killNum/damage counters - whether the app's own margin-based domination logic in
        // LiveStatsBusiness.SetPlayerAcheivments.cs actually fires is up to that app code, which
        // this harness does not reimplement (Task 9-11's scope is the overlay, not the achievement
        // thresholds). Flagged here so REPORT.md's "predicted failures" can call out the ones that
        // never fire so it is obvious that is expected, not a bug in FakePcob.
        var lastFrame = match.FinalFrame;
        var topKiller = lastFrame.Players.OrderByDescending(p => p.KillNum).First();
        var topDamage = lastFrame.Players.OrderByDescending(p => p.Damage).First();
        var midTick = match.TickCount * 3 / 4;
        events.Add(new ExpectedEvent(midTick, $"(expected, app-dependent) Kill Domination popup once {topKiller.PlayerName}'s kill lead clears the app's margin"));
        events.Add(new ExpectedEvent(midTick, $"(expected, app-dependent) Damage Domination popup once {topDamage.PlayerName}'s damage lead clears the app's margin"));

        foreach (var te in match.Events.Where(e => e.Kind == "teamEliminated"))
        {
            events.Add(new ExpectedEvent(te.Tick, te.Text));
        }

        // A team down to exactly 1 alive, and a team with every remaining member knocked -
        // called out wherever they actually occur in the real reconstructed timeline, so the
        // checklist tells a QA person whether to expect them this run rather than assuming they
        // always will.
        var oneAliveTick = FindTick(match, frame => TeamAliveCounts(match, frame).Any(c => c == 1));
        if (oneAliveTick is { } t1) events.Add(new ExpectedEvent(t1, "A team is down to exactly 1 alive member - alive count must read 1, team must not show as eliminated"));
        var allKnockedTick = FindTick(match, frame => AnyTeamFullyKnocked(match, frame));
        if (allKnockedTick is { } t2) events.Add(new ExpectedEvent(t2, "A team has every remaining member knocked (liveState 4) - must still count as alive, not eliminated"));

        foreach (var l4 in match.Events.Where(e => e.Kind == "last4Switch"))
        {
            events.Add(new ExpectedEvent(l4.Tick, l4.Text));
            // "then one more eliminated -> bar shows 3; others must not change position" etc. are
            // exactly the teamEliminated events already queued above, once alive count is <=4.
        }

        events.Add(new ExpectedEvent(match.TickCount, "Match-finished signal: after-match fields (knockouts, assists, headshots, survivalTime, ...) populate with real values"));

        events = events.OrderBy(e => e.Tick).ToList();
        if (mode == "random")
        {
            // Shuffle only the presentation order of same-tick filler lines (idle drift wording
            // etc.) - ticks themselves are never reordered, since they are load-bearing timeline
            // positions from the real reconstructed match.
            events = events
                .GroupBy(e => e.Tick)
                .OrderBy(g => g.Key)
                .SelectMany(g => g.OrderBy(_ => rng.Next()))
                .ToList();
        }

        return new Scenario { Match = match, Circle = circle, ExpectedEvents = events, Mode = mode };
    }

    private static IEnumerable<int> TeamAliveCounts(BuiltMatch match, MatchFrameData frame)
    {
        foreach (var team in match.FinalPlayers.GroupBy(p => p.TeamId))
        {
            var ids = team.Select(p => p.UId).ToHashSet();
            yield return frame.Players.Count(p => ids.Contains(p.UId) && p.LiveState is >= 0 and <= 4);
        }
    }

    private static bool AnyTeamFullyKnocked(BuiltMatch match, MatchFrameData frame)
    {
        foreach (var team in match.FinalPlayers.GroupBy(p => p.TeamId))
        {
            var ids = team.Select(p => p.UId).ToHashSet();
            var alive = frame.Players.Where(p => ids.Contains(p.UId) && p.LiveState is >= 0 and <= 4).ToList();
            if (alive.Count > 0 && alive.All(p => p.LiveState == 4)) return true;
        }
        return false;
    }

    private static int? FindTick(BuiltMatch match, Func<MatchFrameData, bool> predicate)
    {
        foreach (var frame in match.Frames)
        {
            if (predicate(frame)) return frame.Tick;
        }
        return null;
    }

    /// An explicit closing-phase circle timeline (Recon Task 1 §2/§7 - getcircleinfo has no seed
    /// data at all, so unlike everything else in this file, this really is synthesized rather than
    /// derived). Guarantees at least two full closing phases regardless of match length.
    private static CircleTimeline BuildCircleTimeline(int tickCount, Random rng, int phaseCountOverride)
    {
        var phaseCount = Math.Max(2, phaseCountOverride);
        var states = new List<CircleState>(tickCount + 1);
        var phaseLen = Math.Max(6, (tickCount + 1) / phaseCount);
        for (int tick = 0; tick <= tickCount; tick++)
        {
            var phase = Math.Min(phaseCount - 1, tick / phaseLen);
            var intoPhase = tick - phase * phaseLen;
            var closingStart = Math.Max(0, phaseLen - 10);
            if (intoPhase >= closingStart && phase < phaseCount)
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
