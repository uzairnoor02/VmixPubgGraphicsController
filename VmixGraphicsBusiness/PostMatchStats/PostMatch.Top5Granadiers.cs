
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
        public async Task TopGrenadiers(Match matches)
        {
            try
            {
                var topGrenadiers = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.StageId == matches.StageId && x.DayId==matches.MatchDayId)
                    .GroupBy(x => x.PlayerUId)
                    .Select(g => new
                    {
                        PlayerUId = g.Key,
                        PlayerName = g.Select(x => x.PlayerName).FirstOrDefault(),
                        TeamId = g.Select(x => x.TeamId).FirstOrDefault(),
                        TotalGrenadeKills = g.Sum(x => x.KillNumByGrenade ?? 0)
                    })
                    .Where(x => x.TotalGrenadeKills > 0)
                    .OrderByDescending(x => x.TotalGrenadeKills)
                    .Take(5)
                    .ToList();

                var teamsdata = _vmix_GraphicsContext.Teams.Where(x => x.StageId == matches.StageId).ToList();
                var vmixdata = await VmixDataUtils.SetVMIXDataoperations();

                List<string> apiCalls = new List<string>();

                // Set title
             //   apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5Grenadiers, "TITLE", "TOP 5 GRENADIERS"));

                int playerNum = 1;
                foreach (var player in topGrenadiers)
                {
                    var teamData = teamsdata.Where(x => x.TeamId == player.TeamId.ToString()).FirstOrDefault();

                    // Set player name
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5Grenadiers, $"NAMEP{playerNum}", player.PlayerName));

                    // Set team logo
                    if (teamData != null)
                    {
                        apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5Grenadiers, $"TEAMLOGOP{playerNum}", $"{ConfigGlobal.LogosImages}\\{teamData.TeamId}.png"));
                    }

                    // Set player image
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5Grenadiers, $"IMAGEP{playerNum}", $"{ConfigGlobal.PlayerImages}\\0.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5Grenadiers, $"IMAGEP{playerNum}", $"{ConfigGlobal.PlayerImages}\\{player.PlayerUId}.png"));

                    // Set grenade eliminations
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5Grenadiers, $"BUMUSEDP{playerNum}", player.TotalGrenadeKills.ToString()));

                    playerNum++;
                }

                // Clear remaining slots if less than 5 players
                for (int i = playerNum; i <= 5; i++)
                {
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5Grenadiers, $"NAMEP{i}", ""));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5Grenadiers, $"TEAMLOGOP{i}", $"{ConfigGlobal.LogosImages}\\0.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.Top5Grenadiers, $"IMAGEP{i}", $"{ConfigGlobal.PlayerImages}\\0.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.Top5Grenadiers, $"BUMUSEDP{i}", "0"));
                }

                SetTexts setTexts = new SetTexts();
                await setTexts.CallMultipleApiAsync(apiCalls);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}