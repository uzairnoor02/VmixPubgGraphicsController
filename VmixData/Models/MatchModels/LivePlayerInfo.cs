using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace VmixData.Models.MatchModels
{
    public class LivePlayersList
    {
        [JsonPropertyName("playerInfoList")]
        public List<LivePlayerInfo> PlayerInfoList { get; set; }
    }

public class LivePlayerInfo
    {
        [JsonPropertyName("uId")]
        public long UId { get; set; }

        [JsonPropertyName("playerName")]
        public string PlayerName { get; set; }

        [JsonPropertyName("playerOpenId")]
        public string PlayerOpenId { get; set; }

        [JsonPropertyName("picUrl")]
        public string PicUrl { get; set; }

        [JsonPropertyName("showPicUrl")]
        public bool ShowPicUrl { get; set; }

        [JsonPropertyName("teamId")]
        public int TeamId { get; set; }

        [JsonPropertyName("teamName")]
        public string TeamName { get; set; }

        [JsonPropertyName("character")]
        public string Character { get; set; }

        [JsonPropertyName("isFiring")]
        public bool IsFiring { get; set; }

        [JsonPropertyName("bHasDied")]
        public bool BHasDied { get; set; }

        [JsonPropertyName("location")]
        public Location Location { get; set; }

        [JsonPropertyName("health")]
        public int Health { get; set; }

        [JsonPropertyName("healthMax")]
        public int HealthMax { get; set; }

        [JsonPropertyName("liveState")]
        public int LiveState { get; set; }

        [JsonPropertyName("killNum")]
        public int KillNum { get; set; }

        [JsonPropertyName("killNumBeforeDie")]
        public int KillNumBeforeDie { get; set; }

        [JsonPropertyName("playerKey")]
        public long PlayerKey { get; set; }

        [JsonPropertyName("gotAirDropNum")]
        public int GotAirDropNum { get; set; }

        [JsonPropertyName("maxKillDistance")]
        public int MaxKillDistance { get; set; }

        [JsonPropertyName("damage")]
        public int Damage { get; set; }

        [JsonPropertyName("killNumInVehicle")]
        public int KillNumInVehicle { get; set; }

        [JsonPropertyName("killNumByGrenade")]
        public int KillNumByGrenade { get; set; }

        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        [JsonPropertyName("isOutsideBlueCircle")]
        public bool IsOutsideBlueCircle { get; set; }

        [JsonPropertyName("inDamage")]
        public int InDamage { get; set; }

        [JsonPropertyName("heal")]
        public int Heal { get; set; }

        [JsonPropertyName("headShotNum")]
        public int HeadShotNum { get; set; }

        [JsonPropertyName("survivalTime")]
        public int SurvivalTime { get; set; }

        [JsonPropertyName("driveDistance")]
        public int DriveDistance { get; set; }

        [JsonPropertyName("marchDistance")]
        public int MarchDistance { get; set; }

        [JsonPropertyName("assists")]
        public int Assists { get; set; }

        [JsonPropertyName("outsideBlueCircleTime")]
        public double OutsideBlueCircleTime { get; set; }

        [JsonPropertyName("knockouts")]
        public int Knockouts { get; set; }

        [JsonPropertyName("rescueTimes")]
        public int RescueTimes { get; set; }

        [JsonPropertyName("useSmokeGrenadeNum")]
        public int UseSmokeGrenadeNum { get; set; }

        [JsonPropertyName("useFragGrenadeNum")]
        public int UseFragGrenadeNum { get; set; }

        [JsonPropertyName("useBurnGrenadeNum")]
        public int UseBurnGrenadeNum { get; set; }

        [JsonPropertyName("useFlashGrenadeNum")]
        public int UseFlashGrenadeNum { get; set; }

        [JsonPropertyName("poisonTotalDamage")]
        public int PoisonTotalDamage { get; set; }
    }

    public class Location
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("z")]
        public int Z { get; set; }
    }
}
