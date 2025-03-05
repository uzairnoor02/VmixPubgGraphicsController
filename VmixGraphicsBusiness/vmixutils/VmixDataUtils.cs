using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmixGraphicsBusiness.vmixutils
{
    public class VmixDataUtils
    {
        public async Task<vmixguidsclass> SetVMIXDataoperations()
        {
            vmixguidsclass vmixguidsclass = new vmixguidsclass();
            VMIXDataoperations vMIXDataoperation = new VMIXDataoperations();
            var VMIXData = await vMIXDataoperation.GetVMIXData();
            //vmixguidsclass.LiverankingGuid = GetlIVElInputKey(VMIXData, "Live Rankings.gtzip");
            vmixguidsclass.LiverankingGuid16 = GetlIVElInputKey(VMIXData, "Live_Rankings_16.gtzip");
            vmixguidsclass.LiverankingGuid18 = GetlIVElInputKey(VMIXData, "Live_Rankings_18.gtzip");
            vmixguidsclass.LiverankingGuid20 = GetlIVElInputKey(VMIXData, "Live_Rankings_20.gtzip");
            vmixguidsclass.TeamEliminatedGuid = GetlIVElInputKey(VMIXData, "eliminated.gtzip") ?? "eliminated.gtzip";
            vmixguidsclass.GrenadePlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "Player Achievements grenade.gtzip") ?? "Player Achievements grenade.gtzip";
            vmixguidsclass.AirDropPlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "Player Achievements Airdrop.gtzip") ?? "Player Achievements Airdrop.gtzip";
            vmixguidsclass.VehiclePlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "Player Achievements vehicle.gtzip") ?? "Player Achievements vehicle.gtzip";
            vmixguidsclass.FirstBloodPlayerAcheivmentGuid = GetlIVElInputKey(VMIXData, "Player Achievements First Blood.gtzip") ?? "Player Achievements First Blood.gtzip";
            vmixguidsclass.WWCDTEAMSTATSGuid = GetlIVElInputKey(VMIXData, "WWCD TEAM STATS.gtzip") ?? "WWCD TEAM STATS.gtzip";
            vmixguidsclass.TeamsToWatchGUID = GetlIVElInputKey(VMIXData, "TEAMS TO WATCH.gtzip") ?? "TEAMS TO WATCH.gtzip";
            vmixguidsclass.OverAllRankingGUID = GetlIVElInputKey(VMIXData, "OVERALL RANKINGS.gtzip") ?? "OVERALL RANKINGS.gtzip";
            vmixguidsclass.MVPGUID = GetlIVElInputKey(VMIXData, "MVP.gtzip") ?? "MVP.gtzip";
            return vmixguidsclass;
        }
        static string GetlIVElInputKey(VmixData.Models.MatchModels.VmixData vmixData, string inputTitle)
        {
            // Find the input key where input title is "overall rankings"
            var overallInput = vmixData.Inputs.FirstOrDefault(input =>
                input.Title.Equals(inputTitle, StringComparison.OrdinalIgnoreCase) &&
                input.Type.Equals("GT", StringComparison.OrdinalIgnoreCase));

            return overallInput.Key;
        }
    }
    public class vmixguidsclass
    {
        //public string LiverankingGuid { get; set; }
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
        public string MVPGUID { get; set; }

    }

}
