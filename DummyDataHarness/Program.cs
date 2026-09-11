// Dummy-data verification harness for the Redis-removal / Top4 position-lock / survival-%
// fixes made in this pass. Run with `dotnet run` from this folder.
//
// This intentionally does NOT try to drive VmixGraphicsBusiness.LiveMatch.LiveStatsBusiness end
// to end - that class needs a live vMix instance, Hangfire, EF Core and a bunch of DI wiring, so
// a blind attempt at stubbing all of that without being able to compile/run it first would just
// be guessing. Instead this exercises the two pieces that actually matter for the bugs reported:
//
//   1. MatchStateStore's get/set/expire semantics, using the exact same call sequence
//      LiveStatsBusiness.top4.cs makes, across a simulated multi-tick match - proving positions
//      really do stay fixed once assigned, and really do clear on reset.
//   2. The survival/win-probability weighting formula (mirrored here from top4.cs; keep the two
//      in sync if you tune the weights), with dummy 16-team/64-player data covering the exact
//      case that used to be wrong: a team with several knocked-out players.

using VmixGraphicsBusiness.Utils;

var failures = 0;
failures += RunTest("Top4 position lock stays stable across ticks", TestPositionLockStability);
failures += RunTest("ResetMatchState clears position locks", TestResetClearsPositions);
failures += RunTest("Survival score penalizes knocked players correctly", TestSurvivalScoreWeighting);
failures += RunTest("64-player / 16-team dummy data generates a sane match", TestDummyDataShape);

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : $"{failures} TEST(S) FAILED");
Environment.Exit(failures == 0 ? 0 : 1);

static int RunTest(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"[PASS] {name}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL] {name}: {ex.Message}");
        return 1;
    }
}

// ---------- Test 1: position lock stability ----------

static void TestPositionLockStability()
{
    var tmpDir = Path.Combine(Path.GetTempPath(), "vmix_harness_" + Guid.NewGuid());
    var store = new MatchStateStore(tmpDir);

    // 16 teams narrow down to exactly 4 live teams: A=3, B=7, C=11, D=16 (arbitrary PUBG-style
    // per-match team IDs). Everyone else is eliminated (liveMemberNum == 0).
    var liveTeamIds = new[] { 3, 7, 11, 16 };

    Dictionary<int, int>? lockedPositions = null;

    for (int tick = 0; tick < 50; tick++)
    {
        // This mirrors LiveStatsBusiness.top4.cs's CreateTop4LiveRanking logic exactly.
        var stored = AsyncResult(store.StringGetAsync("Top4TeamPositions"));
        Dictionary<int, int> positions;

        if (string.IsNullOrEmpty(stored))
        {
            positions = new Dictionary<int, int>();
            for (int i = 0; i < liveTeamIds.Length; i++)
                positions[liveTeamIds[i]] = i + 1;

            AsyncWait(store.SetAsync("Top4TeamPositions", positions, TimeSpan.FromMinutes(15)));
        }
        else
        {
            positions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(stored)!;
            AsyncWait(store.KeyExpireAsync("Top4TeamPositions", TimeSpan.FromMinutes(15)));
        }

        if (lockedPositions == null)
        {
            lockedPositions = positions;
        }
        else
        {
            foreach (var (teamId, position) in lockedPositions)
            {
                if (!positions.TryGetValue(teamId, out var currentPosition) || currentPosition != position)
                    throw new Exception($"Team {teamId} moved from position {position} to {(positions.TryGetValue(teamId, out var p) ? p.ToString() : "MISSING")} on tick {tick} - this is the swap bug.");
            }
        }
    }

    store.Dispose();
    Directory.Delete(tmpDir, recursive: true);
}

static void TestResetClearsPositions()
{
    var tmpDir = Path.Combine(Path.GetTempPath(), "vmix_harness_" + Guid.NewGuid());
    var store = new MatchStateStore(tmpDir);

    AsyncWait(store.SetAsync("Top4TeamPositions", new Dictionary<int, int> { { 3, 1 }, { 7, 2 } }, TimeSpan.FromMinutes(15)));
    AsyncWait(store.StringSetAsync($"{HelperRedis.isEliminated}:3", "abc"));

    var beforeReset = AsyncResult(store.StringGetAsync("Top4TeamPositions"));
    if (string.IsNullOrEmpty(beforeReset)) throw new Exception("expected positions to be set before reset");

    store.ResetMatchState();

    var afterReset = AsyncResult(store.StringGetAsync("Top4TeamPositions"));
    if (!string.IsNullOrEmpty(afterReset)) throw new Exception("Top4TeamPositions should be empty after ResetMatchState - this is the cross-match leak bug.");

    var eliminationFlag = AsyncResult(store.StringGetAsync($"{HelperRedis.isEliminated}:3"));
    if (!string.IsNullOrEmpty(eliminationFlag)) throw new Exception("elimination flag should be cleared after ResetMatchState.");

    store.Dispose();
    Directory.Delete(tmpDir, recursive: true);
}

