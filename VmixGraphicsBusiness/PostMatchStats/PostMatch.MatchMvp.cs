using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness.Utils;
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    partial class PostMatch
    {
        public async Task CalculateMVP()
        {
            var playersData = JsonSerializer.Deserialize<LivePlayersList>(_redisConnection.GetDatabase().StringGet(HelperRedis.PlayerInfolist));
            if (playersData == null || playersData.PlayerInfoList == null)
            {
                throw new InvalidOperationException("Player data could not be retrieved.");
            }

            var playerStats = playersData.PlayerInfoList;

            if (!playerStats.Any())
            {
                throw new InvalidOperationException("No player stats found for the given match.");
            }

            var mvpPlayer = playerStats
                .Select(p => new
                {
                    Player = p,
                    Score = (p.SurvivalTime * 0.4) + (p.Damage * 0.4) + (p.KillNum * 0.2)
                })
                .OrderByDescending(p => p.Score)
                .FirstOrDefault();

            if (mvpPlayer == null)
            {
                throw new InvalidOperationException("MVP could not be determined.");
            }

            var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
            List<string> apiCalls = new List<string>
    {
        vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.MVPGUID, "MVPName", mvpPlayer.Player.PlayerName),
        vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.MVPGUID, "MVPScore", mvpPlayer.Score.ToString("F2")),
        vmi_LayerSetOnOff.GetSetImageApiCall(vmixdata.MVPGUID, "MVPImage", $"{ConfigGlobal.PlayerImages}//{mvpPlayer.Player.UId}.png")
    };

            SetTexts setTexts = new SetTexts();
            await setTexts.CallApiAsync(apiCalls);
        }

    }
}
