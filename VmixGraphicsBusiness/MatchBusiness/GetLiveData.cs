
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VmixData.Models;
using VmixData.Models.MatchModels;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using static Google.Apis.Requests.BatchRequest;
using Microsoft.Extensions.Logging;
using Hangfire;
using System.Text.Json.Serialization;

namespace VmixGraphicsBusiness.MatchBusiness
{
    public class GetLiveData(LiveStatsBusiness liveStatsBusiness, PostMatch dbBusiness, IConfiguration configuration)
    {
        string _apiUrl = configuration["pcobUrl"]!;
        public async Task<bool> IsInGame()
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(_apiUrl + "isingame");
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

        [AutomaticRetry(Attempts = 0)]
        public async Task FetchAndPostData(Match match)
        {
            var previousData = "";

            using (var client = new HttpClient())
            {
                if (await IsInGame())
                {
                    try
                    {
                        var teampoints = await dbBusiness.fetchTeamPointsAsync(match);
                        var response = await client.GetAsync(_apiUrl + "gettotalplayerlist");
                        var response2 = await client.GetAsync(_apiUrl + "getteaminfolist");

                        if (response.IsSuccessStatusCode)
                        {
                            var data = await response.Content.ReadAsStringAsync();
                            var data2 = await response2.Content.ReadAsStringAsync();
                            if ( data != null)// data != previousData &&
                            {
                                LivePlayersList livePlayerInfo = JsonSerializer.Deserialize<LivePlayersList>(data)!;
                                TeamInfoList TeamInfoList = JsonSerializer.Deserialize<TeamInfoList>(data2)!;

                                await liveStatsBusiness.CreateLiveStats(livePlayerInfo, TeamInfoList, teampoints);
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