using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FakePcob;

/// One logged call, written to recordings/<folder>/index.ndjson (Task 7).
public sealed record RecordIndexEntry(int Seq, long UnixMs, string Endpoint, string Query, int Status, int Bytes, int LatencyMs);

/// Task 7 - `record --upstream <real-pcob>`: a pass-through proxy that saves a real match to disk
/// for later replay, without ever risking the live broadcast pipeline it sits in front of.
public static class RecordCommand
{
    private static readonly string[] KnownPaths = { "gettotalplayerlist", "getteaminfolist", "getcircleinfo", "getkillinfo", "isingame" };

    public static async Task<int> RunAsync(Args opts)
    {
        var upstream = opts.GetOrNull("upstream");
        if (string.IsNullOrWhiteSpace(upstream))
        {
            Console.WriteLine("record requires --upstream http://<real-pcob-ip>:<port>");
            return 1;
        }
        if (!upstream.EndsWith('/')) upstream += "/";
        var port = opts.GetInt("port", 10086);

        var folder = Path.Combine(FindRecordingsRoot(), DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);
        Console.WriteLine($"Recording to: {folder}");
        Console.WriteLine("WARNING: test PC only - never point a real broadcast's pcobUrl at this in the production path.");

        var writer = new BackgroundIndexWriter(folder);
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var seq = 0;
        var seqLock = new object();

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var app = builder.Build();

        foreach (var path in KnownPaths)
        {
            var localPath = path;
            app.MapGet("/" + localPath, async (HttpContext ctx) =>
            {
                int mySeq;
                lock (seqLock) { mySeq = seq++; }
                var started = DateTime.UtcNow;
                var query = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value! : "";
                try
                {
                    using var response = await http.GetAsync(upstream + localPath + query);
                    var body = await response.Content.ReadAsByteArrayAsync();
                    var latency = (int)(DateTime.UtcNow - started).TotalMilliseconds;

                    ctx.Response.StatusCode = (int)response.StatusCode;
                    ctx.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
                    await ctx.Response.Body.WriteAsync(body); // byte-for-byte, never parsed here

                    writer.Enqueue(localPath, query, (int)response.StatusCode, body, mySeq, latency);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[record] {localPath} failed: {ex.Message}");
                    ctx.Response.StatusCode = 502;
                }
            });
        }
        app.MapFallback(ctx =>
        {
            Console.WriteLine($"UNKNOWN PATH {ctx.Request.Method} {ctx.Request.Path}{ctx.Request.QueryString}");
            ctx.Response.StatusCode = 404;
            return Task.CompletedTask;
        });

        Console.WriteLine($"Recording proxy listening on http://127.0.0.1:{port}/ -> {upstream}");
        await app.RunAsync();
        return 0;
    }

    private static string FindRecordingsRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir, "FakePcob.csproj"))) return Path.Combine(dir, "recordings");
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }
        return Path.Combine(Directory.GetCurrentDirectory(), "recordings");
    }

    /// Writes recorded bodies and the ndjson index off the response path: a queue plus one
    /// background drain loop, so a slow disk can never delay the byte-for-byte response the proxy
    /// just forwarded.
    private sealed class BackgroundIndexWriter
    {
        private readonly string _folder;
        private readonly System.Collections.Concurrent.BlockingCollection<(string path, string query, int status, byte[] body, int seq, int latency)> _queue = new();

        public BackgroundIndexWriter(string folder)
        {
            _folder = folder;
            var thread = new Thread(Drain) { IsBackground = true };
            thread.Start();
        }

        public void Enqueue(string path, string query, int status, byte[] body, int seq, int latency)
            => _queue.Add((path, query, status, body, seq, latency));

        private void Drain()
        {
            var indexPath = Path.Combine(_folder, "index.ndjson");
            foreach (var item in _queue.GetConsumingEnumerable())
            {
                try
                {
                    var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var dir = Path.Combine(_folder, item.path);
                    Directory.CreateDirectory(dir);
                    var file = Path.Combine(dir, $"{item.seq:D5}_{unixMs}.json");
                    File.WriteAllBytes(file, item.body);

                    var entry = new RecordIndexEntry(item.seq, unixMs, item.path, item.query, item.status, item.body.Length, item.latency);
                    File.AppendAllText(indexPath, JsonSerializer.Serialize(entry) + "\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[record] disk write failed (response already sent, no impact): {ex.Message}");
                }
            }
        }
    }
}

/// Task 7 - `serve --replay <folder>`: rebuilds a BuiltMatch from a `record` capture. Live-group
/// fields come straight from the recorded gettotalplayerlist bodies (same field shapes as a seed
/// file, since both are real pcob output); after-match fields are best-effort (only meaningful if
/// the last recorded tick happened to be post-match) - documented in REPORT.md rather than
/// silently assumed.
public static class ReplayLoader
{
    public static BuiltMatch Load(string folder)
    {
        var indexPath = File.Exists(Path.Combine(folder, "index.ndjson"))
            ? Path.Combine(folder, "index.ndjson")
            : throw new FileNotFoundException($"No index.ndjson under '{folder}'. Point --replay at a folder created by `record`.");

        var entries = File.ReadAllLines(indexPath)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonSerializer.Deserialize<RecordIndexEntry>(l)!)
            .Where(e => e.Endpoint == "gettotalplayerlist" && e.Status is >= 200 and < 300)
            .OrderBy(e => e.Seq)
            .ToList();

        if (entries.Count == 0)
        {
            throw new InvalidDataException($"'{folder}' has no successful gettotalplayerlist captures to replay.");
        }

