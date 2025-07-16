using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmixData.Models;
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    public partial class PostMatch
    {
        public async Task MatchSummary(Match matches)
        {
            try
            {
                var totalMatches = _vmix_GraphicsContext.Matches.Where(x => x.StageId == matches.StageId);

                List<string> apiCalls = new List<string>();
                var vmixdata = await VmixDataUtils.SetVMIXDataoperations();

                // Get all player stats for the current match
                var allPlayerStats = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId)
                    .ToList();

                // Calculate match summary statistics
                var matchSummary = new
                {
                    Eliminations = allPlayerStats.Sum(x => x.KillNum ?? 0),
                    Knocks = allPlayerStats.Sum(x => x.Knockouts ?? 0),
                    LongestElim = (allPlayerStats.Max(x => x.MaxKillDistance ?? 0)) + "M",
                    TotalHealings = allPlayerStats.Sum(x => x.Heal ?? 0),
                    Throwsused = allPlayerStats.Sum(x => x.UseFragGrenadeNum) + allPlayerStats.Sum(x => x.useBurnGrenadeNum) + allPlayerStats.Sum(x => x.UseSmokeGrenadeNum) ,
                     fragUsers = allPlayerStats.Sum(x => x.UseFragGrenadeNum),
                 burnUsers = allPlayerStats.Sum(x => x.useBurnGrenadeNum),
                 smokeUsers = allPlayerStats.Sum(x => x.UseSmokeGrenadeNum),
                AirDropsLooted = allPlayerStats.Sum(x => x.GotAirdropNum ?? 0),
                    Headshots = allPlayerStats.Sum(x => x.HeadshotNum ?? 0),
                    VehicleElims = allPlayerStats.Sum(x => x.KillNumInVehicle ?? 0)
                };

                // Set the match summary data to VMIX

                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, $"PMNUM", totalMatches.Count().ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "MATCHN", matches.MatchId.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "ELIMS", matchSummary.Eliminations.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "KNOCK", matchSummary.Knocks.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "LELIM", matchSummary.LongestElim.ToString() + "m"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "HEALING", matchSummary.TotalHealings.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "THROWSUSED", matchSummary.Throwsused.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "DROP", matchSummary.AirDropsLooted.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "HEAD", matchSummary.Headshots.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "ELIMSV", matchSummary.VehicleElims.ToString()));

                SetTexts setTexts = new SetTexts();
                await setTexts.CallMultipleApiAsync(apiCalls);

            }
            catch (Exception ex)
            {
                logger.LogError($"Error in MatchSummary: {ex}");
            }
        }
    }
}

