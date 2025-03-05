namespace Vmix_Hangfire_Graphics.Models;
public class PlayerStats
{
    public int Rank { get; set; }
    public string TeamName { get; set; }
    public string PlayerName { get; set; }
    public int TotalKills { get; set; }
    public int TotalAssists { get; set; }
    public int TotalKnockouts { get; set; }
    public int TotalDamage { get; set; }
    public int TotalHeadshots { get; set; }
    public int TotalNadeUse { get; set; }
    public int TotalSmoke { get; set; }
    public int TotalMoliUse { get; set; }
    public int TotalFlashUse { get; set; }
    public int TotalSurvTime { get; set; }
    public int Matches { get; set; }
    public int SurvivalTimeMinutes { get; set; }
    public int TotalThrowables { get; set; }
    public double AverageSurvivalTime { get; set; }
}
