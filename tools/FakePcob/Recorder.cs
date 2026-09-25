using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FakePcob;

/// One logged call, written to recordings/<folder>/index.ndjson (Task 7).
/// Source: "proxy" = the app asked for it through the recorder; "poll" = the recorder's own
/// background poller fetched it. Null in recordings made before the poller existed.
/// File: path of the saved body relative to the recording folder, e.g. "getkillinfo/000007_1790170000000.json".
/// Null in recordings made before per-endpoint numbering.
public sealed record RecordIndexEntry(int Seq, long UnixMs, string Endpoint, string Query, int Status, int Bytes, int LatencyMs, string? Source = null, string? File = null);

/// Every GET route served by the PCOB client's local API server, ObToolsNew/ob.js
/// (PCOB build 4.6.0.21520, guideline dated 6 Jan 2026). ob.js never answers a path it has no
/// handler for (the request just hangs), so the poller must only ask for names on this list.
/// Keep in sync with ob.js when Tencent ships a new client: grep "^app\.get" ob.js.
public static class PcobEndpoints
{
    public static readonly string[] All =
    {
        "isingame", "getallinfo", "gettotalplayerlist", "getteaminfolist", "getgameglobalinfo",
        "getcircleinfo", "getobservingplayer", "getkillinfo", "getkillbossinfo", "getplayerassistinfo",
        "getconsumeitem", "getteamreportdata", "getplayerreportdata", "getplayerweaponinfo",
        "getplayerweapondetailinfo", "getteambackpackinfo", "getallplayerthrowinfo", "getairdropboxinfo",
        "gettdmresultinfo", "getplayersaminfo", "getplayerssightusageinfo", "getreviveplayer",
        "getplayerdeadafterrevive", "getentertopeightafterrevive", "getunpossessemergencycall",
        "getemergencycallland", "getmortarplaced", "getmortarfire", "getmortarkill", "getpickupitem",
        "getplayerpickupinfo",
    };
}

/// Task 7 - `record --upstream <real-pcob>`: saves a real match to disk for later replay.
///
/// Two capture paths feed one folder:
///  1. Proxy (unchanged behaviour): the app polls the recorder, which forwards to real PCOB and
///     returns the body byte-for-byte. Now forwards ANY endpoint name, not just the 5 the app uses.
///  2. Poller (new): every --poll ms it GETs all 31 ob.js endpoints itself and saves a body only
///     when it differs from the last one saved for that endpoint - so endpoints the app never
///     calls (revives, mortars, pickups, weapons, report data...) are captured too.
///
/// `--poll-only` skips the proxy entirely: the app keeps talking straight to PCOB and the recorder
/// just listens alongside it. That is the safest mode - nothing sits in the app's data path.
public static class RecordCommand
{
    private static readonly Regex EndpointName = new("^[a-z]+$", RegexOptions.Compiled);

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
        var pollMs = opts.GetInt("poll", 2000);
        var pollOnly = opts.Has("poll-only");
        if (pollOnly && pollMs <= 0) pollMs = 2000;

