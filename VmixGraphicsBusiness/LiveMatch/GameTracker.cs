using System;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness.Utils;

namespace VmixGraphicsBusiness.LiveMatch
{
    /// <summary>
    /// Tracks pcob's own GameID (from getallinfo) as a second key alongside the app's own
    /// Match record, so "has this specific PUBG game already been tracked, and did it end"
    /// can be answered from pcob's own clock -- not only from whichever Match/Tournament/
    /// Stage/Day slot the operator happened to pick in the UI, and not only from whether this
    /// app process was running continuously enough to see the match finish itself.
    /// </summary>
    public static class GameTracker
    {
        public enum GameStartDecision
        {
            /// No prior record for this GameID, or a prior record exists but pcob says the
            /// game hasn't ended -- safe to start/resume tracking without asking anything.
            Proceed,

            /// A prior record exists for this GameID AND pcob's own getallinfo says it has
            /// already ended -- caller must ask the operator whether to delete the old data.
            AlreadyEndedNeedsConfirmation,

            /// getallinfo couldn't be reached/parsed right now -- caller decides how to
            /// proceed (this check is best-effort and must never block starting a match
            /// pcob itself is unreachable for other reasons too).
            FetchFailed
        }

        public class GameTrackingRecord
        {
            public string GameId { get; set; }
            public int MatchDbId { get; set; }
            public DateTime FirstSeenUtc { get; set; }
            public DateTime LastSeenUtc { get; set; }
        }

        /// <summary>
        /// Call this once when the operator presses Start, before enqueuing the poll job.
        /// Fetches getallinfo live (not from any cached state) and checks Redis for a prior
        /// record under that GameID.
        /// </summary>
        public static async Task<(GameStartDecision decision, AllInfo allInfo, GameTrackingRecord existing)> EvaluateStartAsync(IDatabase redis, string pcobUrl)
        {
            var allInfo = await AllInfoClient.FetchAsync(pcobUrl);
            if (allInfo == null || string.IsNullOrEmpty(allInfo.GameID))
                return (GameStartDecision.FetchFailed, null, null);

            var existing = await GetRecordAsync(redis, allInfo.GameID);

            if (existing == null)
                return (GameStartDecision.Proceed, allInfo, null);

            // Trust pcob's own live FinishedStartTime over anything stored locally -- it's
            // authoritative even if this app crashed or wasn't running when the game ended.
            if (!allInfo.HasEnded)
                return (GameStartDecision.Proceed, allInfo, existing);

            return (GameStartDecision.AlreadyEndedNeedsConfirmation, allInfo, existing);
        }

        /// <summary>Creates or refreshes the tracking record for this GameID.</summary>
        public static async Task RegisterOrResumeAsync(IDatabase redis, string gameId, int matchDbId)
        {
            if (string.IsNullOrEmpty(gameId)) return;

            var record = await GetRecordAsync(redis, gameId) ?? new GameTrackingRecord
            {
                GameId = gameId,
                MatchDbId = matchDbId,
                FirstSeenUtc = DateTime.UtcNow
            };
            record.LastSeenUtc = DateTime.UtcNow;

            await SaveRecordAsync(redis, record);
        }

        /// <summary>
        /// Looks up the DB Match.Id already tracked for a pcob GameID, if any -- used by the
        /// auto-tracking loop to tell "still the match we're already following" apart from
        /// "GameID changed, resolve/create the Match row for it" without re-fetching getallinfo.
        /// </summary>
        public static async Task<int?> GetTrackedMatchDbIdAsync(IDatabase redis, string gameId)
        {
            var record = await GetRecordAsync(redis, gameId);
            return record?.MatchDbId;
        }

        /// <summary>Removes the tracking record for a GameID -- call after the operator confirms deleting old data.</summary>
        public static async Task ClearAsync(IDatabase redis, string gameId)
        {
            if (string.IsNullOrEmpty(gameId)) return;
            await redis.KeyDeleteAsync($"{HelperRedis.GameTrackingKey}:{gameId}");
        }

        private static async Task<GameTrackingRecord> GetRecordAsync(IDatabase redis, string gameId)
        {
            var value = await redis.StringGetAsync($"{HelperRedis.GameTrackingKey}:{gameId}");
            if (value.IsNullOrEmpty) return null;
            try
            {
                return JsonSerializer.Deserialize<GameTrackingRecord>(value);
            }
            catch
            {
                return null;
            }
        }

        private static async Task SaveRecordAsync(IDatabase redis, GameTrackingRecord record)
        {
            await redis.StringSetAsync(
                $"{HelperRedis.GameTrackingKey}:{record.GameId}",
                JsonSerializer.Serialize(record),
                TimeSpan.FromDays(14));
        }
    }
}
