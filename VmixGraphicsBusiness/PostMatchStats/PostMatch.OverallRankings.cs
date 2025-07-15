using StackExchange.Redis;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using VmixData.Models;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness.Utils;
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    public partial class PostMatch
    {
        public async Task OverallRankings(Match matches)
        {
            try
            {
                List<string> apiCalls = new List<string>();
                var teamRankings = _vmix_GraphicsContext.TeamPoints
    .Where(x => x.StageId == matches.StageId)
    .GroupBy(x => x.TeamId)
    .Select(g => new
    {
        TeamId = g.Key,
        TotalPoints = g.Sum(x => x.TotalPoints),
        PlacementPoints = g.Sum(x => x.PlacementPoints),
        WWCD = g.Sum(x => x.WWCD),
        KillPoints = g.Sum(x => x.KillPoints)
    })
    .OrderByDescending(x => x.TotalPoints)
    .ThenByDescending(x => x.PlacementPoints)
    .ThenByDescending(x => x.WWCD)
    .ThenByDescending(x => x.KillPoints)
    .ToList();
                var teamsdata = _vmix_GraphicsContext.Teams.Where(x => x.StageId == matches.StageId).ToList();
                
                var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
                int rankNum = 1;
                var rankingGuids = new List<string>
                {
                    vmixdata.OverAllRankingGUID,
                    vmixdata.OverAllRankingGUID1,
                    vmixdata.OverAllRankingGUID2,
                    vmixdata.OverAllRankingGUID3
                };
                var matchCountsPerTeam = _vmix_GraphicsContext.TeamPoints
    .Where(x => x.StageId == matches.StageId)
    .GroupBy(x => x.TeamId)
    .ToDictionary(g => g.Key.ToString(), g => g
        .Select(tp => tp.MatchId)
        .Distinct()
        .Count());
                foreach (var team in teamRankings)
                {
                    
                    var chicken = _vmix_GraphicsContext.TeamPoints
                        .Where(x => x.TeamId == team.TeamId)
                        .Select(x => x.WWCD)
                        .Sum();

                    var teamData = teamsdata.FirstOrDefault(x => x.TeamId == team.TeamId.ToString());
                    if (teamData == null)
                        continue;
                    int matchCount = matchCountsPerTeam.TryGetValue(teamData.TeamId, out var count) ? count : 0;

                    foreach (var guid in rankingGuids)
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(guid, $"TAGT{rankNum}", teamData.TeamName.ToUpper()));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(guid, $"WWCD{rankNum}", chicken == 0 ? "" : chicken.ToString()));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(guid, $"MATCHT{rankNum}", matchCount.ToString()));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(guid, $"ELIMST{rankNum}", team.KillPoints.ToString()));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(guid, $"PLACET{rankNum}", team.PlacementPoints.ToString()));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(guid, $"TOTALT{rankNum}", team.TotalPoints.ToString()));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(guid, $"LOGOT{rankNum}", $"{ConfigGlobal.LogosImages}\\0.png"));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(guid, $"LOGOT{rankNum}", $"{ConfigGlobal.LogosImages}\\{teamData.TeamId}.png"));
                    }

                    rankNum++;
                }

                SetTexts setTexts = new SetTexts();
                await setTexts.CallMultipleApiAsync(apiCalls);
                var OverAllRankingGUID = vmixdata.OverAllRankingGUID;
                //await vmi_layerSetOnOff.PushAnimationAsync(OverAllRankingGUID, 4, true, 1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
