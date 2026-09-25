using Hangfire;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VmixGraphicsBusiness.Utils
{
    /// <summary>
    /// Clears match-scoped state and blanks the live graphics on the overlay.
    ///
    /// This used to blank every field of every vMix Title (live rankings 4/16/18/20, the
    /// eliminated banner) through the vMix HTTP API - which meant "reset" also failed whenever
    /// vMix wasn't running. The overlay now owns what's on screen, so a reset is just: forget
    /// the previous match, and tell the overlay the live board is empty.
    /// </summary>
    public class Reset(MatchStateStore matchState)
    {
        // The IBackgroundJobClient parameter is unused; it stays only so every existing caller
        // (Form1, MatchControlApi, LiveDashboardHost, queued Hangfire jobs) keeps compiling and
        // deserialising unchanged.
        [Queue(HangfireQueues.HighPriority)]
        [AutomaticRetry(Attempts = 0, DelaysInSeconds = new[] { 1 })]
        [DisableConcurrentExecution(timeoutInSeconds: 3)]
        public Task ResetAll(IBackgroundJobClient _backgroundJobClient) => Resetjob();

        [Queue(HangfireQueues.HighPriority)]
        public Task Resetjob()
        {
            // Clear Top4 position locks / elimination flags / cached match state on every reset,
            // so a stale mapping from the previous match can never leak into the next one. Also
            // clears the live-only graphics (circle bar, Top 4) from the overlay.
            matchState.ResetMatchState();
            matchState.PublishLiveTeams(new List<TeamLiveStats>());
            return Task.CompletedTask;
        }
    }
}
