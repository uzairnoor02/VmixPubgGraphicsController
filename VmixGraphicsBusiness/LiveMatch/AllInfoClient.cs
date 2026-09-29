using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using VmixData.Models.MatchModels;

namespace VmixGraphicsBusiness.LiveMatch
{
    /// <summary>
    /// Thin wrapper around pcob's `getallinfo` -- one shared HttpClient (avoids the
    /// new HttpClient()-per-call pattern used elsewhere), used both by the live poll loop
    /// (GetLiveData, replacing separate gettotalplayerlist + getteaminfolist calls) and by
    /// GameTracker's one-off check before a match starts.
    /// </summary>
    public static class AllInfoClient
    {
        private static readonly HttpClient _pcobClient = new HttpClient();

        /// <summary>Fetches getallinfo. Returns null on any failure (unreachable, bad JSON, etc.).</summary>
        public static async Task<AllInfo> FetchAsync(string pcobUrl)
        {
            try
            {
                var response = await _pcobClient.GetAsync(pcobUrl + "getallinfo");
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var wrapper = JsonSerializer.Deserialize<AllInfoWrapper>(json);
                return wrapper?.AllInfo;
            }
            catch
            {
                return null;
            }
        }
    }
}
