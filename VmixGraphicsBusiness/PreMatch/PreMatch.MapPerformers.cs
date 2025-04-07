using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmixData.Models;
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness.PreMatch
{
    public partial class PreMatch
    {
        public async Task MapTopPerformers(Match matches,string mapName)
        {

            try
            {
                List<string> apiCalls = new List<string>();
                var topMapPerformers = _vmix_GraphicsContext.TeamPoints
                    .Where(x => x.StageId == matches.StageId && x.Map == mapName) // Replace dynamically
                    .GroupBy(x => x.TeamId)
                    .Select(g => new
                    {
                        TeamId = g.Key,
                        MatchIds = g.Select(x => x.MatchId).Distinct().ToList(),  // Collects all unique match IDs
                        PlacementPoints = g.Sum(x => x.PlacementPoints),
                        KillPoints = g.Sum(x => x.KillPoints),
                        TotalPoints = g.Sum(x => x.PlacementPoints) + g.Sum(x => x.KillPoints) // Sum of both points
                    })
                    .OrderByDescending(x => x.TotalPoints) // Order by combined points
                    .Take(4)
                    .ToList();


                var teamsdata = _vmix_GraphicsContext.Teams.Where(x => x.StageId == matches.StageId).ToList();

                var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
                int rankNum = 1;
                foreach (var team in topMapPerformers)
                {

                    var players = _vmix_GraphicsContext.PlayerStats
                        .Where(x => team.MatchIds.Contains(x.MatchId) && x.StageId == matches.StageId  && x.TeamId == team.TeamId)
                        .ToList();
                    var MatchesCount=players.GroupBy(x => new { x.MatchId, x.DayId }) // Grouping by MatchId & DayId
    .Select(g => new
    {
        MatchId = g.Key.MatchId,
        DayId = g.Key.DayId,
        PlayerCount = g.Count() // Count players in each group
    })
    .ToList();
                    var teamData = teamsdata.Where(x => x.TeamId == team.TeamId.ToString()).FirstOrDefault();
                    if (teamData == null)
                    {
                        continue;
                    }


                    int totalDistance = 0;
                    int totalSmokeGrenades = 0;
                    int totalFragGrenades = 0;
                    int totalBurnGrenades = 0;
                    int totalSurvivalTime = 0;
                    int TotalDamage = 0;
                    int playerCount = players.Count;

                    foreach (var player in players)
                    {
                        totalDistance += player.MarchDistance + player.DriveDistance;
                        totalSmokeGrenades += player.UseSmokeGrenadeNum;
                        totalFragGrenades += player.UseFragGrenadeNum;
                        totalBurnGrenades += player.useBurnGrenadeNum;
                        totalSurvivalTime += player.SurvivalTime;
                        TotalDamage += (int)player.Damage;
                    }
                    
                    totalSurvivalTime = totalSurvivalTime / playerCount;
                    
                    var survivalTime = TimeSpan.FromSeconds(totalSurvivalTime);
                    var survivalTimeString = $"{survivalTime.Minutes:D2}:{survivalTime.Seconds:D2}";
                    var title = $"TOP {mapName.ToUpper()} PERFORMERS";
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TopMapPerformers, $"MAPTITLE", $"{title}"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TopMapPerformers, $"TAGT{rankNum}", teamData.TeamName));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TopMapPerformers, $"SURVT{rankNum}", survivalTimeString));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TopMapPerformers, $"DAMAGET{rankNum}", TotalDamage.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TopMapPerformers, $"ELIMST{rankNum}", team.KillPoints.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TopMapPerformers, $"PLACET{rankNum}", team.PlacementPoints.ToString()));
                   // apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TopMapPerformers, $"TOTALT{rankNum}", team.TotalPoints.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.TopMapPerformers, $"LOGOT{rankNum}", $"{ConfigGlobal.LogosImages}\\0.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.TopMapPerformers, $"LOGOT{rankNum}", $"{ConfigGlobal.LogosImages}\\{teamData.TeamId}.png"));
                    rankNum++;
                }
                SetTexts setTexts = new SetTexts();
                await setTexts.CallMultipleApiAsync(apiCalls);
                var TopMapPerformers = vmixdata.TopMapPerformers;
                //await vmi_layerSetOnOff.PushAnimationAsync(TopMapPerformers, 4, true, 1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }




        }
    }
}
