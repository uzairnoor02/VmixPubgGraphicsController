using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmixGraphicsBusiness.vmixutils
{
    public static class VmixDataUtils
    {
        public static async Task<vmixguidsclass> SetVMIXDataoperations()
        {
            vmixguidsclass vmixguidsclass = new vmixguidsclass();
            VMIXDataoperations vMIXDataoperation = new VMIXDataoperations();
            var VMIXData = await vMIXDataoperation.GetVMIXData();
            //vmixguidsclass.LiverankingGuid4 = GetlIVElInputKey(VMIXData, "Live_Rankings_4.gtzip");
            //vmixguidsclass.LiverankingGuid16 = GetlIVElInputKey(VMIXData, "Live_Rankings_16.gtzip")?? "LIVE_RANKINGS_16.gtzip";//
            //vmixguidsclass.LiverankingGuid18 = GetlIVElInputKey(VMIXData, "Live_Rankings_18.gtzip");
            //vmixguidsclass.LiverankingGuid20 = GetlIVElInputKey(VMIXData, "Live_Rankings_20.gtzip");
            //vmixguidsclass.TeamEliminatedGuid = GetlIVElInputKey(VMIXData, "eliminated.gtzip") ?? "eliminated.gtzip";
            //vmixguidsclass.GrenadePlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "Grenade_kill.gtzip") ?? "Player Achievements grenade.gtzip";
            //vmixguidsclass.AirDropPlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "Airdrop_looted.gtzip") ?? "Player Achievements Airdrop.gtzip";
            //vmixguidsclass.VehiclePlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "Vehicle_kill.gtzip") ?? "Vehicle_kill.gtzip";
            //vmixguidsclass.FirstBloodPlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "First_Blood.gtzip") ?? "Player Achievements First Blood.gtzip";
            //vmixguidsclass.WWCDTEAMSTATSGuid = GetlIVElInputKey(VMIXData, "WWCD TEAM STATS.gtzip") ?? "WWCD TEAM STATS.gtzip";
            //vmixguidsclass.TeamsToWatchGUID = GetlIVElInputKey(VMIXData, "TEAMS TO WATCH.gtzip") ?? "TEAMS TO WATCH.gtzip";
            //vmixguidsclass.OverAllRankingGUID = GetlIVElInputKey(VMIXData, "OVERALL RANKINGS.gtzip") ?? "OVERALL RANKINGS.gtzip";
            //vmixguidsclass.MatchRankingsGUID = GetlIVElInputKey(VMIXData, "MATCH RANKINGS.gtzip") ?? "MATCH RANKINGS.gtzip";
            //vmixguidsclass.MVPGUID = GetlIVElInputKey(VMIXData, "MVP OF THE MATCH.gtzip") ?? "MVP OF THE MATCH.gtzip";
            //vmixguidsclass.CircleClosing = GetlIVElInputKey(VMIXData, "zoneclosingin.gtzip") ?? "zoneclosingin.gtzip";
            //vmixguidsclass.TopMapPerformers = GetlIVElInputKey(VMIXData, "TOP MAP PERFORMERS.gtzip") ?? "TOP MAP PERFORMERS.gtzip";
            //vmixguidsclass.WWCDoverlay = GetlIVElInputKey(VMIXData, "WWCD OVERLAY TEAM.gtzip") ?? "WWCD OVERLAY TEAM.gtzip";
            //vmixguidsclass.MatchSummaryGUID = GetlIVElInputKey(VMIXData, "WWCD OVERLAY TEAM.gtzip") ?? "WWCD OVERLAY TEAM.gtzip";
            //vmixguidsclass.Top5Grenadiers = GetlIVElInputKey(VMIXData, "WWCD OVERLAY TEAM.gtzip") ?? "WWCD OVERLAY TEAM.gtzip";
            //return vmixguidsclass;
            vmixguidsclass.LiverankingGuid4 = GetlIVElInputKey(VMIXData, "Live_Rankings_4.gtzip");
            vmixguidsclass.LiverankingGuid16 = GetlIVElInputKey(VMIXData, "Live_Rankings_16.gtzip") ?? "LIVE_RANKINGS_16.gtzip";//
            vmixguidsclass.LiverankingGuid18 = GetlIVElInputKey(VMIXData, "Live_Rankings_18.gtzip");
            vmixguidsclass.LiverankingGuid20 = GetlIVElInputKey(VMIXData, "Live_Rankings_20.gtzip");
            vmixguidsclass.TeamEliminatedGuid = GetlIVElInputKey(VMIXData, "Eliminated.gtzip") ?? "eliminated.gtzip";
            vmixguidsclass.GrenadePlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "Grenade_kill.gtzip") ?? "Player Achievements grenade.gtzip";
            vmixguidsclass.AirDropPlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "Airdrop_Looted.gtzip") ?? "Player Achievements Airdrop.gtzip";
            vmixguidsclass.VehiclePlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "Vehicle_kill.gtzip") ?? "Vehicle_kill.gtzip";
            vmixguidsclass.FirstBloodPlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "First_Blood.gtzip") ?? "Player Achievements First Blood.gtzip";
            vmixguidsclass.WWCDTEAMSTATSGuid = GetlIVElInputKey(VMIXData, "WWCD TEAM STATS.gtzip") ?? "WWCD TEAM STATS.gtzip";
            vmixguidsclass.TeamsToWatchGUID = GetlIVElInputKey(VMIXData, "TEAMS TO WATCH.gtzip") ?? "TEAMS TO WATCH.gtzip";
            vmixguidsclass.OverAllRankingGUID = GetlIVElInputKey(VMIXData, "OVERALL RANKINGS.gtzip") ?? "OVERALL RANKINGS.gtzip";
            vmixguidsclass.OverAllRankingGUID1 = GetlIVElInputKey(VMIXData, "OVERALL RANKINGS P1.gtzip") ?? "OVERALL RANKINGS.gtzip";
            vmixguidsclass.OverAllRankingGUID2 = GetlIVElInputKey(VMIXData, "OVERALL RANKINGS P2.gtzip") ?? "OVERALL RANKINGS.gtzip";
            vmixguidsclass.OverAllRankingGUID3 = GetlIVElInputKey(VMIXData, "OVERALL RANKINGS P3.gtzip") ?? "OVERALL RANKINGS.gtzip";
            vmixguidsclass.MatchRankingsGUID = GetlIVElInputKey(VMIXData, "MATCH RANKINGS.gtzip") ?? "MATCH RANKINGS.gtzip";
            vmixguidsclass.MVPGUID = GetlIVElInputKey(VMIXData, "MATCH MVP.gtzip") ?? "MVP OF THE MATCH.gtzip";
            vmixguidsclass.Top5MatchMVPGUID = GetlIVElInputKey(VMIXData, "TOP 5 PLAYERS.gtzip") ?? "TOP 5 PLAYERS.gtzip";
            vmixguidsclass.Top5StageMVPGUID = GetlIVElInputKey(VMIXData, "TOP 5 PLAYERS OVERALL STAGE.gtzip") ?? "TOP 5 PLAYERS STAGE.gtzip";
            vmixguidsclass.CircleClosing = GetlIVElInputKey(VMIXData, "ZONECLOSING.gtzip") ?? "zoneclosingin.gtzip";
            vmixguidsclass.TopMapPerformers = GetlIVElInputKey(VMIXData, "TOP MAP PERFORMERS.gtzip") ?? "TOP MAP PERFORMERS.gtzip";
            vmixguidsclass.WWCDoverlay = GetlIVElInputKey(VMIXData, "WWCD OVERLAY TEAM.gtzip") ?? "WWCD OVERLAY TEAM.gtzip";
            vmixguidsclass.MatchSummaryGUID = GetlIVElInputKey(VMIXData, "MATCH SUMMARY.gtzip") ?? "Match Summary.gtzip";
            vmixguidsclass.Top5Grenadiers = GetlIVElInputKey(VMIXData, "TOP 5 GRENADIERS.gtzip") ?? "TOP 5 Grenadiers.gtzip";
            return vmixguidsclass;
        }
        public static string GetlIVElInputKey(VmixData.Models.MatchModels.VmixData vmixData, string inputTitle)
        {
            try
            {
                // Find the input key where input title is "overall rankings"
                var overallInput = vmixData.Inputs.FirstOrDefault(input =>
                    input.Title.Equals(inputTitle, StringComparison.OrdinalIgnoreCase) &&
                    input.Type.Equals("GT", StringComparison.OrdinalIgnoreCase));
                return overallInput?.Key ?? null;
            }
            catch
            {
                return null;
            }
        }
    }
    public class vmixguidsclass
    {
        public string LiverankingGuid4 { get; set; }
        public string LiverankingGuid16 { get; set; }
        public string LiverankingGuid18 { get; set; }
        public string LiverankingGuid20 { get; set; }
        public string TeamEliminatedGuid { get; set; }
        public string GrenadePlayerAcheivmentGuid { get; set; }
        public string AirDropPlayerAcheivmentGuid { get; set; }
        public string VehiclePlayerAcheivmentGuid { get; set; }
        public string FirstBloodPlayerAcheivmentGuid { get; set; }
        public string WWCDTEAMSTATSGuid { get; set; }
        public string TeamsToWatchGUID { get; set; }
        public string OverAllRankingGUID { get; set; }
        public string OverAllRankingGUID1 { get; set; }
        public string OverAllRankingGUID2 { get; set; }
        public string OverAllRankingGUID3 { get; set; }
        public string MatchRankingsGUID { get; set; }
        public string MVPGUID { get; set; }
        public string CircleClosing { get; set; }
        public string WWCDoverlay { get; set; }
        public string TopMapPerformers { get; set; }
        public string MatchSummaryGUID { get; set; }
        public string Top5MVPMatchGUID { get; set; }
        public string Top5MVPStageGUID { get; set; }
        public string Top5Grenadiers { get; set; }
        public string Top5MatchMVPGUID { get; set; }
        public string Top5StageMVPGUID { get; set; }



    }

}
