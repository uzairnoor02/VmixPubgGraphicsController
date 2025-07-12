using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VmixData.Models;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness.Utils;
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    partial class PostMatch
    {
        public async Task MatchMvp(Match matches)
        {
            try
            {
                var mvpPlayer = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId)
                    .Select(p => new
                    {
                        Player = p,
                        Score = (p.SurvivalTime * 0.4) + (p.Damage * 0.4) + (p.KillNum * 0.2)
                    })
                    .OrderByDescending(p => p.Score)
                    .FirstOrDefault();

                //var mvpPlayer = playerStats
                //    .Select(p => new
                //    {
                //        Player = p,
                //        Score = (p.SurvivalTime * 0.4) + (p.Damage * 0.4) + (p.KillNum * 0.2)
                //    })
                //    .OrderByDescending(p => p.Score)
                //    .FirstOrDefault();

                if (mvpPlayer == null)
                {
                    throw new InvalidOperationException("MVP could not be determined.");
                }
                var teamdata = _vmix_GraphicsContext.Teams.Where(x => x.TeamId == mvpPlayer.Player.TeamId.ToString()).FirstOrDefault();
                var player = mvpPlayer.Player;
                var survivalTime = TimeSpan.FromSeconds(player.SurvivalTime);
                var survivalTimeString = $"{survivalTime.Minutes:D2}:{survivalTime.Seconds:D2}";

                var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
                var totalTeamKills = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId && x.TeamId == player.TeamId)
                    .Sum(x => x.KillNum ?? 0);

                var playerContribution = totalTeamKills > 0
                    ? (double)player.KillNum / totalTeamKills * 100
                    : 0;
                //var playerContribution = teamdata != null && teamdata.KillPoints > 0
                //    ? (double)player.KillNum / 
                //    : 0;

                List<string> apiCalls = new List<string>();
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MVPGUID, $"TEAMTAGP{1}", teamdata.TeamName));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MVPGUID, $"MATCHN", matches.MatchId.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MVPGUID, $"NAMEP{1}", player.PlayerName));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MVPGUID, $"ELIMSP{1}", player.KillNum.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MVPGUID, $"SURVP{1}", survivalTimeString));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MVPGUID, $"DAMAGEP{1}", player.Damage.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MVPGUID, $"ASSISTSP{1}", player.Assists.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MVPGUID, $"KNOCKP{1}", player.Knockouts.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.MVPGUID, $"CONTP{1}", playerContribution.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.MVPGUID, $"LOGOP{1}", $"{ConfigGlobal.LogosImages}\\{teamdata.TeamId}.png"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.MVPGUID, $"IMAGEP{1}", $"{ConfigGlobal.PlayerImages}\\0.png"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.MVPGUID, $"IMAGEP{1}", $"{ConfigGlobal.PlayerImages}\\{player.PlayerUId}.png"));

                SetTexts setTexts = new SetTexts();
                await setTexts.CallMultipleApiAsync(apiCalls);
            }catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

    }
}
