using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmixData.Models;
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness.Utils
{
    public class Reset(IServiceProvider serviceProvider, ApiCallProcessor apiCallProcessor)
    {
        [AutomaticRetry(Attempts = 0, DelaysInSeconds = new[] { 1 })]

        [DisableConcurrentExecution(timeoutInSeconds: 3)]
        public async Task ResetAll(IBackgroundJobClient _backgroundJobClient)
        { _backgroundJobClient.Enqueue(HangfireQueues.HighPriority,() => Resetjob());
        }
        public async Task Resetjob()
        {
            List<string> apiCalls = new();
            string LiverankingGuid;

            var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
            string TeamEliminatedGuid = vmixdata.TeamEliminatedGuid;
            LiverankingGuid = vmixdata.LiverankingGuid16;
            ResetLiverankings(LiverankingGuid);
            LiverankingGuid = vmixdata.LiverankingGuid18;
            ResetLiverankings(LiverankingGuid);
            LiverankingGuid = vmixdata.LiverankingGuid20;
            ResetLiverankings(LiverankingGuid);
            LiverankingGuid = vmixdata.LiverankingGuid4;
            ResetLiverankings(LiverankingGuid);


            apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"elims", " "));
            apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"teamname", " "));
            apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(TeamEliminatedGuid, $"rank", "#" + " "));
            apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(TeamEliminatedGuid, $"logo", ConfigGlobal.LogosImages + "\\0.png"));

            await apiCallProcessor.ProcessApiCalls(apiCalls);
        }

        private async void ResetLiverankings(string LiverankingGuid)
        {
            List<string> apiCalls = new();
            for (int i = 1; i < 30; i++)
            {
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{i}P1", $"{ConfigGlobal.Images}/Dead/0.png"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{i}P2", $"{ConfigGlobal.Images}/Dead/0.png"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{i}P3", $"{ConfigGlobal.Images}/Dead/0.png"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"T{i}P4", $"{ConfigGlobal.Images}/Dead/0.png"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(LiverankingGuid, $"ELIMST{i}", " "));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(LiverankingGuid, $"TOTALT{i}", " "));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(LiverankingGuid, $"TAGT{i}", " "));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"LOGOT{i}", $"{ConfigGlobal.LogosImages}\\0.png"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(LiverankingGuid, $"RANKT{i}", $"{i}"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(LiverankingGuid, $"EliminatedBGT{i}", $"{ConfigGlobal.Images}\\EliminatedBG\\Team Dead.png"));

                await apiCallProcessor.ProcessApiCalls(apiCalls);
            }
        }
    }
}
