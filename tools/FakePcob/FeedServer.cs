using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FakePcob;

/// Mutable shared state the clock loop and the HTTP handlers both touch. One instance per running
/// `serve` process.
public sealed class SimState
{
    public required Scenario Scenario;
    public RouteMode Routes;
    public int Tick;
    public bool Paused;
    public double Speed = 1;
    public readonly object Lock = new();
    public string? PushUrl;
    public static readonly HttpClient PushClient = new() { Timeout = TimeSpan.FromSeconds(3) };

    public bool InGame => Tick < Scenario.Match.TickCount;
}

/// Task 6 - the actual fake PCOB HTTP server. Recon Task 1 found the app only ever POLLS pcob
/// with GET requests (no listener on the app side to push into) - so `serve` is a plain HTTP
/// server on the paths the app already calls, with zero app-side changes needed. `--push` is kept
/// as an optional extra (post each tick's player-list JSON to a URL) purely for flexibility; it is
/// not required by anything found in Recon.
public static class ServeCommand
{
    public static async Task<int> RunAsync(Args opts)
    {
        BuiltMatch match;
        string matchKey;
        if (opts.Has("replay"))
        {
            var folder = opts.Get("replay", "");
            match = ReplayLoader.Load(folder);
            matchKey = "replay:" + folder;
        }
        else
        {
            matchKey = opts.Get("match", "m1");
            var seed = opts.GetInt("seed", 42);
            match = MatchBuilder.Build(matchKey, seed);
        }

        var scenarioMode = opts.Get("scenario", "replay");
        var scenarioSeed = opts.GetInt("seed", 42);
        var scenario = ScenarioBuilder.Build(match, scenarioMode, scenarioSeed);

        var routes = opts.Get("routes", "split").Equals("merged", StringComparison.OrdinalIgnoreCase) ? RouteMode.Merged : RouteMode.Split;
        var port = opts.GetInt("port", 10086);
        var speed = opts.GetDouble("speed", 1.0);
        var paused = opts.Has("paused");
        var pushUrl = opts.GetOrNull("push");

        var state = new SimState { Scenario = scenario, Routes = routes, Speed = speed, Paused = paused, PushUrl = pushUrl };

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var app = builder.Build();

        MapPcobRoutes(app, state);
        MapControlRoutes(app, state);
        app.MapGet("/_sim", () => Results.Content(SimPage.Html, "text/html"));
        app.MapFallback(ctx =>
        {
            Console.WriteLine($"UNKNOWN PATH {ctx.Request.Method} {ctx.Request.Path}{ctx.Request.QueryString}");
            ctx.Response.StatusCode = 404;
            return Task.CompletedTask;
        });

        Console.WriteLine($"FakePcob serving on http://127.0.0.1:{port}/  (match={matchKey}, scenario={scenarioMode}, routes={routes}, ticks=0..{match.TickCount})");
        Console.WriteLine($"Control page: http://127.0.0.1:{port}/_sim");
        _ = RunClockAsync(state);

        await app.RunAsync();
        return 0;
    }

    private static void MapPcobRoutes(WebApplication app, SimState state)
    {
        app.MapGet("/gettotalplayerlist", () =>
        {
            lock (state.Lock)
            {
                return Results.Content(FeedResponses.BuildPlayerListJson(state.Scenario.Match, state.Tick, state.Routes), "application/json");
            }
        });
        app.MapGet("/getteaminfolist", () =>
        {
            lock (state.Lock)
            {
                return Results.Content(FeedResponses.BuildTeamListJson(state.Scenario.Match, state.Tick, state.Routes), "application/json");
            }
        });
        app.MapGet("/getcircleinfo", () =>
        {
            lock (state.Lock)
            {
                var recorded = RecordedBody(state, "getcircleinfo");
                return Results.Content(recorded ?? FeedResponses.BuildCircleInfoJson(state.Scenario.Circle.At(state.Tick)), "application/json");
            }
        });
        // Replays of a real capture serve the recorded kill feed / backpack / observer in step
        // with the clock; built matches keep the old empty responses.
        app.MapGet("/getkillinfo", () =>
        {
            lock (state.Lock)
            {
                return Results.Content(RecordedBody(state, "getkillinfo") ?? "{\"killInfo\":[]}", "application/json");
            }
        });
        app.MapGet("/getteambackpackinfo", () =>
        {
            lock (state.Lock)
            {
                return Results.Content(RecordedBody(state, "getteambackpackinfo") ?? "{\"teambackpackinfo\":{\"TeamBackPackList\":[]}}", "application/json");
            }
        });
        app.MapGet("/getobservingplayer", () =>
        {
            lock (state.Lock)
            {
                return Results.Content(RecordedBody(state, "getobservingplayer") ?? "{\"observingPlayer\":{}}", "application/json");
            }
        });
        app.MapGet("/isingame", () =>
        {
            lock (state.Lock)
            {
                return Results.Content(FeedResponses.BuildIsInGameJson(state.InGame), "application/json");
            }
        });
    }

