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
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchSummaryGUID, "LELIM", matchSummary.LongestElim.ToString() ));
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
        public async Task DaySummary(Match matches)
        {
            try
            {
                // Get all matches for this day in the stage, ordered by match number
                var dayMatches = _vmix_GraphicsContext.Matches
                    .Where(x => x.StageId == matches.StageId && x.MatchDayId == matches.MatchDayId)
                    .OrderBy(x => x.MatchId)
                    .ToList();

                if (!dayMatches.Any())
                {
                    throw new InvalidOperationException("No matches found for Day Summary.");
                }

                var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
                List<string> apiCalls = new List<string>();

                int slot = 1;
                foreach (var match in dayMatches)
                {
                    // Get players from the winning team (rank 1 = chicken dinner)
                    var winningTeamPlayers = _vmix_GraphicsContext.PlayerStats
                        .Where(x => x.MatchId == match.MatchId && x.StageId == match.StageId && x.DayId == match.MatchDayId && x.Rank == 1).OrderBy(x => x.MatchId)
                        .ToList();

                    if (!winningTeamPlayers.Any()) continue;

                    // Get team info from first player
                    var winningTeamId = winningTeamPlayers.First().TeamId;
                    var teamData = _vmix_GraphicsContext.Teams.FirstOrDefault(x => x.TeamId == winningTeamId.ToString());
                    //var mapData = _vmix_GraphicsContext.Maps.FirstOrDefault(x => x.MapId == match.MapId);

                    // Aggregate team stats from all players
                    var totalElims = winningTeamPlayers.Sum(x => x.KillNum ?? 0);
                    var totalDamage = winningTeamPlayers.Sum(x => x.Damage ?? 0);
                    var totalKnocks = winningTeamPlayers.Sum(x => x.Knockouts ?? 0);
                    var totalHealing = winningTeamPlayers.Sum(x => x.Heal?? 0);

                    // Set match info
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.DaySummaryGUID, $"TAGT{slot}", teamData?.TeamName?.ToUpper() ?? "UNKNOWN"));

                    // Set stats
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.DaySummaryGUID, $"ELIMST{slot}", totalElims.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.DaySummaryGUID, $"DAMAGET{slot}", totalDamage.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.DaySummaryGUID, $"KNOCKST{slot}", totalKnocks.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.DaySummaryGUID, $"HEALINGT{slot}", totalHealing.ToString()));

                    // Set team logo
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.DaySummaryGUID, $"LOGOT{slot}", $"{ConfigGlobal.LogosImages}\\0.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.DaySummaryGUID, $"LOGOT{slot}", $"{ConfigGlobal.LogosImages}\\{teamData?.TeamId ?? "0"}.png"));

                    slot++;
                }

                // Clear remaining slots if fewer than 6 matches
                for (int i = slot; i <= 6; i++)
                {
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.DaySummaryGUID, $"MATCHNUM{i}", ""));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.DaySummaryGUID, $"MAPNAME{i}", ""));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.DaySummaryGUID, $"TEAMNAME{i}", ""));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.DaySummaryGUID, $"ELIMS{i}", ""));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.DaySummaryGUID, $"DAMAGE{i}", ""));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.DaySummaryGUID, $"KNOCKS{i}", ""));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.DaySummaryGUID, $"HEALING{i}", ""));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.DaySummaryGUID, $"TEAMLOGO{i}", $"{ConfigGlobal.LogosImages}\\0.png"));
                }

                SetTexts setTexts = new SetTexts();
                await setTexts.CallMultipleApiAsync(apiCalls);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in DaySummary: {ex}");
            }
        }
    }
}

