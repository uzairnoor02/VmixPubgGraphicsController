using StackExchange.Redis;
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
        public async Task MatchRankings(Match matches)
        {
            try
            {
                List<string> apiCalls = new List<string>();
                var teamRankings = _vmix_GraphicsContext.TeamPoints
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId)
                    .OrderByDescending(x => x.TotalPoints)
                    .ThenByDescending(x => x.PlacementPoints)
                    .ToList();
                var teamsdata = _vmix_GraphicsContext.Teams.Where(x => x.StageId == matches.StageId).ToList();

                var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
                int rankNum = 1;
                foreach (var team in teamRankings)
                {
                    var wwcddata = _vmix_GraphicsContext.PlayerStats.Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId && x.TeamId == team.TeamId).ToList();
                    var wwcd = wwcddata.Any(x => x.Rank == 1);
                    var ranks = wwcddata.Select(x => x.Rank);
                    var teamData = teamsdata.Where(x => x.TeamId == team.TeamId.ToString()).FirstOrDefault();
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchRankingsGUID, $"TAGT{rankNum}", teamData.TeamName));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchRankingsGUID, $"TEAMNAME{rankNum}", teamData.TeamName));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchRankingsGUID, $"ELIMST{rankNum}", team.KillPoints.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchRankingsGUID, $"PLACET{rankNum}", team.PlacementPoints.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MatchRankingsGUID, $"TOTALT{rankNum}", team.TotalPoints.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.MatchRankingsGUID, $"LOGOT{rankNum}", $"{ConfigGlobal.LogosImages}\\0.png"));
                    if (wwcd)
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.MatchRankingsGUID, $"WWCDT{rankNum}", $"{ConfigGlobal.LogosImages}\\WWCD.gif"));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.MatchRankingsGUID, $"WWCDT{rankNum}", $"{ConfigGlobal.LogosImages}\\WWCD.png"));
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.MatchRankingsGUID, $"WWCDT{rankNum}", $"{ConfigGlobal.LogosImages}\\WWCD.PNG"));
                    }
                    else
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.MatchRankingsGUID, $"WWCDT{rankNum}", $"{ConfigGlobal.LogosImages}\\0.png"));
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
