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
                foreach (var team in teamRankings)
                {
                    var chicken = _vmix_GraphicsContext.TeamPoints.Where(x => x.TeamId == team.TeamId).Select(x=>x.WWCD).Sum();

                    var teamData = teamsdata.Where(x => x.TeamId == team.TeamId.ToString()).FirstOrDefault();
                    if (teamData == null)
                    {
                        continue;
                    }

                    apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.OverAllRankingGUID, $"TAGT{rankNum}", teamData.TeamName));
                    apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.OverAllRankingGUID, $"WWCD{rankNum}", chicken.ToString()));
                    apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.OverAllRankingGUID, $"ELIMST{rankNum}", team.KillPoints.ToString()));
                    apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.OverAllRankingGUID, $"PLACET{rankNum}", team.PlacementPoints.ToString()));
                    apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.OverAllRankingGUID, $"TOTALT{rankNum}", team.TotalPoints.ToString()));
                    apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(vmixdata.OverAllRankingGUID, $"LOGOT{rankNum}", $"{ConfigGlobal.LogosImages}\\0.png"));
                    apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(vmixdata.OverAllRankingGUID, $"LOGOT{rankNum}", $"{ConfigGlobal.LogosImages}\\{teamData.TeamId}.png"));

                    rankNum++;
                }
                SetTexts setTexts = new SetTexts();
                await setTexts.CallApiAsync(apiCalls);
                var OverAllRankingGUID = vmixdata.OverAllRankingGUID;
                //await vmi_LayerSetOnOff.PushAnimationAsync(OverAllRankingGUID, 4, true, 1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
