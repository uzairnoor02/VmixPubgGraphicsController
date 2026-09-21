using System.Text;
using System.Text.Json;

namespace VmixIngestAgent;

/// <summary>
/// Runs on the customer's production PC, next to vMix and pcob (PUBG's local companion API,
/// same one GetLiveData.cs in the main application used to poll directly). Its only job: read
/// pcob's isingame/gettotalplayerlist/getteaminfolist endpoints on a short interval and forward
/// the raw responses to the main application's POST /api/ingest/tick - so the main application
/// (and the React dashboard/overlay it serves) can run anywhere (a central server, the cloud, a
/// different building) instead of having to sit on the same machine or LAN segment as pcob.
///
/// Deliberately tiny and dependency-light: one file, no DI container, no logging framework - this
/// runs unattended on a production broadcast PC where "one more thing that can fail to start" is
/// a real cost. Config is a flat JSON file (appsettings.json next to the exe) read once at
/// startup; every setting can also be overridden by an environment variable of the same name
/// (PCOBURL, MAINAPPURL, AGENTKEY, POLLINTERVALMS, HTTPTIMEOUTSECONDS) for container/service
/// deployments that don't want to edit a file.
/// </summary>
internal static class Program
{
    private record AgentConfig(string PcobUrl, string MainAppUrl, string AgentKey, int PollIntervalMs, int HttpTimeoutSeconds);

    private static async Task<int> Main()
    {
        var config = LoadConfig();
        if (string.IsNullOrWhiteSpace(config.AgentKey) || config.AgentKey.StartsWith("CHANGE_ME"))
        {
            Console.WriteLine("AgentKey is not configured (still the placeholder) - set it in appsettings.json or the AGENTKEY environment variable to match Agent:IngestKey on the server. Exiting.");
            return 1;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(config.HttpTimeoutSeconds) };
        var pcobBase = config.PcobUrl.TrimEnd('/') + "/";
        var mainAppBase = config.MainAppUrl.TrimEnd('/');

        Console.WriteLine($"VmixIngestAgent starting. pcob: {pcobBase} -> main app: {mainAppBase} (every {config.PollIntervalMs}ms)");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var wasInGame = false;

        // Tick ordering. Ticks are posted strictly in order, but they don't necessarily arrive
        // that way: a request that stalls (or that we give up waiting on at HttpTimeoutSeconds
        // while the server keeps processing it) can land after the tick behind it. Stamping each
        // one with a monotonic sequence number lets the server discard anything older than what
        // it has already applied, instead of letting a late tick overwrite fresher stats and make
        // the overlay jump backwards.
        //
        // The session id scopes that counter. It's regenerated at the start of every match (and
        // implicitly on restart, since this is a fresh process), which is what lets the counter go
        // back to 1 without the server reading that as a flood of stale ticks.
        var sessionId = NewSessionId();
        long seq = 0;

        while (!cts.IsCancellationRequested)
        {
            var tickStart = DateTime.UtcNow;
            try
            {
                var isInGame = await IsInGameAsync(http, pcobBase);

                if (isInGame)
                {
                    if (!wasInGame)
                    {
                        sessionId = NewSessionId();
                        seq = 0;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Match started - ingest session {sessionId}");
                    }

                    var playerListJson = await http.GetStringAsync(pcobBase + "gettotalplayerlist", cts.Token);
                    var teamInfoJson = await http.GetStringAsync(pcobBase + "getteaminfolist", cts.Token);
                    // getkillinfo is a newer PC-OB endpoint and may be absent on older pcob
                    // builds, so a failure here is expected rather than exceptional: the tick
                    // still ships, with kill data omitted, and the feed simply stays empty.
                    var killInfoJson = await TryGetAsync(http, pcobBase + "getkillinfo", cts.Token);
                    // Claim the sequence number before awaiting the POST, so ordering reflects
                    // when the snapshot was taken rather than when its request happened to finish.
                    await PostTickAsync(http, mainAppBase, config.AgentKey, isInGame: true, playerListJson, teamInfoJson, sessionId, ++seq, killInfoJson);
                    wasInGame = true;
                }
                else if (wasInGame)
                {
                    // One last tick with whatever pcob still reports, so the main app can run its
                    // end-of-match post-processing off real final data rather than nothing at all.
                    string? finalPlayers = null, finalTeams = null;
                    try
                    {
                        finalPlayers = await http.GetStringAsync(pcobBase + "gettotalplayerlist", cts.Token);
                        finalTeams = await http.GetStringAsync(pcobBase + "getteaminfolist", cts.Token);
                    }
                    catch { /* best-effort - the match-ended signal below still matters even without final data */ }

                    await PostTickAsync(http, mainAppBase, config.AgentKey, isInGame: false, finalPlayers, finalTeams, sessionId, ++seq);
                    wasInGame = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Tick failed: {ex.Message}");
            }

            var elapsed = DateTime.UtcNow - tickStart;
            var remaining = TimeSpan.FromMilliseconds(config.PollIntervalMs) - elapsed;
            if (remaining > TimeSpan.Zero)
            {
                try { await Task.Delay(remaining, cts.Token); } catch (TaskCanceledException) { break; }
            }
        }

        Console.WriteLine("VmixIngestAgent stopped.");
        return 0;
    }

    private static string NewSessionId() => Guid.NewGuid().ToString("N");

    /// <summary>GET that returns null rather than throwing, for endpoints whose absence is normal.</summary>
    private static async Task<string?> TryGetAsync(HttpClient http, string url, CancellationToken token)
    {
        try
        {
            var response = await http.GetAsync(url, token);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(token) : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> IsInGameAsync(HttpClient http, string pcobBase)
    {
        try
        {
            var response = await http.GetAsync(pcobBase + "isingame");
            if (!response.IsSuccessStatusCode) return false;
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("isInGame", out var prop) && prop.GetBoolean();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"isingame check failed (is pcob running?): {ex.Message}");
            return false;
        }
    }

    private static async Task PostTickAsync(HttpClient http, string mainAppBase, string agentKey, bool isInGame, string? playerListJson, string? teamInfoJson, string sessionId, long seq, string? killInfoJson = null)
    {
        var payload = JsonSerializer.Serialize(new { isInGame, playerListJson, teamInfoJson, sessionId, seq, killInfoJson });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{mainAppBase}/api/ingest/tick") { Content = content };
        request.Headers.Add("X-Agent-Key", agentKey);

        var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Main app rejected tick #{seq} ({(int)response.StatusCode}): {body}");
            return;
        }

        // A 200 with applied=false isn't an error - it's the server telling us this tick arrived
        // after a newer one and was correctly discarded. Worth logging (a steady stream of these
        // means the link to the server is slower than the poll interval) but never worth retrying:
        // the next poll already carries fresher data than anything a retry could resend.
        if (!WasApplied(body))
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Tick #{seq} arrived out of order and was discarded by the server: {body}");
        }
    }

