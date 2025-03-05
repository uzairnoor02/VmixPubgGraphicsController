
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

namespace VmixGraphicsBusiness.MatchBusiness
{
    public class GetLiveData(LiveStatsBusiness liveStatsBusiness,PostMatch dbBusiness,IConfiguration configuration)
    {
        string apiUrl = configuration["pcobUrl"]!;
        public async Task FetchAndPostData(Match match)
        { 
            var previousData = "";
            bool isingame = true;
            //string apiUrl = "https://9422-2a09-bac1-5b20-20-00-2e5-51.ngrok-free.app/";

            using ( var client = new HttpClient())
            {
                while (isingame)
                {
                    try
                    {
                        var teampoints = await dbBusiness.fetchTeamPointsAsync(match);
                        var response = await client.GetAsync(apiUrl + "gettotalplayerlist");
                        var response2 = await client.GetAsync(apiUrl + "getteaminfolist");

                        if (response.IsSuccessStatusCode)
                        {
                            var data = await response.Content.ReadAsStringAsync();
                            var data2 = await response2.Content.ReadAsStringAsync();
                            if (data != previousData && data != null)
                            {
                                LivePlayersList livePlayerInfo = JsonSerializer.Deserialize<LivePlayersList>(data)!;
                                TeamInfoList teamInfoListResponse = JsonSerializer.Deserialize<TeamInfoList>(data2)!;
                                if (livePlayerInfo.PlayerInfoList.Any(x => x.Rank == 0))
                                {
                                    Console.WriteLine(data);
                                    await liveStatsBusiness.CreateLiveStats(livePlayerInfo, teamInfoListResponse, teampoints);
                                    Task.Delay(3000);
                                    isingame = true;
                                }
                                else
                                {
                                    isingame = false;
                                    dbBusiness.createPostMtachStats(livePlayerInfo, match, teamInfoListResponse);

                                }

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
    
}