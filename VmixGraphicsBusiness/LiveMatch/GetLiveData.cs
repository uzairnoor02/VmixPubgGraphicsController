
using Google.Apis.Sheets.v4.Data;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;
using VmixData.Models;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness;
using VmixGraphicsBusiness.PostMatchStats;
using VmixGraphicsBusiness.Utils;

namespace VmixGraphicsBusiness.LiveMatch
{
    public class GetLiveData
    {
        private readonly LiveStatsBusiness _liveStatsBusiness;
        private readonly PostMatch _dbBusiness;
        private readonly MatchStateStore _matchState;
        private readonly string _pcobUrl;
        private readonly IServiceProvider serviceProvider1;
        private List<LiveTeamPointStats> teampoints = null;

        // One shared, reused HttpClient for this instance's whole lifetime instead of a new
        // HttpClient() per call (the old IsInGame/GetCircleInfo/FetchAndPostData each created
        // their own). Reusing a client avoids socket exhaustion and per-call TCP handshake
        // overhead, and the explicit timeout means a stalled pcob endpoint can never stall the
        // poll loop for the BCL default of 100 seconds.
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        // Per-match tracker turning pcob's rolling getkillinfo list into "what's new since the
        // last tick" - see KillFeedTracker for why every failure mode there is silent.
        private readonly VmixGraphicsBusiness.Utils.KillFeedTracker _killFeed = new();

        // Target cadence for the live poll loop. PUBG's own feed updates roughly every 2 seconds,
        // so this stays comfortably ahead of that instead of chasing it.
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

        private static readonly JsonSerializerOptions CaseInsensitiveJson = new() { PropertyNameCaseInsensitive = true };

        // How long "start match" waits for pcob to report isInGame before giving up.
        private static readonly TimeSpan MaxWaitForGameStart = TimeSpan.FromMinutes(10);

        public GetLiveData(LiveStatsBusiness liveStatsBusiness, PostMatch dbBusiness, MatchStateStore matchState, IServiceProvider serviceProvider)
        {
            _liveStatsBusiness = liveStatsBusiness;
            _dbBusiness = dbBusiness;
            _pcobUrl = ConfigGlobal.PcobUrl;
            _matchState = matchState;
            serviceProvider1 = serviceProvider;
        }

