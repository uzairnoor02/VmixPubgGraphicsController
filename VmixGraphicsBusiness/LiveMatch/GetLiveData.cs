
using Google.Apis.Sheets.v4.Data;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;
using VmixData.Models;
using VmixData.Models.MatchModels;
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
        int zonemoving=0;

        public GetLiveData(LiveStatsBusiness liveStatsBusiness, PostMatch dbBusiness, IBackgroundJobClient backgroundJobClient, IConnectionMultiplexer connectionMultiplexer, IServiceProvider serviceProvider)
        {
            _liveStatsBusiness = liveStatsBusiness;
            _dbBusiness = dbBusiness;
            _backgroundJobClient = backgroundJobClient;
            _pcobUrl = ConfigGlobal.PcobUrl;
            _redisConnection = connectionMultiplexer;
            serviceProvider1 = serviceProvider;

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
                    Console.WriteLine($"An error occurred while checking isingame status: {e.Message}");
                    return false;
                }
            }
        }

        [AutomaticRetry(Attempts = 0, DelaysInSeconds = new[] { 2 })]
        public async Task FetchAndPostData(Match match)
        {
            var previousData = "";
            if (teampoints is null)
            {
                teampoints = await _dbBusiness.fetchTeamPointsAsync(match);
            }
            using (var client = new HttpClient())
            {
                while (await IsInGame())
                {
                    GetCircleInfo();
                    try
                    {
                        var responsegetplayerData = await client.GetAsync(_pcobUrl + "gettotalplayerlist");
                        var responseTeamInfoList = await client.GetAsync(_pcobUrl + "getteaminfolist");


                        if (responsegetplayerData.IsSuccessStatusCode)
                        {
                            var PlayerData = await responsegetplayerData.Content.ReadAsStringAsync();
                            var teamdata = await responseTeamInfoList.Content.ReadAsStringAsync();
                            if (PlayerData != null && PlayerData != previousData)// PlayerData != previousData &&
                            {
                                LivePlayersList livePlayerInfo = JsonSerializer.Deserialize<LivePlayersList>(PlayerData)!;
                                TeamInfoList TeamInfoList = JsonSerializer.Deserialize<TeamInfoList>(teamdata)!;
                                var filteredPlayerInfo = new LivePlayersList
                                {
                                    PlayerInfoList = livePlayerInfo.PlayerInfoList.Select(player => new LivePlayerInfo
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

                                _backgroundJobClient.Enqueue(HangfireQueues.HighPriority, () => _liveStatsBusiness.CreateLiveStats(filteredPlayerInfo, TeamInfoList, teampoints));
                                previousData = PlayerData;
                                var db = _redisConnection.GetDatabase();
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
                            Console.WriteLine($"Failed to fetch PlayerData. Status code: {responsegetplayerData.StatusCode}");
                        }
                        await Task.Delay(1000);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"An error occurred: {e.Message}");
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
                _backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(liverakiingguid20, 4, false, 789));
                _backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushAnimationAsync(liverakiingguid18, 4, false, 7897));

                await Task.Delay(10000);
                var responsegetplayerDatapost = await client.GetAsync(_pcobUrl + "gettotalplayerlist");
                var responseTeamInfoListpost = await client.GetAsync(_pcobUrl + "getteaminfolist");
                string PlayerDatapost, teamdatapost;

                LivePlayersList livePlayerInfoPost = new();
                TeamInfoList TeamInfoListPost = new();
                if (responsegetplayerDatapost.IsSuccessStatusCode)
                {


                    PlayerDatapost = await responsegetplayerDatapost.Content.ReadAsStringAsync();
                    teamdatapost = await responseTeamInfoListpost.Content.ReadAsStringAsync();
                    LivePlayersList livePlayerInfo = JsonSerializer.Deserialize<LivePlayersList>(PlayerDatapost)!;
                    TeamInfoList TeamInfoList = JsonSerializer.Deserialize<TeamInfoList>(teamdatapost)!;
                    _dbBusiness.createPostMtachStats(livePlayerInfo!, match, TeamInfoList!);
                }
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
                        if (circleInfo.CircleStatus=="2" && zonemoving == 0 && int.Parse(circleInfo.CircleIndex) < 6 && (int.Parse(circleInfo.MaxTime) - int.Parse(circleInfo.Counter)) <= 17)
                        {
                            Console.WriteLine("maxtime:" + circleInfo.MaxTime + "shrinkprogress=" + circleInfo.Counter);
                            zonemoving = 1;
                            _backgroundJobClient.Enqueue(() => vmi_layerSetOnOff.PushCircleAnimationAsync(circleClosingGtzip, 2, true, (int.Parse(circleInfo.MaxTime) - int.Parse(circleInfo.Counter)-3)));
                            zonemoving = 1;
                        }
                        if(circleInfo.CircleStatus == "0" && zonemoving == 1)
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
