using System.Xml.Serialization;

namespace VmixGraphicsBusiness;
public class SetTexts
{
    public async Task CallApiAsync(List<string> apiCalls)
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
}