    /// Recorded body for this replay tick, or null (built match, or nothing recorded yet).
    /// Caller holds state.Lock.
    private static string? RecordedBody(SimState state, string endpoint)
    {
        var match = state.Scenario.Match;
        if (match.Recorded is null || match.FrameUnixMs is null || match.FrameUnixMs.Count == 0) return null;
        var i = Math.Clamp(state.Tick, 0, match.FrameUnixMs.Count - 1);
        return match.Recorded.BodyAt(endpoint, match.FrameUnixMs[i]);
    }

    private static void MapControlRoutes(WebApplication app, SimState state)
    {
        app.MapGet("/_sim/status", () =>
        {
            lock (state.Lock)
            {
                var upcoming = state.Scenario.ExpectedEvents.Where(e => e.Tick >= state.Tick).Take(6)
                    .Select(e => new { e.Tick, e.Text });
                var current = state.Scenario.ExpectedEvents.LastOrDefault(e => e.Tick <= state.Tick);
                return Results.Json(new
                {
                    tick = state.Tick,
                    tickCount = state.Scenario.Match.TickCount,
                    paused = state.Paused,
                    speed = state.Speed,
                    inGame = state.InGame,
                    matchKey = state.Scenario.Match.MatchKey,
                    routes = state.Routes.ToString(),
                    currentExpected = current?.Text,
                    upcoming,
                });
            }
        });
        app.MapPost("/_sim/pause", () => { lock (state.Lock) { state.Paused = true; } return Results.Ok(); });
        app.MapPost("/_sim/resume", () => { lock (state.Lock) { state.Paused = false; } return Results.Ok(); });
        app.MapPost("/_sim/step", () =>
        {
            lock (state.Lock)
            {
                state.Tick = Math.Min(state.Scenario.Match.TickCount, state.Tick + 1);
            }
            return Results.Ok();
        });
        app.MapPost("/_sim/restart", () => { lock (state.Lock) { state.Tick = 0; } return Results.Ok(); });
        app.MapPost("/_sim/speed", (HttpRequest req) =>
        {
            if (double.TryParse(req.Query["x"], out var x) && x > 0)
            {
                lock (state.Lock) { state.Speed = x; }
            }
            return Results.Ok();
        });
        app.MapPost("/_sim/jump", (HttpRequest req) =>
        {
            if (int.TryParse(req.Query["tick"], out var t))
            {
                lock (state.Lock) { state.Tick = Math.Clamp(t, 0, state.Scenario.Match.TickCount); }
            }
            return Results.Ok();
        });
    }

    private static async Task RunClockAsync(SimState state)
    {
        while (true)
        {
            double speed;
            lock (state.Lock) { speed = Math.Max(0.05, state.Speed); }
            var delayMs = (int)(2000 / speed);
            await Task.Delay(delayMs);

            int tick;
            bool paused;
            lock (state.Lock)
            {
                paused = state.Paused;
                if (!paused && state.Tick < state.Scenario.Match.TickCount)
                {
                    state.Tick++;
                }
                tick = state.Tick;
            }
            if (!paused)
            {
                LogTick(state, tick);
                await PushIfConfiguredAsync(state, tick);
            }
        }
    }

    private static void LogTick(SimState state, int tick)
    {
        var frame = state.Scenario.Match.Frames[tick];
        var aliveTeams = state.Scenario.Match.FinalPlayers.GroupBy(p => p.TeamId)
            .Count(g => frame.Players.Any(p => g.Select(x => x.UId).Contains(p.UId) && p.LiveState is >= 0 and <= 4));
        var alivePlayers = frame.Players.Count(p => p.LiveState is >= 0 and <= 4);
        var mm = (tick * 2) / 60;
        var ss = (tick * 2) % 60;
        Console.WriteLine($"[T{tick:D3} {mm:D2}:{ss:D2}] alive {aliveTeams} teams / {alivePlayers} players");
        foreach (var e in state.Scenario.ExpectedEvents.Where(e => e.Tick == tick))
        {
            Console.WriteLine($">>> EXPECT: {e.Text}");
        }
    }

    private static async Task PushIfConfiguredAsync(SimState state, int tick)
    {
        if (string.IsNullOrWhiteSpace(state.PushUrl)) return;
        try
        {
            var json = FeedResponses.BuildPlayerListJson(state.Scenario.Match, tick, state.Routes);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            await SimState.PushClient.PostAsync(state.PushUrl, content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[push] failed: {ex.Message}");
        }
    }
}
