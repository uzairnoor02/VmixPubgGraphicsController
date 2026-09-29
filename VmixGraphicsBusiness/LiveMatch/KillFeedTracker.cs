using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness.Utils;

namespace VmixGraphicsBusiness.LiveMatch
{
    /// <summary>
    /// Wraps the real pcob `getkillinfo` endpoint. That endpoint returns the WHOLE match's
    /// kill/knock list on every call -- newest-first, cumulative, starting empty in a fresh
    /// ob.js (confirmed against the 20260923-232832 capture). This class turns that into
    /// "what's new since the last poll", so achievement handlers react to the actual kill
    /// event instead of guessing from whichever player's KillNum happened to move first in
    /// gettotalplayerlist that tick.
    ///
    /// Cost per poll: 1 pcob call + 2 cheap Redis ops (GET + SET of an integer), not one Redis
    /// round trip per kill row -- the feed only ever grows during a match, so remembering "how
    /// many rows we'd already seen" is enough to find exactly what's new.
    /// </summary>
    public static class KillFeedTracker
    {
        // One shared client for the pcob host, same pattern vmi_layerSetOnOff already uses for
        // vMix -- avoids the new HttpClient()-per-poll pattern used elsewhere in this codebase.
        private static readonly HttpClient _pcobClient = new HttpClient();

        /// <summary>Fetches getkillinfo and returns it oldest-first (pcob itself sends newest-first).</summary>
        public static async Task<List<KillInfoRow>> FetchAsync(string pcobUrl)
        {
            try
            {
                var response = await _pcobClient.GetAsync(pcobUrl + "getkillinfo");
                if (!response.IsSuccessStatusCode) return new List<KillInfoRow>();

                var json = await response.Content.ReadAsStringAsync();
                var wrapper = JsonSerializer.Deserialize<KillInfoWrapper>(json);
                var rows = wrapper?.KillInfo ?? new List<KillInfoRow>();

                return rows.OrderBy(r => r.CurGameTimeSeconds).ToList();
            }
            catch
            {
                // Best-effort poll: getkillinfo failing must never break the 1s stats tick.
                return new List<KillInfoRow>();
            }
        }

        /// <summary>
        /// Given this poll's full (oldest-first) row list, returns only the rows appended since
        /// the last poll for this match, and advances the "seen count" in Redis. Scoped by the
        /// match's DB id, so a new match always starts this back at zero without depending on a
        /// separate reset job to clear anything.
        ///
        /// If the row count ever goes DOWN (ob.js restarted mid-match -- the feed starts empty
        /// again), that's treated as a resync: nothing is replayed as if it just happened.
        /// </summary>
        public static async Task<List<KillInfoRow>> GetNewRowsAsync(IDatabase redis, int matchDbId, List<KillInfoRow> allRowsOldestFirst)
        {
            if (allRowsOldestFirst == null || allRowsOldestFirst.Count == 0)
                return new List<KillInfoRow>();

            var countKey = $"{HelperRedis.KillFeedSeenCountKey}:{matchDbId}";
            var storedValue = await redis.StringGetAsync(countKey);
            int lastSeenCount = storedValue.IsNullOrEmpty ? 0 : (int)storedValue;
            int currentCount = allRowsOldestFirst.Count;

            if (currentCount <= lastSeenCount)
            {
                if (currentCount != lastSeenCount)
                    await redis.StringSetAsync(countKey, currentCount, TimeSpan.FromHours(2));
                return new List<KillInfoRow>();
            }

            var newRows = allRowsOldestFirst.Skip(lastSeenCount).Take(currentCount - lastSeenCount).ToList();
            await redis.StringSetAsync(countKey, currentCount, TimeSpan.FromHours(2));
            return newRows;
        }
    }
}
