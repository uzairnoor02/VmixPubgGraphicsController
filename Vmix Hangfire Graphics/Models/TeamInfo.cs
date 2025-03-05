using System.Collections.Generic;
namespace Vmix_Hangfire_Graphics.Models;

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
