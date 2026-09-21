using System.Text;
using System.Text.Json.Nodes;

namespace FakePcob;

/// Task 8 - `dump --match m1 --out frames`: writes every tick's full payload set to disk plus a
/// human checklist, with no server involved - useful for diffing two runs or reviewing a match
/// without standing up `serve`.
public static class DumpCommand
{
    public static Task<int> RunAsync(Args opts)
    {
        var matchKey = opts.Get("match", "m1");
        var seed = opts.GetInt("seed", 42);
        var scenarioMode = opts.Get("scenario", "replay");
        var routes = opts.Get("routes", "split").Equals("merged", StringComparison.OrdinalIgnoreCase) ? RouteMode.Merged : RouteMode.Split;
        var outDir = opts.Get("out", "frames");

        var match = MatchBuilder.Build(matchKey, seed);
        var scenario = ScenarioBuilder.Build(match, scenarioMode, seed);

        Directory.CreateDirectory(outDir);
        for (int tick = 0; tick <= match.TickCount; tick++)
        {
            var payload = new JsonObject
            {
                ["tick"] = tick,
                ["gettotalplayerlist"] = JsonNode.Parse(FeedResponses.BuildPlayerListJson(match, tick, routes)),
                ["getteaminfolist"] = JsonNode.Parse(FeedResponses.BuildTeamListJson(match, tick, routes)),
                ["getcircleinfo"] = JsonNode.Parse(FeedResponses.BuildCircleInfoJson(scenario.Circle.At(tick))),
                ["isingame"] = JsonNode.Parse(FeedResponses.BuildIsInGameJson(tick < match.TickCount)),
            };
            File.WriteAllText(Path.Combine(outDir, $"frame_{tick:D3}.json"), payload.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# EXPECTED - {matchKey} ({scenarioMode}, routes={routes})");
        sb.AppendLine();
        sb.AppendLine("| tick | time @1x | expected on screen | pass |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var e in scenario.ExpectedEvents.OrderBy(e => e.Tick))
        {
            var mm = (e.Tick * 2) / 60;
            var ss = (e.Tick * 2) % 60;
            sb.AppendLine($"| {e.Tick} | {mm:D2}:{ss:D2} | {e.Text.Replace("|", "\\|")} | ☐ |");
        }
        File.WriteAllText(Path.Combine(outDir, "EXPECTED.md"), sb.ToString());

        Console.WriteLine($"Wrote {match.TickCount + 1} frame files and EXPECTED.md to {outDir}/");
        return Task.FromResult(0);
    }
}
