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
        public async Task TeamsToWatch(Match matches)
        {
            try
            {
                List<string> apiCalls = new List<string>();
                var teamsPoints = _vmix_GraphicsContext.TeamPoints
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId)
                    .OrderByDescending(x => x.TotalPoints)
                    .Take(4)
                    .ToList();
                var teamsdata = _vmix_GraphicsContext.Teams.Where(x => x.StageId == matches.StageId).ToList();



                var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
                int teamnum = 1;
                foreach (var team in teamsPoints)
                {
                    var players  = _vmix_GraphicsContext.PlayerStats
                        .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId && x.TeamId==team.TeamId)
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
                    int playerCount = players.Count;

                    foreach (var player in players)
                    {
                        totalDistance += player.MarchDistance+player.DriveDistance;
                        totalSmokeGrenades += player.UseSmokeGrenadeNum;
                        totalFragGrenades += player.UseFragGrenadeNum;
                        totalBurnGrenades += player.useBurnGrenadeNum;
                        totalSurvivalTime += player.SurvivalTime;
                    }
                    totalSurvivalTime = totalSurvivalTime / playerCount;
                    var survivalTime = TimeSpan.FromSeconds(totalSurvivalTime);
                    var survivalTimeString = $"{survivalTime.Minutes:D2}:{survivalTime.Seconds:D2}";
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TeamsToWatchGUID, $"TAGT{teamnum}", teamData.TeamName));

                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TeamsToWatchGUID, $"MOLIUSEDT{teamnum}", totalBurnGrenades.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TeamsToWatchGUID, $"SURVIVALT{teamnum}", survivalTimeString));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TeamsToWatchGUID, $"MATCHN", matches.MatchId.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TeamsToWatchGUID, $"BUMUSEDT{teamnum}", totalFragGrenades.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TeamsToWatchGUID, $"SMOKEUSEDT{teamnum}", totalSmokeGrenades.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TeamsToWatchGUID, $"TRAVELDIST{teamnum}", totalDistance+"M"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.TeamsToWatchGUID, $"MATCHNumber", matches.MatchId.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.TeamsToWatchGUID, $"LOGOTEAM{teamnum}", $"{ConfigGlobal.LogosImages}\\0.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.TeamsToWatchGUID, $"LOGOTEAM{teamnum}", $"{ConfigGlobal.LogosImages}\\{teamData.TeamId}.png"));

                    teamnum++;
                }
                SetTexts setTexts = new SetTexts();
                await setTexts.CallMultipleApiAsync(apiCalls);
                var TeamsToWatchGUID = vmixdata.TeamsToWatchGUID;
                //await vmi_layerSetOnOff.PushAnimationAsync(TeamsToWatchGUID, 4, true, 1);
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
