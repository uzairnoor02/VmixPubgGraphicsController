
using Hangfire;
using Microsoft.Extensions.Configuration;
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
        private readonly vmi_layerSetOnOff _vmi_LayerSetOnOff;
        private List<LiveTeamPointStats> teampoints = null;

        public GetLiveData(LiveStatsBusiness liveStatsBusiness, PostMatch dbBusiness, IBackgroundJobClient backgroundJobClient, IConnectionMultiplexer connectionMultiplexer, vmi_layerSetOnOff vmi_LayerSetOnOff)
        {
            _liveStatsBusiness = liveStatsBusiness;
            _dbBusiness = dbBusiness;
            _backgroundJobClient = backgroundJobClient;
            _pcobUrl = ConfigGlobal.PcobUrl;
            _redisConnection = connectionMultiplexer;
            _vmi_LayerSetOnOff = vmi_LayerSetOnOff;
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
                        return true;// isInGameResponse?.IsInGame ?? false;
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

                                _backgroundJobClient.Enqueue(() => _liveStatsBusiness.CreateLiveStats(livePlayerInfo, TeamInfoList, teampoints));
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
                _vmi_LayerSetOnOff.PushAnimationAsync(liverakiingguid16, 3, false, 1);
                _vmi_LayerSetOnOff.PushAnimationAsync(liverakiingguid4, 3, false, 1);
                _vmi_LayerSetOnOff.PushAnimationAsync(liverakiingguid20, 3, false, 1);
                _vmi_LayerSetOnOff.PushAnimationAsync(liverakiingguid18, 3, false, 1);


                var responsegetplayerDatapost = await client.GetAsync(_pcobUrl + "gettotalplayerlist");
                var responseTeamInfoListpost = await client.GetAsync(_pcobUrl + "getteaminfolist");
                string PlayerDatapost, teamdatapost;

                LivePlayersList livePlayerInfoPost = new();
                TeamInfoList TeamInfoListPost = new();
                if (responsegetplayerDatapost.IsSuccessStatusCode)
                {

                    var db = _redisConnection.GetDatabase();

                    PlayerDatapost = await responsegetplayerDatapost.Content.ReadAsStringAsync();
                    teamdatapost = await responseTeamInfoListpost.Content.ReadAsStringAsync();
                    await db.StringSetAsync(HelperRedis.PlayerInfolist, PlayerDatapost);
                    await db.StringSetAsync(HelperRedis.TeamInfoList, teamdatapost);
                    LivePlayersList livePlayerInfo = JsonSerializer.Deserialize<LivePlayersList>(PlayerDatapost)!;
                    TeamInfoList TeamInfoList = JsonSerializer.Deserialize<TeamInfoList>(teamdatapost)!;
                    _dbBusiness.createPostMtachStats(livePlayerInfo!, match, TeamInfoList!);
                }
            }

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