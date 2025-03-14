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
            vmixguidsclass.LiverankingGuid4 = GetlIVElInputKey(VMIXData, "Live_Rankings_4.gtzip");
            vmixguidsclass.LiverankingGuid16 = GetlIVElInputKey(VMIXData, "Live_Rankings_16.gtzip")?? "LIVE_RANKINGS_16.gtzip";//
            vmixguidsclass.LiverankingGuid18 = GetlIVElInputKey(VMIXData, "Live_Rankings_18.gtzip");
            vmixguidsclass.LiverankingGuid20 = GetlIVElInputKey(VMIXData, "Live_Rankings_20.gtzip");
            vmixguidsclass.TeamEliminatedGuid = GetlIVElInputKey(VMIXData, "eliminated.gtzip") ?? "eliminated.gtzip";
            vmixguidsclass.GrenadePlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "Grenade_kill.gtzip") ?? "Player Achievements grenade.gtzip";
            vmixguidsclass.AirDropPlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "Airdrop_looted.gtzip") ?? "Player Achievements Airdrop.gtzip";
            vmixguidsclass.VehiclePlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "Vehicle_kill.gtzip") ?? "Vehicle_kill.gtzip";
            vmixguidsclass.FirstBloodPlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "First_Blood.gtzip") ?? "Player Achievements First Blood.gtzip";
            vmixguidsclass.WWCDTEAMSTATSGuid = GetlIVElInputKey(VMIXData, "WWCD TEAM STATS.gtzip") ?? "WWCD TEAM STATS.gtzip";
            vmixguidsclass.TeamsToWatchGUID = GetlIVElInputKey(VMIXData, "TEAMS TO WATCH.gtzip") ?? "TEAMS TO WATCH.gtzip";
            vmixguidsclass.OverAllRankingGUID = GetlIVElInputKey(VMIXData, "OVERALL RANKINGS.gtzip") ?? "OVERALL RANKINGS.gtzip";
            vmixguidsclass.MatchRankingsGUID = GetlIVElInputKey(VMIXData, "MATCH RANKINGS.gtzip") ?? "MATCH RANKINGS.gtzip";
            vmixguidsclass.MVPGUID = GetlIVElInputKey(VMIXData, "MVP OF THE MATCH.gtzip") ?? "MVP OF THE MATCH.gtzip";
            vmixguidsclass.CircleClosing = GetlIVElInputKey(VMIXData, "CircleClosing.gtzip") ?? "CircleClosing.gtzip";
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
        public string MatchRankingsGUID { get; set; }
        public string MVPGUID { get; set; }
        public string CircleClosing { get; set; }

    }

}
