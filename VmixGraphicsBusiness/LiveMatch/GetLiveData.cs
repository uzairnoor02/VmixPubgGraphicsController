
using Hangfire;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;
using VmixData.Models;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness.PostMatchStats;

namespace VmixGraphicsBusiness.LiveMatch
{
    public class GetLiveData
    {
        private readonly LiveStatsBusiness _liveStatsBusiness;
        private readonly PostMatch _dbBusiness;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly string _pcobUrl;
        private List<LiveTeamPointStats> teampoints = null;

        public GetLiveData(LiveStatsBusiness liveStatsBusiness, PostMatch dbBusiness, IBackgroundJobClient backgroundJobClient)
        {
            _liveStatsBusiness = liveStatsBusiness;
            _dbBusiness = dbBusiness;
            _backgroundJobClient = backgroundJobClient;
            _pcobUrl = GlobalSettings.PcobUrl;
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

        [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 2 })]
        public async Task FetchAndPostData(Match match)
        {
            var previousData = "";
            if (teampoints is null)
            {
                teampoints = await _dbBusiness.fetchTeamPointsAsync(match);
            }
            using (var client = new HttpClient())
            {
                //if (await IsInGame())
                while(await IsInGame())
                {
                    try
                    {
                        var response = await client.GetAsync(_pcobUrl + "gettotalplayerlist");
                        var response2 = await client.GetAsync(_pcobUrl + "getteaminfolist");

                        if (response.IsSuccessStatusCode)
                        {
                            var data = await response.Content.ReadAsStringAsync();
                            var data2 = await response2.Content.ReadAsStringAsync();
                            if (data != null)// data != previousData &&
                            {
                                LivePlayersList livePlayerInfo = JsonSerializer.Deserialize<LivePlayersList>(data)!;
                                TeamInfoList TeamInfoList = JsonSerializer.Deserialize<TeamInfoList>(data2)!;

                                _backgroundJobClient.Enqueue(() => _liveStatsBusiness.CreateLiveStats(livePlayerInfo, TeamInfoList, teampoints));
                                previousData = data;
                            }
                            else
                            {
                                Console.WriteLine("No change in data.");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Failed to fetch data. Status code: {response.StatusCode}");
                        }
                        await Task.Delay(3000);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"An error occurred: {e.Message}");
                    }
                }
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