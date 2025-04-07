using System.Collections.Generic;
using System.Text.Json.Serialization;
namespace VmixData.Models.MatchModels;

public class TeamInfo
{
    public int teamId { get; set; }
    public bool isShowLogo { get; set; }
    public string logoPicUrl { get; set; }
    public int killNum { get; set; }
    public int liveMemberNum { get; set; }
}

public class TeamInfoList
{
    public List<TeamInfo> teamInfoList { get; set; }
}

public class VehicleEliminationInfo
{
    public string PlayerId { get; set; }
    public int VehicleKills { get; set; }
    public DateTime DateTime { get; set; }
}
public class GrenadeEliminationInfo
{
    public string PlayerId { get; set; }
    public int GrenadeKills { get; set; }
    public DateTime DateTime { get; set; }
}
public class AirDropLootedInfo
{
    public string PlayerId { get; set; }
    public int AirdropLootedNumber { get; set; }
    public DateTime DateTime { get; set; }
}
public class killDominationInfo
{
    public string PlayerId { get; set; }
    public int? KillInfo { get; set; }
    public DateTime DateTime { get; set; }
}
public class DamageDominationInfo
{
    public string PlayerId { get; set; }
    public int? DamageInfo { get; set; }
    public DateTime DateTime { get; set; }
}
public class FirstBlood
{
    public string PlayerId { get; set; }
    public DateTime DateTime { get; set; }
}


public class CircleDataWrapper
{
    [JsonPropertyName("circleInfo")]
    public CircleInfo CircleInfo { get; set; }
}

public class CircleInfo
{
    [JsonPropertyName("GameTime")]
    public string GameTime { get; set; }

    [JsonPropertyName("CircleStatus")]
    public string CircleStatus { get; set; }

    [JsonPropertyName("CircleIndex")]
    public string CircleIndex { get; set; }

    [JsonPropertyName("Counter")]
    public string Counter { get; set; }

    [JsonPropertyName("MaxTime")]
    public string MaxTime { get; set; }
}
