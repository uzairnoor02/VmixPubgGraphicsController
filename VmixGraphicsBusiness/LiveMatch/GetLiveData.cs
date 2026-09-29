
using Google.Apis.Sheets.v4.Data;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
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
        private readonly IConnectionMultiplexer _redisConnection;
        private readonly string _pcobUrl;
        private readonly IServiceProvider serviceProvider1;
        private List<LiveTeamPointStats> teampoints = null;

        private ISubscriber subscriber;
        private readonly IDatabase db;
        int zonemoving = 0;

        public GetLiveData(LiveStatsBusiness liveStatsBusiness, PostMatch dbBusiness, IBackgroundJobClient backgroundJobClient, IConnectionMultiplexer connectionMultiplexer, IServiceProvider serviceProvider)
        {
            _liveStatsBusiness = liveStatsBusiness;
            _dbBusiness = dbBusiness;
            _backgroundJobClient = backgroundJobClient;
            _pcobUrl = ConfigGlobal.PcobUrl;
            _redisConnection = connectionMultiplexer;
            serviceProvider1 = serviceProvider;
            db = _redisConnection.GetDatabase();

            subscriber = _redisConnection.GetSubscriber();
            using var scope = serviceProvider.CreateScope();
        }

        public async Task<bool> IsInGame()
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(_pcobUrl + "isingame");
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
                    await db.StringSetAsync(HelperRedis.MatchStatus, $"{e.Message}");
                    await subscriber.PublishAsync("match-status-channel", "Exception");
                    Console.WriteLine($"An error occurred while checking isingame status: {e.Message}");
                    return false;
                }
            }
        }

        [AutomaticRetry(Attempts = 0, DelaysInSeconds = new[] { 2 })]
        [DisableConcurrentExecution(timeoutInSeconds: 1)]
        public async Task FetchAndPostData(Match match)
        {
            var previousData = "";
            if (teampoints is null)
            {
                teampoints = await _dbBusiness.fetchTeamPointsAsync(match);
            }

            await db.StringSetAsync(HelperRedis.MatchStatus, $"Match {match.MatchId} started successfully!");
            await subscriber.PublishAsync("match-status-channel", "Started");
            while (await IsInGame())
            {
                GetCircleInfo();
                try
                {
                    // getallinfo replaces separate gettotalplayerlist + getteaminfolist calls --
                    // it's a superset (same player/team fields, confirmed identical casing against
                    // the 20260923-232832 capture) plus GameID and the match clock. Fetched
                    // together with getkillinfo via Task.WhenAll, not sequentially.
                    var allInfoTask = AllInfoClient.FetchAsync(_pcobUrl);
                    var killInfoTask = KillFeedTracker.FetchAsync(_pcobUrl);

                    await Task.WhenAll(allInfoTask, killInfoTask);
                    var allInfo = await allInfoTask;
                    var allKillRowsOldestFirst = await killInfoTask;

                    if (allInfo != null)
                    {
                        // Turns the cumulative getkillinfo feed into "what's new since last poll",
                        // scoped to this match. See KillFeedTracker for why this is cheap.
                        var newKillEvents = await KillFeedTracker.GetNewRowsAsync(db, match.Id, allKillRowsOldestFirst);

                        var PlayerData = JsonSerializer.Serialize(allInfo.TotalPlayerList);
                        var teamdata = JsonSerializer.Serialize(allInfo.TeamInfoList);

                        if (true||(PlayerData != null && PlayerData != previousData))// PlayerData != previousData &&
                        {
                            var filteredPlayerInfo = new LivePlayersList
                            {
                                PlayerInfoList = allInfo.TotalPlayerList.Select(player => new LivePlayerInfo
                                {
                                    UId = player.UId,
                                    PlayerName = player.PlayerName,
                                    TeamId = player.TeamId,
                                    TeamName = player.TeamName,
                                    Health = player.Health,
                                    HealthMax = player.HealthMax,
                                    LiveState = player.LiveState,
                                    KillNum = player.KillNum,
                                    KillNumByGrenade = player.KillNumByGrenade,
                                    KillNumInVehicle = player.KillNumInVehicle,
                                    GotAirDropNum = player.GotAirDropNum,
                                    UseFragGrenadeNum = player.UseFragGrenadeNum,
                                    UseSmokeGrenadeNum = player.UseSmokeGrenadeNum,
                                    UseBurnGrenadeNum = player.UseBurnGrenadeNum,
                                    BHasDied = player.BHasDied,
                                    IsOutsideBlueCircle = player.IsOutsideBlueCircle,
                                    Rank = player.Rank,
                                    Assists = player.Assists,
                                    KillNumBeforeDie = player.KillNumBeforeDie,

                                }).ToList()
                            };
                            TeamInfoList TeamInfoList = new TeamInfoList { teamInfoList = allInfo.TeamInfoList };

                            _backgroundJobClient.Enqueue(HangfireQueues.HighPriority, () => _liveStatsBusiness.CreateDynamicLiveStats(match, filteredPlayerInfo, TeamInfoList, teampoints, newKillEvents, allInfo.CurrentTime));
                            previousData = PlayerData;
                            await db.StringSetAsync(HelperRedis.PlayerInfolist, PlayerData);
                            await db.StringSetAsync(HelperRedis.TeamInfoList, teamdata);
                        }
                        else
                        {
                            Console.WriteLine("No change in PlayerData.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to fetch getallinfo (unreachable or bad response).");
                    }
                    await Task.Delay(1000);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An error occurred: {e.Message}");
                    await db.StringSetAsync(HelperRedis.MatchStatus, $"{e.Message}");
                    await subscriber.PublishAsync("match-status-channel", "Exception");
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
            var allInfoPost = await AllInfoClient.FetchAsync(_pcobUrl);

            if (allInfoPost != null)
            {
                LivePlayersList livePlayerInfo = new LivePlayersList { PlayerInfoList = allInfoPost.TotalPlayerList };
                TeamInfoList TeamInfoList = new TeamInfoList { teamInfoList = allInfoPost.TeamInfoList };
                await _dbBusiness.createPostMtachStats(livePlayerInfo!, match, TeamInfoList!);

                // Final live-checked ended state for this pcob GameID, so the next time Start
                // is pressed for the same game, EvaluateStartAsync's fresh getallinfo check
                // still finds a record here (its own HasEnded check is what actually decides).
                if (!string.IsNullOrEmpty(allInfoPost.GameID))
                    await GameTracker.RegisterOrResumeAsync(db, allInfoPost.GameID, match.Id);
            }

            await db.StringSetAsync(HelperRedis.MatchStatus, $"");
            await subscriber.PublishAsync("match-status-channel", "Ended");

        }

        /// <summary>
        /// Continuous replacement for the manual "pick Day/Match, press Start, restart for the
        /// next match" flow. The operator picks Tournament + Stage once; everything else --
        /// which match is live, when one ends, when the next one begins -- is driven off pcob's
        /// own getallinfo (GameID + HasEnded), never IsInGame(). Runs until the process is
        /// stopped (this codebase's existing "stop" is restarting the whole app, same as
        /// FetchAndPostData's loop above -- there's no separate cancellation path here either).
        /// </summary>
        [AutomaticRetry(Attempts = 0, DelaysInSeconds = new[] { 2 })]
        [DisableConcurrentExecution(timeoutInSeconds: 1)]
        public async Task RunAutoTrackingAsync(int tournamentId, int stageId)
        {
            // Deliberately NOT resolved once here: this loop can run for days across many
            // matches, and holding one EF Core context open that long lets its change tracker
            // grow unbounded. Every DB touch below opens its own short-lived scope instead.
            Match activeMatch = null;
            List<LiveTeamPointStats> teampointsForActiveMatch = null;

            await db.StringSetAsync(HelperRedis.MatchStatus, "Waiting for match data...");
            await subscriber.PublishAsync("match-status-channel", "AutoTrackingStarted");

            while (true)
            {
                AllInfo allInfo;
                try
                {
                    allInfo = await AllInfoClient.FetchAsync(_pcobUrl);
                }
                catch
                {
                    allInfo = null;
                }

                if (allInfo == null || string.IsNullOrEmpty(allInfo.GameID))
                {
                    // pcob has no live game data yet (or is unreachable) -- the operator pressed
                    // Start before the game actually began. Nothing to do until real data flows.
                    await Task.Delay(2000);
                    continue;
                }

                // A GameID we haven't resolved to a Match yet: either the very first game seen
                // this session, or the previous one ended and a new one has begun. Resolve it
                // fresh every time the GameID changes, never only once at Start.
                if (activeMatch == null || activeMatch.GameId != allInfo.GameID)
                {
                    var trackedMatchDbId = await GameTracker.GetTrackedMatchDbIdAsync(db, allInfo.GameID);
                    if (trackedMatchDbId.HasValue)
                    {
                        using var lookupScope = serviceProvider1.CreateScope();
                        var context = lookupScope.ServiceProvider.GetRequiredService<vmix_graphicsContext>();
                        activeMatch = await context.Matches.FindAsync(trackedMatchDbId.Value);
                    }

                    if (activeMatch == null)
                    {
                        using var createScope = serviceProvider1.CreateScope();
                        var tournamentBusinessForCreate = createScope.ServiceProvider.GetRequiredService<TournamentBusiness>();

                        // Never seen this GameID before. Before treating it as the next match,
                        // check whether today's most recent match never actually concluded (no
                        // results saved) -- a crash, or pcob/ob.js restarted mid-match, can leave
                        // one behind. If so, ask the operator whether this new game is that same
                        // match starting over, or the next match should be started instead of
                        // silently guessing either way.
                        var incomplete = await tournamentBusinessForCreate.FindIncompleteMatchAsync(tournamentId, stageId, DateTime.UtcNow);
                        if (incomplete != null)
                        {
                            var decision = await AskOperatorIncompleteMatchDecisionAsync(incomplete, allInfo.GameID);
                            if (decision == IncompleteMatchDecision.ContinueExisting)
                            {
                                activeMatch = await tournamentBusinessForCreate.ReassignGameIdAsync(incomplete, allInfo.GameID);
                            }
                        }

                        if (activeMatch == null)
                        {
                            // Either no incomplete match was pending, or the operator chose to
                            // start fresh -- Day/MatchId are derived (today's date, next slot); the
                            // operator never enters them.
                            activeMatch = await tournamentBusinessForCreate.GetOrCreateMatchByGameIdAsync(tournamentId, stageId, allInfo.GameID, DateTime.UtcNow);
                        }
                    }

                    await GameTracker.RegisterOrResumeAsync(db, allInfo.GameID, activeMatch.Id);
                    teampointsForActiveMatch = await _dbBusiness.fetchTeamPointsAsync(activeMatch);
                    await db.StringSetAsync(HelperRedis.MatchStatus, $"Tracking GameID {allInfo.GameID} as Match {activeMatch.MatchId} (Day {activeMatch.MatchDayId})");
                    Console.WriteLine($"Now tracking GameID {allInfo.GameID} as Match {activeMatch.MatchId}, Day {activeMatch.MatchDayId}.");
                }

                if (!allInfo.HasEnded)
                {
                    // Match still in progress -- same per-tick work as FetchAndPostData's loop:
                    // circle timer, live stats push, achievements off the real getkillinfo feed.
                    GetCircleInfo();
                    try
                    {
                        var allKillRowsOldestFirst = await KillFeedTracker.FetchAsync(_pcobUrl);
                        var newKillEvents = await KillFeedTracker.GetNewRowsAsync(db, activeMatch.Id, allKillRowsOldestFirst);

                        var filteredPlayerInfo = new LivePlayersList
                        {
                            PlayerInfoList = allInfo.TotalPlayerList.Select(player => new LivePlayerInfo
                            {
                                UId = player.UId,
                                PlayerName = player.PlayerName,
                                TeamId = player.TeamId,
                                TeamName = player.TeamName,
                                Health = player.Health,
                                HealthMax = player.HealthMax,
                                LiveState = player.LiveState,
                                KillNum = player.KillNum,
                                KillNumByGrenade = player.KillNumByGrenade,
                                KillNumInVehicle = player.KillNumInVehicle,
                                GotAirDropNum = player.GotAirDropNum,
                                UseFragGrenadeNum = player.UseFragGrenadeNum,
                                UseSmokeGrenadeNum = player.UseSmokeGrenadeNum,
                                UseBurnGrenadeNum = player.UseBurnGrenadeNum,
                                BHasDied = player.BHasDied,
                                IsOutsideBlueCircle = player.IsOutsideBlueCircle,
                                Rank = player.Rank,
                                Assists = player.Assists,
                                KillNumBeforeDie = player.KillNumBeforeDie,
                            }).ToList()
                        };
                        var teamInfoList = new TeamInfoList { teamInfoList = allInfo.TeamInfoList };

                        _backgroundJobClient.Enqueue(HangfireQueues.HighPriority, () => _liveStatsBusiness.CreateDynamicLiveStats(activeMatch, filteredPlayerInfo, teamInfoList, teampointsForActiveMatch, newKillEvents, allInfo.CurrentTime));

                        await db.StringSetAsync(HelperRedis.PlayerInfolist, JsonSerializer.Serialize(allInfo.TotalPlayerList));
                        await db.StringSetAsync(HelperRedis.TeamInfoList, JsonSerializer.Serialize(allInfo.TeamInfoList));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"An error occurred: {e.Message}");
                        await db.StringSetAsync(HelperRedis.MatchStatus, $"{e.Message}");
                        await subscriber.PublishAsync("match-status-channel", "Exception");
                    }

                    await Task.Delay(1000);
                }
                else
                {
                    // pcob says this GameID has ended. Only ever call CreateDynamicLiveStats
                    // while a match is live (branch above) -- once it's finished, save final
                    // results straight from this same getallinfo response via
                    // createPostMtachStats, and only the first time: if PlayerStats/TeamPoints
                    // already exist for this match, an earlier poll already captured it, so skip
                    // straight past instead of saving again.
                    using var endedScope = serviceProvider1.CreateScope();
                    var tournamentBusinessForCheck = endedScope.ServiceProvider.GetRequiredService<TournamentBusiness>();
                    bool alreadyHasResults = await tournamentBusinessForCheck.HasResultsAsync(activeMatch);
                    if (!alreadyHasResults)
                    {
                        var livePlayerInfo = new LivePlayersList { PlayerInfoList = allInfo.TotalPlayerList };
                        var teamInfoList = new TeamInfoList { teamInfoList = allInfo.TeamInfoList };
                        await _dbBusiness.createPostMtachStats(livePlayerInfo, activeMatch, teamInfoList);
                        Console.WriteLine($"Saved final results for GameID {allInfo.GameID} (Match {activeMatch.MatchId}, Day {activeMatch.MatchDayId}).");

                        await db.StringSetAsync(HelperRedis.MatchStatus, "");
                        await subscriber.PublishAsync("match-status-channel", $"GameEnded:{activeMatch.Id}");
                    }

                    // Keep polling -- the next GameID change (a new match starting) is what moves
                    // this loop forward, not a manual restart.
                    await Task.Delay(3000);
                }
            }
        }

        private enum IncompleteMatchDecision
        {
            ContinueExisting,
            StartNew
        }

        /// <summary>
        /// Publishes a decision request on match-status-channel and blocks (this loop only, not
        /// the app) until Form1's subscriber writes an answer to
        /// HelperRedis.PendingMatchDecisionResponseKey. There's exactly one auto-tracking loop
        /// running at a time in this app, so a single pending-decision slot is enough -- no
        /// per-request id needed.
        /// </summary>
        private async Task<IncompleteMatchDecision> AskOperatorIncompleteMatchDecisionAsync(Match incompleteMatch, string newGameId)
        {
            var payload = JsonSerializer.Serialize(new PendingMatchDecisionInfo
            {
                MatchDbId = incompleteMatch.Id,
                MatchNumber = incompleteMatch.MatchId,
                DayId = incompleteMatch.MatchDayId,
                NewGameId = newGameId
            });

            await db.KeyDeleteAsync(HelperRedis.PendingMatchDecisionResponseKey);
            await db.StringSetAsync(HelperRedis.PendingMatchDecisionKey, payload);
            await subscriber.PublishAsync("match-status-channel", "IncompleteMatchNeedsDecision");

            while (true)
            {
                var response = await db.StringGetAsync(HelperRedis.PendingMatchDecisionResponseKey);
                if (!response.IsNullOrEmpty)
                {
                    await db.KeyDeleteAsync(HelperRedis.PendingMatchDecisionResponseKey);
                    return response == "Continue" ? IncompleteMatchDecision.ContinueExisting : IncompleteMatchDecision.StartNew;
                }
                await Task.Delay(1000);
            }
        }

        public async Task<int> GetCircleInfo()
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(_pcobUrl + "getcircleinfo");
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
        }

        //public async Task<int> GetCircleInfo(int count)
        //{
        //    vmixguidsclass vmixguids = await VmixDataUtils.SetVMIXDataoperations();
        //    string circleClosingGtzip = vmixguids.CircleClosing;
        //    _backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushCircleAnimationAsync(circleClosingGtzip, 2, true, count));
        //    return count;
        //}
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
        //private static readonly Dictionary<string, int[]> ZoneTimings = new()
        //{
        //    { "Erangel", new[] { 300, 200, 150, 120, 120, 90, 90, 60 } },
        //    { "Miramar", new[] { 300, 200, 150, 120, 120, 90, 90, 60 } },
        //    { "Sanhok", new[] { 180, 120, 105, 90, 90, 60, 60, 45 } }
        //};

        ////public async Task TrackCircleTiming(string mapName, int circleCount, int triggerTime)
        ////{
        ////    // Immediately trigger the animation for the current closing circle
        ////    vmixguidsclass vmixguids = await VmixDataUtils.SetVMIXDataoperations();
        ////    string circleClosingGtzip = vmixguids.CircleClosing;

        ////    _backgroundJobClient.Enqueue(() =>
        ////        vmi_layerSetOnOff.PushCircleAnimationAsync(circleClosingGtzip, 2, true, triggerTime, ZoneTimings, circleCount, mapName));

        ////}

    }
}