        public async Task<bool> IsInGame()
        {
            try
            {
                var response = await _httpClient.GetAsync(_pcobUrl + "isingame");
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsStringAsync();
                    // Case-insensitive: real pcob sends "isInGame", but a field name that differs
                    // only by case (as FakePcob's "IsInGame" did) must never read as "not in game"
                    // and silently block the whole match from starting.
                    var isInGameResponse = JsonSerializer.Deserialize<IsInGameResponse>(data, CaseInsensitiveJson);
                    return isInGameResponse?.IsInGame ?? false;
                }
                else
                {
                    Console.WriteLine($"Failed to fetch isingame status. Status code: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception e)
            {
                _matchState.PublishMatchStatus($"{e.Message}");
                Console.WriteLine($"An error occurred while checking isingame status: {e.Message}");
                return false;
            }
        }

        [Queue(HangfireQueues.HighPriority)]
        [AutomaticRetry(Attempts = 0, DelaysInSeconds = new[] { 2 })]
        [DisableConcurrentExecution(timeoutInSeconds: 1)]
        public async Task FetchAndPostData(Match match)
        {
            var previousData = "";

            // Clear any Top4 position locks / elimination flags / cached lists left over from a
            // previous match. This is the fix for positions leaking across matches - belt and
            // suspenders alongside Reset.cs also clearing state on an explicit reset.
            _matchState.ResetMatchState();

            if (teampoints is null)
            {
                teampoints = await _dbBusiness.fetchTeamPointsAsync(match);
            }

            // A fresh match must never inherit the previous match's kills, or every one of them
            // would re-announce on the first tick.
            _killFeed.Reset();

            // Wait (bounded) for PUBG to actually be in a match. Previously, starting a match
            // while pcob was still in the lobby fell straight through the loop below and went on
            // to save the lobby's empty numbers as this match's final results.
            var waitDeadline = DateTime.UtcNow + MaxWaitForGameStart;
            var inGame = await IsInGame();
            if (!inGame)
            {
                _matchState.PublishMatchStatus($"Match {match.MatchId}: waiting for PUBG to go in-game...");
                while (!inGame && DateTime.UtcNow < waitDeadline)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    inGame = await IsInGame();
                }
            }
            if (!inGame)
            {
                _matchState.PublishMatchStatus($"Match {match.MatchId}: PUBG never went in-game within {MaxWaitForGameStart.TotalMinutes:0} minutes - nothing was recorded. Start the match again once the game is live.");
                return;
            }

            _matchState.PublishMatchStatus($"Match {match.MatchId} started successfully!");

            while (await IsInGame())
            {
                var tickStopwatch = System.Diagnostics.Stopwatch.StartNew();
                await GetCircleInfo();
                // Kill names and team inventories first, so this tick's banners (FIRST BLOOD's
                // victim) and Last 4 cards (throwables) can already use them.
                await PollKillFeedAsync();
                await PollBackpackAsync();
                try
                {
                    var responsegetplayerData = await _httpClient.GetAsync(_pcobUrl + "gettotalplayerlist");
                    var responseTeamInfoList = await _httpClient.GetAsync(_pcobUrl + "getteaminfolist");

                    if (responsegetplayerData.IsSuccessStatusCode)
                    {
                        var PlayerData = await responsegetplayerData.Content.ReadAsStringAsync();
                        var teamdata = await responseTeamInfoList.Content.ReadAsStringAsync();
                        if (true || (PlayerData != null && PlayerData != previousData)) // PlayerData != previousData &&
                        {
                            LivePlayersList livePlayerInfo = JsonSerializer.Deserialize<LivePlayersList>(PlayerData)!;
                            TeamInfoList TeamInfoList = JsonSerializer.Deserialize<TeamInfoList>(teamdata)!;
                            var filteredPlayerInfo = LiveStatsBusiness.FilterPlayerInfo(livePlayerInfo);

                            // Process this tick's data directly, in-process, instead of bouncing it
                            // through a Hangfire queue. That old hop added a Redis round trip plus a
                            // queue dequeue purely as internal plumbing between two parts of the same
                            // process, and - combined with Hangfire's DisableConcurrentExecution lock
                            // timeouts - could silently drop a tick's update under any load spike.
                            // Awaiting it directly here also guarantees ticks are always processed and
                            // displayed strictly in order, since this loop is the only caller.
                            var liveStatsResult = await _liveStatsBusiness.CreateDynamicLiveStats(match, filteredPlayerInfo, TeamInfoList, teampoints);
                            if (liveStatsResult is List<TeamLiveStats> teamLiveStatsList)
                            {
                                // Feeds the SignalR hub for the web dashboard - see LiveDashboardHub.
                                _matchState.PublishLiveTeams(teamLiveStatsList);
                            }
                            previousData = PlayerData;
                            await _matchState.StringSetAsync(HelperRedis.PlayerInfolist, PlayerData);
                            await _matchState.StringSetAsync(HelperRedis.TeamInfoList, teamdata);
                        }
                        else
                        {
                            Console.WriteLine("No change in PlayerData.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to fetch PlayerData. Status code: {responsegetplayerData.StatusCode}");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An error occurred: {e.Message}");
                    _matchState.PublishMatchStatus($"{e.Message}");
                }

                // Adaptive delay: aim for PollInterval between the START of one tick and the START
                // of the next, instead of always sleeping a flat 1000ms on top of however long this
                // tick's HTTP calls + processing took. Under load the old fixed delay let the loop's
                // real cadence drift past PUBG's own 2-second update interval; this keeps it pinned.
                var remaining = PollInterval - tickStopwatch.Elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining);
                }
            }

            // Match over. The live graphics (circle bar, Top 4) are cleared from the overlay here -
            // this replaces the old vMix "overlay out" animation calls, which made the whole
            // match depend on vMix being reachable at this exact moment. What's on air after this
            // is decided on the overlay/Director side, never by this loop.
            _matchState.ClearLiveGraphics();
            _matchState.PublishMatchStatus($"Match {match.MatchId} finished - computing results...");

            // pcob keeps final numbers settling for a few seconds after isingame flips false.
            await Task.Delay(5000);
            try
            {
                var responsegetplayerDatapost = await _httpClient.GetAsync(_pcobUrl + "gettotalplayerlist");
                var responseTeamInfoListpost = await _httpClient.GetAsync(_pcobUrl + "getteaminfolist");

                if (responsegetplayerDatapost.IsSuccessStatusCode && responseTeamInfoListpost.IsSuccessStatusCode)
                {
                    var PlayerDatapost = await responsegetplayerDatapost.Content.ReadAsStringAsync();
                    var teamdatapost = await responseTeamInfoListpost.Content.ReadAsStringAsync();
                    LivePlayersList livePlayerInfo = JsonSerializer.Deserialize<LivePlayersList>(PlayerDatapost)!;
                    TeamInfoList TeamInfoList = JsonSerializer.Deserialize<TeamInfoList>(teamdatapost)!;

                    // Saves the match to the DB, then computes and publishes every post-match
                    // graphic (match/overall rankings, MVP, champions, teams to watch) to the
                    // overlay - see PostMatch.createPostMtachStats.
                    await _dbBusiness.createPostMtachStats(livePlayerInfo!, match, TeamInfoList!);
                    _matchState.PublishMatchStatus($"Match {match.MatchId} results saved - post-match graphics are ready.");
                }
                else
                {
                    _matchState.PublishMatchStatus($"Match {match.MatchId}: could not read final results from pcob ({responsegetplayerDatapost.StatusCode}/{responseTeamInfoListpost.StatusCode}).");
                }
            }
            catch (Exception ex)
            {
                // Post-match processing must never take the app down with it - log and leave the
                // raw match state recoverable rather than throwing out of FetchAndPostData.
                Console.WriteLine($"Post-match processing failed: {ex.Message}");
                _matchState.PublishMatchStatus($"Post-match processing error: {ex.Message}");
            }
        }

        /// <summary>
        /// Polls pcob's getkillinfo: new eliminations go to the overlay's kill feed, and each
        /// killer -> victim pair is remembered for the achievement banners (see KillFeedPublisher).
        /// Names only - kill counts keep coming from the player list. Best-effort: a pcob without
        /// this endpoint just leaves the feed on its derived fallback.
        /// </summary>
        private async Task PollKillFeedAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(_pcobUrl + "getkillinfo");
                if (!response.IsSuccessStatusCode) return;
                var raw = await response.Content.ReadAsStringAsync();

                if (_killFeed.ShouldLogRawSample())
                {
                    Console.WriteLine($"[killfeed] first getkillinfo payload this match: {raw}");
                }

                KillFeedPublisher.Apply(_matchState, _killFeed, raw);
            }
            catch
            {
                // getkillinfo is optional - never let it break a tick that otherwise worked.
            }
        }