        var frames = new List<MatchFrameData>();
        List<SeedPlayer>? lastPlayers = null;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var file = Directory.GetFiles(Path.Combine(folder, "gettotalplayerlist"), $"{e.Seq:D5}_*.json").FirstOrDefault();
            if (file is null) continue;
            var envelope = JsonSerializer.Deserialize<SeedEnvelope>(File.ReadAllText(file));
            if (envelope is null || envelope.PlayerInfoList.Count == 0) continue;
            lastPlayers = envelope.PlayerInfoList;

            var frame = new MatchFrameData { Tick = frames.Count };
            foreach (var p in envelope.PlayerInfoList)
            {
                frame.Players.Add(new PlayerTick
                {
                    UId = p.UId, PlayerName = p.PlayerName, TeamId = p.TeamId, TeamName = p.TeamName,
                    Location = p.Location, Health = p.Health, HealthMax = p.HealthMax, LiveState = p.LiveState,
                    KillNum = p.KillNum, KillNumBeforeDie = p.KillNumBeforeDie, GotAirDropNum = p.GotAirDropNum,
                    MaxKillDistance = p.MaxKillDistance, Damage = p.Damage, KillNumInVehicle = p.KillNumInVehicle,
                    KillNumByGrenade = p.KillNumByGrenade, Rank = p.Rank, IsOutsideBlueCircle = p.IsOutsideBlueCircle,
                });
            }
            frames.Add(frame);
        }

        if (lastPlayers is null)
        {
            throw new InvalidDataException($"'{folder}' had entries in index.ndjson but none of the body files could be read.");
        }

        var events = DetectEvents(lastPlayers, frames);
        return new BuiltMatch
        {
            MatchKey = "replay",
            FinalPlayers = lastPlayers,
            Frames = frames,
            TickCount = frames.Count - 1,
            Events = events,
        };
    }

    /// Same diff-based detection MatchBuilder uses internally, duplicated here (not shared)
    /// because a recorded replay's frames come from real HTTP captures, not the reconstruction
    /// engine, and keeping the two independent means a bug in one can't silently hide in the
    /// other's code path.
    private static List<SimEvent> DetectEvents(List<SeedPlayer> players, List<MatchFrameData> frames)
    {
        var events = new List<SimEvent>();
        var reportedDeath = new HashSet<long>();
        var teamMemberIds = players.GroupBy(p => p.TeamId).ToDictionary(g => g.Key, g => g.Select(p => p.UId).ToHashSet());
        var teamWasAlive = teamMemberIds.Keys.ToDictionary(id => id, _ => true);

        for (int tick = 0; tick < frames.Count; tick++)
        {
            var cur = frames[tick];
            foreach (var p in cur.Players.Where(p => p.LiveState == 5 && reportedDeath.Add(p.UId)))
            {
                events.Add(new SimEvent(tick, "death", p.UId, null, $"{p.PlayerName} ({p.TeamName}) is eliminated"));
            }
            if (tick > 0)
            {
                var prev = frames[tick - 1];
                foreach (var p in players)
                {
                    var pPrev = prev.Players.FirstOrDefault(x => x.UId == p.UId);
                    var pCur = cur.Players.FirstOrDefault(x => x.UId == p.UId);
                    if (pPrev is null || pCur is null) continue;
                    if (pCur.KillNumByGrenade > pPrev.KillNumByGrenade)
                        events.Add(new SimEvent(tick, "grenade", p.UId, null, $"GRENADE ELIMINATION popup - {p.PlayerName} ({p.TeamName})"));
                    else if (pCur.KillNumInVehicle > pPrev.KillNumInVehicle)
                        events.Add(new SimEvent(tick, "vehicle", p.UId, null, $"VEHICLE KILL popup - {p.PlayerName} ({p.TeamName})"));
                    else if (pCur.KillNum > pPrev.KillNum)
                        events.Add(new SimEvent(tick, "kill", p.UId, null, $"{p.PlayerName} ({p.TeamName}) gets a kill"));
                    if (pCur.GotAirDropNum > pPrev.GotAirDropNum)
                        events.Add(new SimEvent(tick, "airdrop", p.UId, null, $"AIRDROP popup - {p.PlayerName} ({p.TeamName})"));
                }
                foreach (var (teamId, members) in teamMemberIds)
                {
                    var aliveNow = cur.Players.Any(p => members.Contains(p.UId) && p.LiveState is >= 0 and <= 4);
                    if (teamWasAlive[teamId] && !aliveNow)
                    {
                        var teamName = players.First(p => p.TeamId == teamId).TeamName;
                        events.Add(new SimEvent(tick, "teamEliminated", members.First(), null, $"TEAM ELIMINATED popup - {teamName}"));
                    }
                    teamWasAlive[teamId] = aliveNow;
                }
                var aliveTeams = teamMemberIds.Count(kv => cur.Players.Any(p => kv.Value.Contains(p.UId) && p.LiveState is >= 0 and <= 4));
                var aliveTeamsPrev = teamMemberIds.Count(kv => prev.Players.Any(p => kv.Value.Contains(p.UId) && p.LiveState is >= 0 and <= 4));
                if (aliveTeams <= 4 && aliveTeamsPrev > 4)
                {
                    events.Add(new SimEvent(tick, "last4Switch", 0, null, "Live rankings hide, Last-4 bar appears top-middle"));
                }
            }
        }
        return events.OrderBy(e => e.Tick).ToList();
    }
}