        var root = opts.GetOrNull("out") is { Length: > 0 } outDir ? Path.GetFullPath(outDir) : FindRecordingsRoot();
        var folder = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);
        Console.WriteLine($"Recording to: {folder}");
        Console.WriteLine("WARNING: test PC only - never point a real broadcast's pcobUrl at this in the production path.");

        var writer = new BackgroundIndexWriter(folder);
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var seq = 0;
        int NextSeq() => Interlocked.Increment(ref seq) - 1;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        Task? pollTask = null;
        if (pollMs > 0)
        {
            Console.WriteLine($"Polling all {PcobEndpoints.All.Length} ob.js endpoints every {pollMs}ms from {upstream} (saved only when changed)");
            pollTask = PollLoopAsync(http, upstream, pollMs, writer, NextSeq, cts.Token);
        }

        if (pollOnly)
        {
            Console.WriteLine("Poll-only mode: no proxy. Leave the app pointed at the real PCOB. Ctrl+C to stop.");
            try { await pollTask!; } catch (OperationCanceledException) { }
            Console.WriteLine("Stopped.");
            return 0;
        }

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var app = builder.Build();

        app.MapGet("/{endpoint}", async (HttpContext ctx, string endpoint) =>
        {
            if (!EndpointName.IsMatch(endpoint))
            {
                ctx.Response.StatusCode = 404;
                return;
            }
            var mySeq = NextSeq();
            var sw = Stopwatch.StartNew();
            var query = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value! : "";
            try
            {
                using var response = await http.GetAsync(upstream + endpoint + query);
                var body = await response.Content.ReadAsByteArrayAsync();
                var latency = (int)sw.ElapsedMilliseconds;

                ctx.Response.StatusCode = (int)response.StatusCode;
                ctx.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
                await ctx.Response.Body.WriteAsync(body); // byte-for-byte, never parsed here

                writer.Enqueue(endpoint, query, (int)response.StatusCode, body, mySeq, latency, "proxy");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[record] {endpoint} failed: {ex.Message}" +
                    (Array.IndexOf(PcobEndpoints.All, endpoint) < 0 ? " (not an ob.js endpoint - ob.js never answers unknown paths)" : ""));
                ctx.Response.StatusCode = 502;
            }
        });
        app.MapFallback(ctx =>
        {
            Console.WriteLine($"UNKNOWN PATH {ctx.Request.Method} {ctx.Request.Path}{ctx.Request.QueryString}");
            ctx.Response.StatusCode = 404;
            return Task.CompletedTask;
        });

        Console.WriteLine($"Recording proxy listening on http://127.0.0.1:{port}/ -> {upstream}");
        if (new Uri(upstream).Port == port && new Uri(upstream).IsLoopback)
        {
            Console.WriteLine($"NOTE: PCOB itself uses port {port} on this PC. Start the proxy with a different --port (e.g. 10087)," +
                              " or use --poll-only.");
        }
        try { await app.RunAsync(cts.Token); } catch (OperationCanceledException) { }
        cts.Cancel();
        return 0;
    }

    /// Fetches every ob.js endpoint each round (in parallel) and hands a body to the writer only
    /// when its hash differs from the last one saved for that endpoint. A round that is still
    /// running when the next tick is due simply delays it (PeriodicTimer never stacks ticks), so a
    /// slow PCOB can't pile up requests. No failure here ever stops the recorder or the proxy.
    private static async Task PollLoopAsync(HttpClient http, string upstream, int intervalMs,
        BackgroundIndexWriter writer, Func<int> nextSeq, CancellationToken ct)
    {
        var lastHash = new Dictionary<string, string>();
        var saved = PcobEndpoints.All.ToDictionary(e => e, _ => 0);
        bool? upstreamOk = null;
        string? lastInGame = null;
        var lastReport = Stopwatch.StartNew();
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));

        do
        {
            var results = await Task.WhenAll(PcobEndpoints.All.Select(async ep =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    using var r = await http.GetAsync(upstream + ep, ct);
                    var body = await r.Content.ReadAsByteArrayAsync(ct);
                    return (ep, ok: true, status: (int)r.StatusCode, body, latency: (int)sw.ElapsedMilliseconds);
                }
                catch
                {
                    return (ep, ok: false, status: 0, body: Array.Empty<byte>(), latency: 0);
                }
            }));
            if (ct.IsCancellationRequested) break;

            var anyOk = results.Any(r => r.ok);
            if (anyOk != upstreamOk)
            {
                upstreamOk = anyOk;
                Console.WriteLine(anyOk
                    ? $"[poll] connected to PCOB API at {upstream}"
                    : $"[poll] cannot reach PCOB API at {upstream} - is launch.bat running and API Enable clicked? retrying...");
            }

            foreach (var r in results.Where(r => r.ok))
            {
                var hash = Convert.ToHexString(SHA1.HashData(r.body));
                if (lastHash.TryGetValue(r.ep, out var prev) && prev == hash) continue;
                lastHash[r.ep] = hash;
                writer.Enqueue(r.ep, "", r.status, r.body, nextSeq(), r.latency, "poll");
                saved[r.ep]++;

                if (r.ep == "isingame")
                {
                    var now = Encoding.UTF8.GetString(r.body);
                    if (lastInGame is not null)
                        Console.WriteLine(now.Contains("true")
                            ? "[poll] MATCH STARTED"
                            : "[poll] MATCH ENDED - keep recording ~60s so the end-of-match stats are captured");
                    lastInGame = now;
                }
            }

            if (lastReport.Elapsed.TotalSeconds >= 15 && upstreamOk == true)
            {
                lastReport.Restart();
                var active = saved.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}");
                Console.WriteLine($"[poll] files saved per endpoint: {string.Join(" ", active)}");
            }
        }
        while (await SafeWaitAsync(timer, ct));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
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
    /// just forwarded. The timestamp is taken when the body arrives, not when it reaches disk.
    ///
    /// Layout: one folder per endpoint, and inside it files numbered 000001, 000002, ... in the
    /// exact order they were saved (numbered here, on the single drain thread, so the order can
    /// never be scrambled by parallel requests). Sorting a folder by name = sorting by save time.
    private sealed class BackgroundIndexWriter
    {
        private readonly string _folder;
        private readonly Dictionary<string, int> _perEndpoint = new();
        private readonly System.Collections.Concurrent.BlockingCollection<(string path, string query, int status, byte[] body, int seq, int latency, string source, long unixMs)> _queue = new();

        public BackgroundIndexWriter(string folder)
        {
            _folder = folder;
            var thread = new Thread(Drain) { IsBackground = true };
            thread.Start();
        }

        public void Enqueue(string path, string query, int status, byte[] body, int seq, int latency, string source)
            => _queue.Add((path, query, status, body, seq, latency, source, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        private void Drain()
        {
            var indexPath = Path.Combine(_folder, "index.ndjson");
            foreach (var item in _queue.GetConsumingEnumerable())
            {
                try
                {
                    var dir = Path.Combine(_folder, item.path);
                    Directory.CreateDirectory(dir);
                    var n = _perEndpoint.TryGetValue(item.path, out var c) ? c + 1 : 1;
                    _perEndpoint[item.path] = n;
                    var name = $"{n:D6}_{item.unixMs}.json";
                    File.WriteAllBytes(Path.Combine(dir, name), item.body);

                    var entry = new RecordIndexEntry(item.seq, item.unixMs, item.path, item.query, item.status, item.body.Length, item.latency, item.source, item.path + "/" + name);
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

        var allEntries = File.ReadAllLines(indexPath)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonSerializer.Deserialize<RecordIndexEntry>(l)!)
            .ToList();
        var entries = allEntries
            .Where(e => e.Endpoint == "gettotalplayerlist" && e.Status is >= 200 and < 300)
            .OrderBy(e => e.UnixMs).ThenBy(e => e.Seq)
            .ToList();

        // A recording can hold the same player list twice: once per app poll ("proxy") and once
        // per change seen by the recorder's own poller ("poll"). Use one source only, or every tick
        // would replay twice. Proxy wins (it is exactly what the app saw, at the app's cadence);
        // poll-only recordings fall back to the poller's frames.
        if (entries.Any(e => e.Source == "proxy"))
            entries = entries.Where(e => e.Source == "proxy").ToList();
        else if (entries.Any(e => e.Source == "poll") && entries.Any(e => e.Source is null))
            entries = entries.Where(e => e.Source != "poll").ToList();

        if (entries.Count == 0)
        {
            throw new InvalidDataException($"'{folder}' has no successful gettotalplayerlist captures to replay.");
        }

        var frames = new List<MatchFrameData>();
        var frameUnixMs = new List<long>();
        List<SeedPlayer>? lastPlayers = null;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var file = e.File is not null
                ? Path.Combine(folder, e.File.Replace('/', Path.DirectorySeparatorChar))
                : Directory.GetFiles(Path.Combine(folder, "gettotalplayerlist"), $"{e.Seq:D5}_*.json").FirstOrDefault(); // pre-numbering recordings
            if (file is not null && !File.Exists(file)) file = null;
            if (file is null) continue;
            var envelope = JsonSerializer.Deserialize<SeedEnvelope>(File.ReadAllText(file));
            if (envelope is null || envelope.PlayerInfoList.Count == 0) continue;
            lastPlayers = envelope.PlayerInfoList;

            var frame = new MatchFrameData { Tick = frames.Count };
            frameUnixMs.Add(e.UnixMs);
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
            FrameUnixMs = frameUnixMs,
            Recorded = RecordedFeeds.Load(folder, allEntries),
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
