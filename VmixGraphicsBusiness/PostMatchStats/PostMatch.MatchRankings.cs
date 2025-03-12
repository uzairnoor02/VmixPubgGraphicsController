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
                    .ToList();
                var teamsdata = _vmix_GraphicsContext.Teams.ToList();

                var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
                int rankNum = 1;
                foreach (var team in teamRankings)
                {
                    var teamData = teamsdata.Where(x => x.TeamId == team.TeamId.ToString()).FirstOrDefault();
                    if (teamData == null)
                    {
                        continue;
                    }

                    apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.MatchRankingsGUID, $"TAGT{rankNum}", teamData.TeamName));
                    apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.MatchRankingsGUID, $"TEAMNAME{rankNum}", teamData.TeamName));
                    apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.MatchRankingsGUID, $"ELIMST{rankNum}", teamData.TeamName));
                    apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.MatchRankingsGUID, $"PLACET{rankNum}", teamData.TeamName));
                    apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.MatchRankingsGUID, $"TOTALT{rankNum}", teamData.TeamName));
                    apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(vmixdata.MatchRankingsGUID, $"LOGOTEAM{rankNum}", $"{ConfigGlobal.LogosImages}\\{teamData.TeamId}.png"));

                    rankNum++;
                }
                SetTexts setTexts = new SetTexts();
                await setTexts.CallApiAsync(apiCalls);
                var MatchRankingsGUID = vmixdata.MatchRankingsGUID;
                //await vmi_LayerSetOnOff.PushAnimationAsync(MatchRankingsGUID, 4, true, 1);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }
}