    private static bool WasApplied(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            // Absent "applied" means an older server build that predates tick ordering - treat
            // its 200 as success rather than logging a false warning every single tick.
            return !doc.RootElement.TryGetProperty("applied", out var applied)
                   || applied.ValueKind != JsonValueKind.False;
        }
        catch
        {
            return true;
        }
    }

    private static AgentConfig LoadConfig()
    {
        var pcobUrl = "http://127.0.0.1:19999/";
        var mainAppUrl = "http://localhost:5050";
        var agentKey = "";
        var pollIntervalMs = 1000;
        var httpTimeoutSeconds = 5;

        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(configPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                var root = doc.RootElement;
                if (root.TryGetProperty("PcobUrl", out var p)) pcobUrl = p.GetString() ?? pcobUrl;
                if (root.TryGetProperty("MainAppUrl", out var m)) mainAppUrl = m.GetString() ?? mainAppUrl;
                if (root.TryGetProperty("AgentKey", out var k)) agentKey = k.GetString() ?? agentKey;
                if (root.TryGetProperty("PollIntervalMs", out var i)) pollIntervalMs = i.GetInt32();
                if (root.TryGetProperty("HttpTimeoutSeconds", out var t)) httpTimeoutSeconds = t.GetInt32();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not parse appsettings.json ({ex.Message}) - using defaults / environment variables only.");
            }
        }

        pcobUrl = Environment.GetEnvironmentVariable("PCOBURL") ?? pcobUrl;
        mainAppUrl = Environment.GetEnvironmentVariable("MAINAPPURL") ?? mainAppUrl;
        agentKey = Environment.GetEnvironmentVariable("AGENTKEY") ?? agentKey;
        if (int.TryParse(Environment.GetEnvironmentVariable("POLLINTERVALMS"), out var envPoll)) pollIntervalMs = envPoll;
        if (int.TryParse(Environment.GetEnvironmentVariable("HTTPTIMEOUTSECONDS"), out var envTimeout)) httpTimeoutSeconds = envTimeout;

        return new AgentConfig(pcobUrl, mainAppUrl, agentKey, pollIntervalMs, httpTimeoutSeconds);
    }
}