        /// <summary>
        /// Polls pcob's getteambackpackinfo - the inventory of the team the observer is watching -
        /// and keeps per-team throwable counts for the Last 4 cards (see TeamInventoryTracker).
        /// Best-effort, like the kill feed.
        /// </summary>
        private async Task PollBackpackAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(_pcobUrl + "getteambackpackinfo");
                if (!response.IsSuccessStatusCode) return;
                _matchState.Inventory.Update(await response.Content.ReadAsStringAsync());
            }
            catch
            {
                // optional endpoint
            }
        }

        /// <summary>Polls pcob's getcircleinfo and publishes it to the overlay's Circle bar
        /// ("CircleUpdated") - see CirclePayload for the normalisation.</summary>
        public async Task<int> GetCircleInfo()
        {
            try
            {
                var response = await _httpClient.GetAsync(_pcobUrl + "getcircleinfo");
                if (!response.IsSuccessStatusCode) return 0;
                return CirclePayload.Publish(_matchState, await response.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                // The zone bar is optional - never let it break the stats tick.
                Console.WriteLine($"getcircleinfo failed: {ex.Message}");
            }
            return 0;
        }

        public class DependencyJobActivator : JobActivator
        {
            private readonly IServiceProvider _serviceProvider;

            public DependencyJobActivator(IServiceProvider serviceProvider)
            {
                _serviceProvider = serviceProvider;
            }

            public override object ActivateJob(Type jobType)
            {
                return _serviceProvider.GetService(jobType);
            }
        }
        public class IsInGameResponse
        {
            [JsonPropertyName("isInGame")]
            public bool IsInGame { get; set; }
        }
    }
}
