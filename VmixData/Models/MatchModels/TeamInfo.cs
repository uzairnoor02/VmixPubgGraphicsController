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