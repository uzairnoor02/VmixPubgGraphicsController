using Microsoft.Extensions.Logging;
using VmixData.Models;
using VmixGraphicsBusiness.Utils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    public partial class PostMatch
    {
        /// <summary>Whole-lobby totals for this match -> overlay "MatchSummaryUpdated".
        /// NOTE: no overlay widget renders this yet - the data is published (and kept in the
        /// snapshot) so a Match Summary graphic can be added on the frontend alone.</summary>
        public async Task MatchSummary(Match matches)
        {
            await using var _vmix_GraphicsContext = await _dbContextFactory.CreateDbContextAsync();
            try
            {
                var totalMatches = _vmix_GraphicsContext.Matches.Count(x => x.StageId == matches.StageId);
                var all = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId)
                    .ToList();
                if (all.Count == 0) return;

                PublishGraphic(GraphicEvents.MatchSummaryUpdated, new
                {
                    kind = "match",
                    matchNumber = matches.MatchId,
                    totalMatches,
                    eliminations = all.Sum(x => x.KillNum ?? 0),
                    knocks = all.Sum(x => x.Knockouts ?? 0),
                    longestElim = $"{all.Max(x => x.MaxKillDistance ?? 0)}M",
                    healing = all.Sum(x => x.Heal ?? 0),
                    throwablesUsed = all.Sum(x => x.UseFragGrenadeNum + x.useBurnGrenadeNum + x.UseSmokeGrenadeNum),
                    fragsUsed = all.Sum(x => x.UseFragGrenadeNum),
                    molotovsUsed = all.Sum(x => x.useBurnGrenadeNum),
                    smokesUsed = all.Sum(x => x.UseSmokeGrenadeNum),
                    airdropsLooted = all.Sum(x => x.GotAirdropNum ?? 0),
                    headshots = all.Sum(x => x.HeadshotNum ?? 0),
                    vehicleElims = all.Sum(x => x.KillNumInVehicle ?? 0),
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in MatchSummary");
            }
        }

        /// <summary>Each match of the day with its WWCD team's totals -> overlay
        /// "MatchSummaryUpdated" (kind = "day"). Same no-widget-yet note as MatchSummary.</summary>
        public async Task DaySummary(Match matches)
        {
            await using var _vmix_GraphicsContext = await _dbContextFactory.CreateDbContextAsync();
            try
            {
                var dayMatches = _vmix_GraphicsContext.Matches
                    .Where(x => x.StageId == matches.StageId && x.MatchDayId == matches.MatchDayId)
                    .OrderBy(x => x.MatchId)
                    .ToList();
                if (!dayMatches.Any()) return;

                var winners = new List<object>();
                foreach (var match in dayMatches)
                {
                    var winningTeamPlayers = _vmix_GraphicsContext.PlayerStats
                        .Where(x => x.MatchId == match.MatchId && x.StageId == match.StageId && x.DayId == match.MatchDayId && x.Rank == 1)
                        .ToList();
                    if (!winningTeamPlayers.Any()) continue;

                    var winningTeamId = winningTeamPlayers.First().TeamId;
                    var teamData = _vmix_GraphicsContext.Teams.FirstOrDefault(x => x.TeamId == winningTeamId.ToString());

                    winners.Add(new
                    {
                        matchNumber = match.MatchId,
                        teamName = teamData?.TeamName?.ToUpper() ?? "UNKNOWN",
                        logoUrl = MediaUrls.TeamLogo(winningTeamId),
                        eliminations = winningTeamPlayers.Sum(x => x.KillNum ?? 0),
                        damage = winningTeamPlayers.Sum(x => x.Damage ?? 0),
                        knocks = winningTeamPlayers.Sum(x => x.Knockouts ?? 0),
                        healing = winningTeamPlayers.Sum(x => x.Heal ?? 0),
                    });
                }

                PublishGraphic(GraphicEvents.MatchSummaryUpdated, new
                {
                    kind = "day",
                    dayNumber = matches.MatchDayId,
                    winners,
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in DaySummary");
            }
        }
    }
}
