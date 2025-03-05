using Hangfire;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Vmix_Hangfire_Graphics.Business
{
    public class GetTotalPlayersHangfireJobBusiness(IConfiguration configuration, IBackgroundJobClient backgroundJobClient, LiveStatsBusiness liveStatsBusiness)
    {
        public void ScheduleRecurringJob()
        {
            try
            {
                // Schedule the recurring job using Hangfire
                RecurringJob.AddOrUpdate(
                    () => FetchAndPostData(configuration["HangfireJobSettings:ApiUrl"], backgroundJobClient, liveStatsBusiness),
                    "* * * * * *"); // Schedule to run every second
            }
            catch (Exception ex)
            {Console.WriteLine(ex.Message);
            }
        }

        public async Task FetchAndPostData(string apiUrl, IBackgroundJobClient backgroundJobClient, LiveStatsBusiness liveStatsBusiness)
        {
            var previousData = "";

            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var data = await response.Content.ReadAsStringAsync();

                        if (data != previousData && data != null)
                        {
                            Models.PlayerInfoList livePlayerInfo = JsonSerializer.Deserialize<Models.PlayerInfoList>(data)!;
                            Console.WriteLine(data);
                            backgroundJobClient.Enqueue(() => liveStatsBusiness.CreateLiveStats(livePlayerInfo));
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
