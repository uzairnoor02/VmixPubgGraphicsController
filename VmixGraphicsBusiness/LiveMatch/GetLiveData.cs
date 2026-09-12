
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
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness.LiveMatch
{
    public class GetLiveData
    {
        private readonly LiveStatsBusiness _liveStatsBusiness;
        private readonly PostMatch _dbBusiness;
        private readonly IBackgroundJobClient _backgroundJobClient;
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

        // Target cadence for the live poll loop. PUBG's own feed updates roughly every 2 seconds,
        // so this stays comfortably ahead of that instead of chasing it.
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

        int zonemoving = 0;

        public GetLiveData(LiveStatsBusiness liveStatsBusiness, PostMatch dbBusiness, IBackgroundJobClient backgroundJobClient, MatchStateStore matchState, IServiceProvider serviceProvider)
        {
            _liveStatsBusiness = liveStatsBusiness;
            _dbBusiness = dbBusiness;
            _backgroundJobClient = backgroundJobClient;
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
                    var isInGameResponse = JsonSerializer.Deserialize<IsInGameResponse>(data);
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

            _matchState.PublishMatchStatus($"Match {match.MatchId} started successfully!");

            while (await IsInGame())
            {
                var tickStopwatch = System.Diagnostics.Stopwatch.StartNew();
                GetCircleInfo();
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

            var a = await VmixDataUtils.SetVMIXDataoperations();
            var liverakiingguid16 = a.LiverankingGuid16;
            var liverakiingguid18 = a.LiverankingGuid18;
            var liverakiingguid20 = a.LiverankingGuid20;
            var liverakiingguid4 = a.LiverankingGuid4;
            _backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(liverakiingguid16, 1, false, 3000));

            _backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(liverakiingguid16, 4, false, 3000));
            _backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(liverakiingguid4, 4, false, 3400));

            await Task.Delay(5000);
            var responsegetplayerDatapost = await _httpClient.GetAsync(_pcobUrl + "gettotalplayerlist");
            var responseTeamInfoListpost = await _httpClient.GetAsync(_pcobUrl + "getteaminfolist");
            string PlayerDatapost, teamdatapost;

            LivePlayersList livePlayerInfoPost = new();
            TeamInfoList TeamInfoListPost = new();
            if (responsegetplayerDatapost.IsSuccessStatusCode)
            {
                PlayerDatapost = await responsegetplayerDatapost.Content.ReadAsStringAsync();
                teamdatapost = await responseTeamInfoListpost.Content.ReadAsStringAsync();
                LivePlayersList livePlayerInfo = JsonSerializer.Deserialize<LivePlayersList>(PlayerDatapost)!;
                TeamInfoList TeamInfoList = JsonSerializer.Deserialize<TeamInfoList>(teamdatapost)!;

                try
                {
                    await _dbBusiness.createPostMtachStats(livePlayerInfo!, match, TeamInfoList!);
                }
                catch (Exception ex)
                {
                    // Post-match processing must never take the app down with it - log and leave the
                    // raw match state recoverable rather than throwing out of FetchAndPostData.
                    Console.WriteLine($"Post-match processing failed: {ex.Message}");
                    _matchState.PublishMatchStatus($"Post-match processing error: {ex.Message}");
                }
            }

            _matchState.PublishMatchStatus("");
        }

        public async Task<int> GetCircleInfo()
        {
            try
            {
                var response = await _httpClient.GetAsync(_pcobUrl + "getcircleinfo");
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsStringAsync();

                    var circleInfoDaTA = JsonSerializer.Deserialize<CircleDataWrapper>(data);
                    var circleInfo = circleInfoDaTA.CircleInfo;
                    vmixguidsclass vmixguids = await VmixDataUtils.SetVMIXDataoperations();
                    string circleClosingGtzip = vmixguids.CircleClosing;
                    if (circleInfo.CircleStatus == "2" && zonemoving == 0 && int.Parse(circleInfo.CircleIndex) < 6 && (int.Parse(circleInfo.MaxTime) - int.Parse(circleInfo.Counter)) <= 17)
                    {
                        Console.WriteLine("maxtime:" + circleInfo.MaxTime + "shrinkprogress=" + circleInfo.Counter);
                        zonemoving = 1;
                        _backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushCircleAnimationAsync(circleClosingGtzip, 2, true, (int.Parse(circleInfo.MaxTime) - int.Parse(circleInfo.Counter) - 3)));
                        zonemoving = 1;
                    }
                    if (circleInfo.CircleStatus == "0" && zonemoving == 1)
                    {
                        zonemoving = 0;
                    }
                    return int.Parse(circleInfo.CircleIndex);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
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
