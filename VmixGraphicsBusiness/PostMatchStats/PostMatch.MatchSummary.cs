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
                    GrenadeElims = allPlayerStats.Sum(x => x.KillNumByGrenade ?? 0),
                    AirDropsLooted = allPlayerStats.Sum(x => x.GotAirdropNum ?? 0),
                    Headshots = allPlayerStats.Sum(x => x.HeadshotNum ?? 0),
                    VehicleElims = allPlayerStats.Sum(x => x.KillNumInVehicle ?? 0)
                };

                // Set the match summary data to VMIX
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "MATCHN", matches.MatchId.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "ELIMS", matchSummary.Eliminations.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "KNOCK", matchSummary.Knocks.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "LELIM", matchSummary.LongestElim.ToString() + "m"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "HEALING", matchSummary.TotalHealings.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "ELIMSG", matchSummary.GrenadeElims.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "DROP", matchSummary.AirDropsLooted.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "HEAD", matchSummary.Headshots.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "ELIMSV", matchSummary.VehicleElims.ToString()));

                SetTexts setTexts = new SetTexts();
                await setTexts.CallMultipleApiAsync(apiCalls);

                Console.WriteLine($"Match {matches.MatchId} Summary:");
                Console.WriteLine($"Total Eliminations: {matchSummary.Eliminations}");
                Console.WriteLine($"Total Knocks: {matchSummary.Knocks}");
                Console.WriteLine($"Longest Elimination: {matchSummary.LongestElim}m");
                Console.WriteLine($"Total Healings: {matchSummary.TotalHealings}");
                Console.WriteLine($"Grenade Eliminations: {matchSummary.GrenadeElims}");
                Console.WriteLine($"Air Drops Looted: {matchSummary.AirDropsLooted}");
                Console.WriteLine($"Headshots: {matchSummary.Headshots}");
                Console.WriteLine($"Vehicle Eliminations: {matchSummary.VehicleElims}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MatchSummary: {ex}");
            }
        }
    }
}

