
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
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPMatchGUID, $"CONTP{rank}",
                        (playerContribution % 1 == 0 ? playerContribution.ToString("F0") : playerContribution.ToString("F1")) + "%"));
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
                // First, get aggregated stats for each player across all matches in the stage
                var top5StageMVPs = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.StageId == matches.StageId)
                    .GroupBy(x => x.PlayerUId) // Group by player
                    .Select(g => new
                    {
                        PlayerUId = g.Key,
                        PlayerName = g.First().PlayerName,
                        TeamId = g.First().TeamId,
                        TotalKills = g.Sum(x => x.KillNum ?? 0),
                        TotalDamage = g.Sum(x => x.Damage ?? 0),
                        TotalSurvivalTime = g.Sum(x => x.SurvivalTime),
                        TotalAssists = g.Sum(x => x.Assists ?? 0),
                        TotalKnockouts = g.Sum(x => x.Knockouts ?? 0),
                        MatchesPlayed = g.Count()
                    })
                    .Select(p => new
                    {
                        Player = p,
                        Score = (p.TotalSurvivalTime * 0.4) + (p.TotalDamage * 0.4) + (p.TotalKills * 0.2)
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
                foreach (var mvpData in top5StageMVPs)
                {
                    var mvpPlayerStats = mvpData.Player;
                    var teamdata = _vmix_GraphicsContext.Teams.Where(x => x.TeamId == mvpPlayerStats.TeamId.ToString()).FirstOrDefault();

                    // Format survival time
                    var survivalTime = TimeSpan.FromSeconds(mvpPlayerStats.TotalSurvivalTime);
                    var survivalTimeString = $"{survivalTime.Minutes:D2}:{survivalTime.Seconds:D2}";

                    // Calculate player's contribution to team kills across all matches in stage
                    var totalTeamKills = _vmix_GraphicsContext.PlayerStats
                        .Where(x => x.StageId == matches.StageId && x.TeamId == mvpPlayerStats.TeamId)
                        .Sum(x => x.KillNum ?? 0);

                    var playerContribution = totalTeamKills > 0
                        ? Math.Round((double)mvpPlayerStats.TotalKills / totalTeamKills * 100, 1)
                        : 0;

                    // Set all the display values using aggregated stats
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"RANK{rank}", $"#{rank}"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"TEAMTAGP{rank}", teamdata?.TeamName ?? "Unknown"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"NAMEP{rank}", mvpPlayerStats.PlayerName));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"ELIMSP{rank}", mvpPlayerStats.TotalKills.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"SURVP{rank}", survivalTimeString));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"DAMAGEP{rank}", mvpPlayerStats.TotalDamage.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"ASSISTSP{rank}", mvpPlayerStats.TotalAssists.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"KNOCKP{rank}", mvpPlayerStats.TotalKnockouts.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5MVPStageGUID, $"CONTP{rank}", playerContribution.ToString("F1") + "%"));

                    // Set images
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5MVPStageGUID, $"TEAMLOGOP{rank}", $"{ConfigGlobal.LogosImages}\\0.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5MVPStageGUID, $"TEAMLOGOP{rank}", $"{ConfigGlobal.LogosImages}\\{teamdata?.TeamId}.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5MVPStageGUID, $"LOGOP{rank}", $"{ConfigGlobal.LogosImages}\\{teamdata?.TeamId ?? "0"}.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5MVPStageGUID, $"IMAGEP{rank}", $"{ConfigGlobal.PlayerImages}\\0.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5MVPStageGUID, $"IMAGEP{rank}", $"{ConfigGlobal.PlayerImages}\\{mvpPlayerStats.PlayerUId}.png"));

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
        public async Task StageMVP(Match matches)
        {
            try
            {
                var playerStageStats = _vmix_GraphicsContext.PlayerStats
            .Where(x => x.StageId == matches.StageId)
            .GroupBy(x => x.PlayerUId) // Group by player
            .Select(g => new
            {
                PlayerUId = g.Key,
                PlayerName = g.First().PlayerName,
                TeamId = g.First().TeamId,
                TotalKills = g.Sum(x => x.KillNum ?? 0),
                TotalDamage = g.Sum(x => x.Damage ?? 0),
                TotalSurvivalTime = g.Sum(x => x.SurvivalTime),
                TotalAssists = g.Sum(x => x.Assists ?? 0),
                TotalKnockouts = g.Sum(x => x.Knockouts ?? 0),
                MatchesPlayed = g.Count()
            })
            .Select(p => new
            {
                Player = p,
                Score = (p.TotalSurvivalTime * 0.4) + (p.TotalDamage * 0.4) + (p.TotalKills * 0.2)
            })
            .OrderByDescending(p => p.Score)
            .Take(1)
            .ToList();

                if (!playerStageStats.Any())
                {
                    throw new InvalidOperationException("No players found for Top Stage MVP calculation.");
                }

                var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
                List<string> apiCalls = new List<string>();

                var mvpPlayerStats = playerStageStats.First().Player;
                var teamdata = _vmix_GraphicsContext.Teams.Where(x => x.TeamId == mvpPlayerStats.TeamId.ToString()).FirstOrDefault();

                // Format survival time
                var survivalTime = TimeSpan.FromSeconds(mvpPlayerStats.TotalSurvivalTime);
                var survivalTimeString = $"{survivalTime.Minutes:D2}:{survivalTime.Seconds:D2}";

                // Calculate player's contribution to team kills across all matches in stage
                var totalTeamKills = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.StageId == matches.StageId && x.TeamId == mvpPlayerStats.TeamId)
                    .Sum(x => x.KillNum ?? 0);

                var playerContribution = totalTeamKills > 0
                    ? Math.Round((double)mvpPlayerStats.TotalKills / totalTeamKills * 100, 1)
                    : 0;

                // Set all the display values using aggregated stats
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.StageMVP, $"RANK", $"#1"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.StageMVP, $"TEAMTAGP1", teamdata?.TeamName ?? "Unknown"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.StageMVP, $"NAMEP", mvpPlayerStats.PlayerName));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.StageMVP, $"ELIMSP", mvpPlayerStats.TotalKills.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.StageMVP, $"SURVP", survivalTimeString));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.StageMVP, $"DAMAGEP", mvpPlayerStats.TotalDamage.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.StageMVP, $"ASSISTSP", mvpPlayerStats.TotalAssists.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.StageMVP, $"KNOCKP", mvpPlayerStats.TotalKnockouts.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.StageMVP, $"CONTP1", playerContribution.ToString("F1") + "%"));

                // Optional: Add matches played and total score
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.StageMVP, $"MATCHESP", mvpPlayerStats.MatchesPlayed.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.StageMVP, $"SCOREP", Math.Round(playerStageStats.First().Score, 1).ToString()));

                // Set images
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.StageMVP, $"TEAMLOGOP", $"{ConfigGlobal.LogosImages}\\0.png"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.StageMVP, $"TEAMLOGOP", $"{ConfigGlobal.LogosImages}\\{teamdata?.TeamId}.png"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.StageMVP, $"LOGOP", $"{ConfigGlobal.LogosImages}\\{teamdata?.TeamId ?? "0"}.png"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.StageMVP, $"IMAGEP", $"{ConfigGlobal.PlayerImages}\\0.png"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.StageMVP, $"IMAGEP", $"{ConfigGlobal.PlayerImages}\\{mvpPlayerStats.PlayerUId}.png"));

                SetTexts setTexts = new SetTexts();
                await setTexts.CallMultipleApiAsync(apiCalls);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in StageMVP: {ex}");
            }
        }


    }
}
