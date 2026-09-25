using Microsoft.Extensions.Logging;
using VmixData.Models;
using VmixGraphicsBusiness.Utils;

namespace VmixGraphicsBusiness.PreMatch
{
    public partial class PreMatch
    {
        /// <summary>Top 4 teams on the given map this stage -> overlay "MapPerformersUpdated",
        /// in the same TeamToWatchEntry shape so it renders with TeamsToWatchRenderer.</summary>
        public async Task MapTopPerformers(Match matches, string mapName)
        {
            try
            {
                var topMapPerformers = _vmix_GraphicsContext.TeamPoints
                    .Where(x => x.StageId == matches.StageId && x.Map == mapName)
                    .GroupBy(x => x.TeamId)
                    .Select(g => new
                    {
                        TeamId = g.Key,
                        MatchIds = g.Select(x => x.MatchId).Distinct().ToList(),
                        PlacementPoints = g.Sum(x => x.PlacementPoints),
                        KillPoints = g.Sum(x => x.KillPoints),
                        TotalPoints = g.Sum(x => x.PlacementPoints) + g.Sum(x => x.KillPoints)
                    })
                    .OrderByDescending(x => x.TotalPoints)
                    .Take(4)
                    .ToList();

                var teamsdata = _vmix_GraphicsContext.Teams.Where(x => x.StageId == matches.StageId).ToList();

                var entries = new List<object>();
                int rankNum = 1;
                foreach (var team in topMapPerformers)
                {
                    var teamData = teamsdata.FirstOrDefault(x => x.TeamId == team.TeamId.ToString());
                    if (teamData == null)
                        continue;

                    var players = _vmix_GraphicsContext.PlayerStats
                        .Where(x => team.MatchIds.Contains(x.MatchId) && x.StageId == matches.StageId && x.TeamId == team.TeamId)
                        .ToList();

                    double avgSurvival = players.Count > 0 ? players.Average(p => p.SurvivalTime) : 0;
                    int totalDamage = players.Sum(p => p.Damage ?? 0);
                    var survival = TimeSpan.FromSeconds(avgSurvival);

                    entries.Add(new
                    {
                        key = team.TeamId,
                        rank = rankNum,
                        teamName = teamData.TeamName,
                        logoUrl = MediaUrls.TeamLogo(team.TeamId),
                        reason = $"{team.TotalPoints} pts on {mapName}",
                        stats = new object[]
                        {
                            new { label = "PLACEMENT", value = team.PlacementPoints },
                            new { label = "ELIMS", value = team.KillPoints },
                            new { label = "DAMAGE", value = totalDamage },
                            new { label = "AVG SURVIVAL", value = $"{(int)survival.TotalMinutes:D2}:{survival.Seconds:D2}" },
                        },
                    });
                    rankNum++;
                }

                matchState.PublishGraphic(GraphicEvents.MapPerformersUpdated, new
                {
                    title = $"TOP {mapName.ToUpper()} PERFORMERS",
                    map = mapName,
                    teams = entries,
                });
                await Task.CompletedTask;
            }
            catch (Exception e)
            {
                logger.LogError(e, "error in MapTopPerformers");
            }
        }
    }
}
