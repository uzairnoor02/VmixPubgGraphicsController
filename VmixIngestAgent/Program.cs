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

        while (!cts.IsCancellationRequested)
        {
            var tickStart = DateTime.UtcNow;
            try
            {
                var isInGame = await IsInGameAsync(http, pcobBase);

                if (isInGame)
                {
                    var playerListJson = await http.GetStringAsync(pcobBase + "gettotalplayerlist", cts.Token);
                    var teamInfoJson = await http.GetStringAsync(pcobBase + "getteaminfolist", cts.Token);
                    await PostTickAsync(http, mainAppBase, config.AgentKey, isInGame: true, playerListJson, teamInfoJson);
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

                    await PostTickAsync(http, mainAppBase, config.AgentKey, isInGame: false, finalPlayers, finalTeams);
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

    private static async Task PostTickAsync(HttpClient http, string mainAppBase, string agentKey, bool isInGame, string? playerListJson, string? teamInfoJson)
    {
        var payload = JsonSerializer.Serialize(new { isInGame, playerListJson, teamInfoJson });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{mainAppBase}/api/ingest/tick") { Content = content };
        request.Headers.Add("X-Agent-Key", agentKey);

        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Main app rejected tick ({(int)response.StatusCode}): {body}");
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