// ---------- Test 2: survival score weighting ----------
// Mirrors the scoring in LiveStatsBusiness.top4.cs's CreateTop4LiveRanking. If you change the
// weights there, update this copy too so the test still means something.

const float KnockedCombatWeight = 0.15f;

static double AverageTeamHealth(IEnumerable<(int liveState, float healthPercent)> players)
{
    double weighted = 0;
    int counted = 0;
    foreach (var (liveState, healthPercent) in players)
    {
        if (liveState is >= 0 and <= 3) { weighted += healthPercent; counted++; }
        else if (liveState == 4) { weighted += healthPercent * KnockedCombatWeight; counted++; }
    }
    return counted > 0 ? weighted / counted : 0;
}

static void TestSurvivalScoreWeighting()
{
    // Team Alpha: 3 players knocked at 100% (as-knocked) health, 1 standing at 100% health.
    var teamAlpha = new (int, float)[] { (4, 100f), (4, 100f), (4, 100f), (0, 100f) };
    // Team Bravo: all 4 players standing, each at 60% health.
    var teamBravo = new (int, float)[] { (0, 60f), (0, 60f), (0, 60f), (0, 60f) };

    var alphaHealth = AverageTeamHealth(teamAlpha);
    var bravoHealth = AverageTeamHealth(teamBravo);

    Console.WriteLine($"    Alpha (3 knocked + 1 healthy) weighted health: {alphaHealth:F1}");
    Console.WriteLine($"    Bravo (4 standing at 60%) weighted health:      {bravoHealth:F1}");

    // Before the fix, Alpha's average would have been 100 (only the one standing player counted)
    // - a mostly-wiped team reading as MORE healthy than a fully-standing-but-injured team. The
    // fix must produce the opposite: Alpha's real fighting strength is clearly worse than Bravo's.
    if (alphaHealth >= bravoHealth)
        throw new Exception($"a team with 3 knocked players (weighted {alphaHealth:F1}) should score below a team with all 4 standing at 60% (weighted {bravoHealth:F1}), but didn't - the knocked-player fix isn't working.");

    // A fully-dead-filtered team (no alive/knocked players passed in at all) must not divide by zero.
    var noOne = Array.Empty<(int, float)>();
    var emptyHealth = AverageTeamHealth(noOne);
    if (emptyHealth != 0) throw new Exception("empty team should score 0, not throw or return a nonzero value.");
}

// ---------- Test 3: dummy data shape ----------

static void TestDummyDataShape()
{
    var random = new Random(42);
    var teams = GenerateDummyMatch(random);

    if (teams.Count != 16) throw new Exception($"expected 16 teams, got {teams.Count}");
    var totalPlayers = teams.Sum(t => t.Players.Count);
    if (totalPlayers != 64) throw new Exception($"expected 64 players total, got {totalPlayers}");
    foreach (var team in teams)
    {
        if (team.Players.Count != 4) throw new Exception($"team {team.TeamId} has {team.Players.Count} players, expected 4");
    }

    Console.WriteLine($"    Generated {teams.Count} teams / {totalPlayers} players. Sample: Team {teams[0].TeamId} - {string.Join(", ", teams[0].Players.Select(p => p.PlayerName))}");
}

static List<DummyTeam> GenerateDummyMatch(Random random)
{
    var teams = new List<DummyTeam>();
    for (int teamId = 1; teamId <= 16; teamId++)
    {
        var players = new List<DummyPlayer>();
        for (int slot = 1; slot <= 4; slot++)
        {
            players.Add(new DummyPlayer(
                PlayerName: $"Team{teamId}_Player{slot}",
                TeamId: teamId,
                Health: random.Next(0, 101),
                HealthMax: 100,
                LiveState: random.Next(0, 6), // 0-3 alive, 4 knocked, 5 dead
                KillNum: random.Next(0, 6)
            ));
        }
        teams.Add(new DummyTeam(
            TeamId: teamId,
            Players: players,
            KillNum: players.Sum(p => p.KillNum),
            LiveMemberNum: players.Count(p => p.LiveState != 5)
        ));
    }
    return teams;
}

// ---------- small helpers to call the store's async API synchronously in this console script ----------

static T AsyncResult<T>(Task<T> task) => task.GetAwaiter().GetResult();
static void AsyncWait(Task task) => task.GetAwaiter().GetResult();

record DummyPlayer(string PlayerName, int TeamId, int Health, int HealthMax, int LiveState, int KillNum);
record DummyTeam(int TeamId, List<DummyPlayer> Players, int KillNum, int LiveMemberNum);
