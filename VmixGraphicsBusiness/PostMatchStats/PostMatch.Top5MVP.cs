
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmixData.Models;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness.Utils;
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    partial class PostMatch
    {
        public async Task Top5MatchMVP(Match matches)
        {
            try
            {
                var totalMatches = _vmix_GraphicsContext.Matches.Where(x => x.StageId == matches.StageId);
                var top5MVPs = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId)
                    .Select(p => new
                    {
                        Player = p,
                        Score = (p.SurvivalTime * 0.4) + (p.Damage * 0.4) + (p.KillNum * 0.2)
                    })
                    .OrderByDescending(p => p.Score)
                    .Take(5)
                    .ToList();

                if (!top5MVPs.Any())
                {
                    throw new InvalidOperationException("No players found for Top 5 MVP calculation.");
                }

                var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
                List<string> apiCalls = new List<string>();

                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPMatchGUID, $"MATCHN", matches.MatchId.ToString()));

                int rank = 1;
                foreach (var mvpPlayer in top5MVPs)
                {
                    var teamdata = _vmix_GraphicsContext.Teams.Where(x => x.TeamId == mvpPlayer.Player.TeamId.ToString()).FirstOrDefault();
                    var player = mvpPlayer.Player;
                    var survivalTime = TimeSpan.FromSeconds(player.SurvivalTime);
                    var survivalTimeString = $"{survivalTime.Minutes:D2}:{survivalTime.Seconds:D2}";

                    var totalTeamKills = _vmix_GraphicsContext.PlayerStats
                        .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId && x.TeamId == player.TeamId)
                        .Sum(x => x.KillNum ?? 0);

                    var playerContribution = totalTeamKills > 0
                        ? Math.Round((double)player.KillNum / totalTeamKills * 100, 1)
                        : 0;

                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPMatchGUID, $"PMNUM", totalMatches.Count().ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPMatchGUID, $"NAMEP{rank}", player.PlayerName));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPMatchGUID, $"ELIMSP{rank}", player.KillNum.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPMatchGUID, $"SURVP{rank}", survivalTimeString));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPMatchGUID, $"DAMAGEP{rank}", player.Damage.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPMatchGUID, $"ASSISTSP{rank}", player.Assists.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPMatchGUID, $"KNOCKP{rank}", player.Knockouts.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPMatchGUID, $"CONTP{rank}", playerContribution.ToString("F1") + "%"));
                    
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5MVPMatchGUID, $"TEAMLOGOP{rank}", $"{ConfigGlobal.LogosImages}\\{teamdata?.TeamId ?? "0"}.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5MVPMatchGUID, $"IMAGEP{rank}", $"{ConfigGlobal.PlayerImages}\\0.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5MVPMatchGUID, $"IMAGEP{rank}", $"{ConfigGlobal.PlayerImages}\\{player.PlayerUId}.png"));

                    rank++;
                }

                SetTexts setTexts = new SetTexts();
                await setTexts.CallMultipleApiAsync(apiCalls);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in Top5MatchMVP: {ex}");
            }
        }

        public async Task Top5StageMVP(Match matches)
        {
            try
            {
                var top5StageMVPs = _vmix_GraphicsContext.PlayerStats
                    .Where(x=> x.StageId == matches.StageId )
                    .Select(p => new
                    {
                        Player = p,
                        Score = (p.SurvivalTime * 0.4) + (p.Damage * 0.4) + (p.KillNum * 0.2)
                    })
                    .OrderByDescending(p => p.Score)
                    .Take(5)
                    .ToList();

                if (!top5StageMVPs.Any())
                {
                    throw new InvalidOperationException("No players found for Top 5 Stage MVP calculation.");
                }

                var vmixdata = await VmixDataUtils.SetVMIXDataoperations();

                List<string> apiCalls = new List<string>();

                int rank = 1;
                foreach (var mvpPlayer in top5StageMVPs.Select(x=>x.Player))
                {
                    var teamdata = _vmix_GraphicsContext.Teams.Where(x => x.TeamId == mvpPlayer.TeamId.ToString()).FirstOrDefault();
                    var survivalTime = TimeSpan.FromSeconds(mvpPlayer.SurvivalTime);
                    var survivalTimeString = $"{survivalTime.Minutes:D2}:{survivalTime.Seconds:D2}";

                    // Calculate player's contribution to team kills across all matches in stage
                    var totalTeamKills = _vmix_GraphicsContext.PlayerStats
                        .Where(x => x.StageId == matches.StageId && x.TeamId == mvpPlayer.TeamId)
                        .Sum(x => x.KillNum ?? 0);

                    var playerContribution = totalTeamKills > 0
                        ? Math.Round((double)mvpPlayer.KillNum/ totalTeamKills * 100, 1)
                        : 0;

                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"RANK{rank}", $"#{rank}"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"TEAMTAGP{rank}", teamdata?.TeamName ?? "Unknown"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"NAMEP{rank}", mvpPlayer.PlayerName));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"ELIMSP{rank}", mvpPlayer.KillNum.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"SURVP{rank}", survivalTimeString));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"DAMAGEP{rank}", mvpPlayer.Damage.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"ASSISTSP{rank}", mvpPlayer.Assists.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"KNOCKP{rank}", mvpPlayer.Knockouts.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"CONTP{rank}", playerContribution.ToString("F1") + "%"));
                    //apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"SCOREP{rank}", Math.Round(mvpPlayer.Score, 1).ToString()));
                    //apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"MATCHESP{rank}", mvpPlayer.MatchesPlayed.ToString()));

                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5MVPStageGUID, $"TEAMLOGOP{rank}", $"{ConfigGlobal.LogosImages}\\0.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5MVPStageGUID, $"TEAMLOGOP{rank}", $"{ConfigGlobal.LogosImages}\\{teamdata?.TeamId}.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5MVPStageGUID, $"LOGOP{rank}", $"{ConfigGlobal.LogosImages}\\{teamdata?.TeamId ?? "0"}.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5MVPStageGUID, $"IMAGEP{rank}", $"{ConfigGlobal.PlayerImages}\\0.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5MVPStageGUID, $"IMAGEP{rank}", $"{ConfigGlobal.PlayerImages}\\{mvpPlayer.PlayerUId}.png"));

                    rank++;
                }

                SetTexts setTexts = new SetTexts();
                await setTexts.CallMultipleApiAsync(apiCalls);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in Top5StageMVP: {ex}");
            }
        }
    }
}
