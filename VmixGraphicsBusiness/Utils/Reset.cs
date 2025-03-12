using Hangfire;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmixData.Models;
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness.Utils
{
    public class Reset(vmi_layerSetOnOff vmi_LayerSetOnOff,ApiCallProcessor apiCallProcessor)
    {
        public async void ResetAll()
        {
            List<string> apiCalls = new();
            string LiverankingGuid;

            var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
            string TeamEliminatedGuid=vmixdata.TeamEliminatedGuid;
            LiverankingGuid = vmixdata.LiverankingGuid16;
            ResetLiverankings(LiverankingGuid, apiCalls);
            LiverankingGuid = vmixdata.LiverankingGuid18;
            ResetLiverankings(LiverankingGuid, apiCalls);
            LiverankingGuid = vmixdata.LiverankingGuid20;
            ResetLiverankings(LiverankingGuid, apiCalls);
            LiverankingGuid = vmixdata.LiverankingGuid4;
            ResetLiverankings(LiverankingGuid, apiCalls);

            ResetLiverankings(LiverankingGuid, apiCalls);

            apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"elims", "00"));
            apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"teamname", " "));
            apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"rank", "#" + "01"));
            apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(TeamEliminatedGuid, $"logo", ConfigGlobal.LogosImages + "\\0.png"));

            await apiCallProcessor.ProcessApiCalls(apiCalls);

        }

        private void ResetLiverankings(string LiverankingGuid, List<string> apiCalls)
        {
            for (int i = 1; i < 30; i++)
            {
                apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{i}P1", $"{ConfigGlobal.Images}/Dead/0.png"));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{i}P2", $"{ConfigGlobal.Images}/Dead/0.png"));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{i}P3", $"{ConfigGlobal.Images}/Dead/0.png"));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{i}P4", $"{ConfigGlobal.Images}/Dead/0.png"));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(LiverankingGuid, $"ELIMST{i}", "00"));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(LiverankingGuid, $"TOTALT{i}", "00"));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(LiverankingGuid, $"TAGT{i}", "00"));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"LOGOT{i}", $"{ConfigGlobal.LogosImages}\\0.png"));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{i}EliminatedBG", $"{ConfigGlobal.Images}\\EliminatedBG\\Team Dead.png"));
            }
        }
    }
}
