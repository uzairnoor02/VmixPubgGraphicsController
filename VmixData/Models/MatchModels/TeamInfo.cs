using System.Collections.Generic;
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
public class FirstBlood
{
    public string PlayerId { get; set; }
    public DateTime DateTime { get; set; }
}

public class CircleInfo
{
    public int GameTime { get; set; }  // Game time (0 ~ xxx seconds)
    public int CircleStatus { get; set; }  // 0 = wait, 1 = delay, 2 = move
    public int CircleIndex { get; set; }  // Number of the circle
    public int Counter { get; set; }  // Current circle shrink counter
    public int MaxTime { get; set; }  // Current circle max shrink counter
}