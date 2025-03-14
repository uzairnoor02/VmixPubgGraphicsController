using System.Xml.Serialization;

namespace VmixGraphicsBusiness.vmixutils;
public class SetTexts
{
    public async Task CallMultipleApiAsync(List<string> apiCalls)
    {
        using (HttpClient client = new HttpClient())
        {
            foreach (var apiCall in apiCalls)
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(apiCall);
                    response.EnsureSuccessStatusCode();
                    Console.WriteLine($"API call succeeded: {apiCall}");
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"Request error: {e.Message}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Unexpected error: {e.Message}");
                }
            }
        }
    }
    public async Task CallApiAsync(string apiCall)
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(apiCall);
                response.EnsureSuccessStatusCode();
                Console.WriteLine($"API call succeeded: {apiCall}");
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Request error: {e.Message}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected error: {e.Message}");
            }
        }
    }
}
