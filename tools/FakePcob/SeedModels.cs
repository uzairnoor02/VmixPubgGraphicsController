using System.Text.Json.Serialization;

namespace FakePcob;

public sealed class SeedLocation
{
    [JsonPropertyName("x")] public long X { get; set; }
    [JsonPropertyName("y")] public long Y { get; set; }
    [JsonPropertyName("z")] public long Z { get; set; }

    public SeedLocation Clone() => new() { X = X, Y = Y, Z = Z };
}

/// One player's real end-of-match record. Field names/order/casing are the canonical 43 keys from
/// seed/SEEDS.md - including the two PascalCase outliers (AIKillNum, BossKillNum) and the three
/// at the tail (PoisonTotalDamage, UseSelfRescueTime, UseEmergencyCallTime), which the real capture
/// does NOT camelCase like the rest. Explicit [JsonPropertyName] on every field rather than a
/// naming policy, because no single policy produces both "uId" and "AIKillNum".
public sealed class SeedPlayer
{
    [JsonPropertyName("uId")] public long UId { get; set; }
    [JsonPropertyName("playerName")] public string PlayerName { get; set; } = "";
    [JsonPropertyName("playerOpenId")] public string PlayerOpenId { get; set; } = "";
    [JsonPropertyName("picUrl")] public string PicUrl { get; set; } = "";
    [JsonPropertyName("showPicUrl")] public bool ShowPicUrl { get; set; }
    [JsonPropertyName("teamId")] public int TeamId { get; set; }
    [JsonPropertyName("teamName")] public string TeamName { get; set; } = "";
    [JsonPropertyName("character")] public string Character { get; set; } = "None";
    [JsonPropertyName("isFiring")] public bool IsFiring { get; set; }
    [JsonPropertyName("bHasDied")] public bool BHasDied { get; set; }
    [JsonPropertyName("location")] public SeedLocation Location { get; set; } = new();
    [JsonPropertyName("health")] public int Health { get; set; }
    [JsonPropertyName("healthMax")] public int HealthMax { get; set; } = 100;
    [JsonPropertyName("liveState")] public int LiveState { get; set; }
    [JsonPropertyName("killNum")] public int KillNum { get; set; }
    [JsonPropertyName("killNumBeforeDie")] public int KillNumBeforeDie { get; set; }
    [JsonPropertyName("playerKey")] public long PlayerKey { get; set; }
    [JsonPropertyName("gotAirDropNum")] public int GotAirDropNum { get; set; }
    [JsonPropertyName("maxKillDistance")] public int MaxKillDistance { get; set; }
    [JsonPropertyName("damage")] public int Damage { get; set; }
    [JsonPropertyName("killNumInVehicle")] public int KillNumInVehicle { get; set; }
    [JsonPropertyName("killNumByGrenade")] public int KillNumByGrenade { get; set; }
    [JsonPropertyName("AIKillNum")] public int AIKillNum { get; set; }
    [JsonPropertyName("BossKillNum")] public int BossKillNum { get; set; }
    [JsonPropertyName("rank")] public int Rank { get; set; }
    [JsonPropertyName("isOutsideBlueCircle")] public bool IsOutsideBlueCircle { get; set; }
    [JsonPropertyName("inDamage")] public int InDamage { get; set; }
    [JsonPropertyName("heal")] public int Heal { get; set; }
    [JsonPropertyName("headShotNum")] public int HeadShotNum { get; set; }
    [JsonPropertyName("survivalTime")] public int SurvivalTime { get; set; }
    [JsonPropertyName("driveDistance")] public int DriveDistance { get; set; }
    [JsonPropertyName("marchDistance")] public int MarchDistance { get; set; }
    [JsonPropertyName("assists")] public int Assists { get; set; }
    [JsonPropertyName("outsideBlueCircleTime")] public double OutsideBlueCircleTime { get; set; }
    [JsonPropertyName("knockouts")] public int Knockouts { get; set; }
    [JsonPropertyName("rescueTimes")] public int RescueTimes { get; set; }
    [JsonPropertyName("useSmokeGrenadeNum")] public int UseSmokeGrenadeNum { get; set; }
    [JsonPropertyName("useFragGrenadeNum")] public int UseFragGrenadeNum { get; set; }
    [JsonPropertyName("useBurnGrenadeNum")] public int UseBurnGrenadeNum { get; set; }
    [JsonPropertyName("useFlashGrenadeNum")] public int UseFlashGrenadeNum { get; set; }
    [JsonPropertyName("PoisonTotalDamage")] public int PoisonTotalDamage { get; set; }
    [JsonPropertyName("UseSelfRescueTime")] public int UseSelfRescueTime { get; set; }
    [JsonPropertyName("UseEmergencyCallTime")] public int UseEmergencyCallTime { get; set; }
}

public sealed class SeedEnvelope
{
    [JsonPropertyName("playerInfoList")] public List<SeedPlayer> PlayerInfoList { get; set; } = new();
}
