using System.Collections.Generic;
namespace Vmix_Hangfire_Graphics.Models;


public class PlayerInfoList
{
    public List<LivePlayerInfo> playerInfoList { get; set; }
}

public class LivePlayerInfo
{
    public long UId { get; set; }
    public string PlayerName { get; set; }
    public string PlayerOpenId { get; set; }
    public string PicUrl { get; set; }
    public bool ShowPicUrl { get; set; }
    public int TeamId { get; set; }
    public string TeamName { get; set; }
    public string Character { get; set; }
    public bool IsFiring { get; set; }
    public bool BHasDied { get; set; }

    public Location Location { get; set; }

    public int Health { get; set; }
    public int HealthMax { get; set; }
    public int LiveState { get; set; } // Might need further clarification on its meaning
    public int KillNum { get; set; }
    public int KillNumBeforeDie { get; set; }
    public long PlayerKey { get; set; }
    public int GotAirDropNum { get; set; }
    public int MaxKillDistance { get; set; }
    public int Damage { get; set; }
    public int KillNumInVehicle { get; set; }
    public int KillNumByGrenade { get; set; }
    public int Rank { get; set; }
    public bool IsOutsideBlueCircle { get; set; }
    public int InDamage { get; set; }
    public int Heal { get; set; }
    public int HeadShotNum { get; set; }
    public int SurvivalTime { get; set; }
    public int DriveDistance { get; set; }
    public int MarchDistance { get; set; }
    public int Assists { get; set; }
    public double OutsideBlueCircleTime { get; set; }
    public int Knockouts { get; set; }
    public int RescueTimes { get; set; }
    public int UseSmokeGrenadeNum { get; set; }
    public int UseFragGrenadeNum { get; set; }
    public int UseBurnGrenadeNum { get; set; }
    public int UseFlashGrenadeNum { get; set; }
    public int PoisonTotalDamage { get; set; }
}

public class Location
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}
