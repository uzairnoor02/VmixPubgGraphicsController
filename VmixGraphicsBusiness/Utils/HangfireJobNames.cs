using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmixGraphicsBusiness.Utils
{
    public static class HangfireJobNames
    {
        public const string FetchAndPostDataJob = "FetchAndPostDataJob";

    }

    public static class HelperRedis
    {
        public const string VehicleEliminationsKey = "VehicleEliminations";
        public const string GrenadeEliminationsKey = "GrenadeEliminations";
        public const string AirDropLootedKey = "AirDropLooted";
        public const string FirstBloodKey = "FirstBlood";
        public const string isEliminated = "isEliminated";
        public const string TeamInfoList = "TeamInfoList";
        public const string PlayerInfolist = "PlayerInfolist";
        public const string LiveRankingGuid = "LiveRankingGuid";
        public const string KillDominationKey = "KillDominationKey";
        public const string DamageDominationKey = "DamageDominationKey";
        public const string MatchStatus = "MatchStatus";
        public const string MatchId = "MatchId";
        public const string KillFeedSeenCountKey = "KillFeedSeenCount"; // scoped per match: $"{KillFeedSeenCountKey}:{matchDbId}"
        public const string GameTrackingKey = "GameTracking"; // scoped per pcob GameID: $"{GameTrackingKey}:{gameId}"
        public const string AchievementCheckTimeKey = "AchievementCheckTime"; // scoped per match: $"{AchievementCheckTimeKey}:{matchDbId}" -- last getallinfo CurrentTime we ran Airdrop/Vehicle checks for
        public const string GrenadeLastKillTimeKey = "GrenadeLastKillTime"; // scoped per match: $"{GrenadeLastKillTimeKey}:{matchDbId}" -- highest CurGameTime of a grenade kill already shown
        public const string Top4TeamPositionsKey = "Top4TeamPositions"; // scoped per match: $"{Top4TeamPositionsKey}:{matchDbId}" -- fixed T1-T4 slot assignment for the Top 4 overlay
        public const string PendingMatchDecisionKey = "PendingMatchDecision"; // JSON details of an unresolved "match never concluded" prompt RunAutoTrackingAsync is waiting on
        public const string PendingMatchDecisionResponseKey = "PendingMatchDecisionResponse"; // operator's answer to the above: "Continue" or "NewMatch"
    }

    /// <summary>Payload stored at HelperRedis.PendingMatchDecisionKey -- shared shape between GetLiveData (writer) and Form1 (reader).</summary>
    public class PendingMatchDecisionInfo
    {
        public int MatchDbId { get; set; }
        public int MatchNumber { get; set; }
        public int DayId { get; set; }
        public string NewGameId { get; set; }
    }
}
