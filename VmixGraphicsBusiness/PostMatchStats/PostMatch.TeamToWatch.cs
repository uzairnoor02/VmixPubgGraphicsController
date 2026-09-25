using Microsoft.Extensions.Logging;
using VmixData.Models;
using VmixGraphicsBusiness.Utils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    public partial class PostMatch
    {
        /// <summary>The top 4 teams of this match with their supporting numbers -> overlay
        /// "TeamsToWatchUpdated" (TeamsToWatchRenderer's TeamToWatchEntry shape).</summary>
        public async Task TeamsToWatch(Match matches)
        {
            await using var _vmix_GraphicsContext = await _dbContextFactory.CreateDbContextAsync();
            try
            {
                var teamsPoints = _vmix_GraphicsContext.TeamPoints
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId)
                    .OrderByDescending(x => x.TotalPoints)
                    .Take(4)
                    .ToList();
                var teamsdata = _vmix_GraphicsContext.Teams.Where(x => x.StageId == matches.StageId).ToList();

                var entries = new List<object>();
                int teamnum = 1;
                foreach (var team in teamsPoints)
                {
                    var teamData = teamsdata.FirstOrDefault(x => x.TeamId == team.TeamId.ToString());
                    if (teamData == null)
                        continue;

                    var players = _vmix_GraphicsContext.PlayerStats
                        .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId && x.TeamId == team.TeamId)
                        .ToList();

                    int totalDistance = players.Sum(p => p.MarchDistance + p.DriveDistance);
                    int totalSmoke = players.Sum(p => p.UseSmokeGrenadeNum);
                    int totalFrag = players.Sum(p => p.UseFragGrenadeNum);
                    int totalBurn = players.Sum(p => p.useBurnGrenadeNum);
                    // Guarded: a team with no saved player rows used to divide by zero here and
                    // abort the whole graphic.
                    double avgSurvival = players.Count > 0 ? players.Average(p => p.SurvivalTime) : 0;

                    entries.Add(new
                    {
                        key = team.TeamId,
                        rank = teamnum,
                        teamName = teamData.TeamName,
                        logoUrl = MediaUrls.TeamLogo(team.TeamId),
                        reason = $"#{teamnum} in match {matches.MatchId} - {team.TotalPoints} pts",
                        stats = new object[]
                        {
                            new { label = "ELIMS", value = team.KillPoints },
                            new { label = "AVG SURVIVAL", value = FormatSurvival(avgSurvival) },
                            new { label = "TRAVELLED", value = $"{totalDistance}M" },
                            new { label = "THROWABLES", value = totalFrag + totalSmoke + totalBurn },
                        },
                        // Full breakdown the old vMix Title showed, for a custom layout.
                        fragsUsed = totalFrag,
                        smokesUsed = totalSmoke,
                        molotovsUsed = totalBurn,
                    });
                    teamnum++;
                }

                PublishGraphic(GraphicEvents.TeamsToWatchUpdated, entries);
            }
            catch (Exception e)
            {
                logger.LogError(e, "error in TeamsToWatch");
            }
        }
    }
}
