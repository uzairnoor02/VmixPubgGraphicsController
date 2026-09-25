using Microsoft.Extensions.Logging;
using VmixData.Models;
using VmixGraphicsBusiness.Utils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    public partial class PostMatch
    {
        /// <summary>Stage standings across every match played -> overlay
        /// "OverallRankingsUpdated", rendered by the shared RankingsRenderer. Same ordering as
        /// before: total, then placement, then WWCDs, then elims.</summary>
        public async Task OverallRankings(Match matches)
        {
            await using var _vmix_GraphicsContext = await _dbContextFactory.CreateDbContextAsync();
            try
            {
                var totalMatches = _vmix_GraphicsContext.Matches.Count(x => x.StageId == matches.StageId);

                var teamRankings = _vmix_GraphicsContext.TeamPoints
                    .Where(x => x.StageId == matches.StageId)
                    .GroupBy(x => x.TeamId)
                    .Select(g => new
                    {
                        TeamId = g.Key,
                        TotalPoints = g.Sum(x => x.TotalPoints),
                        PlacementPoints = g.Sum(x => x.PlacementPoints),
                        WWCD = g.Sum(x => x.WWCD),
                        KillPoints = g.Sum(x => x.KillPoints),
                        MatchesPlayed = g.Count()
                    })
                    .OrderByDescending(x => x.TotalPoints)
                    .ThenByDescending(x => x.PlacementPoints)
                    .ThenByDescending(x => x.WWCD)
                    .ThenByDescending(x => x.KillPoints)
                    .ToList();
                var teamsdata = _vmix_GraphicsContext.Teams.Where(x => x.StageId == matches.StageId).ToList();

                var rows = new List<object>();
                int rankNum = 1;
                foreach (var team in teamRankings)
                {
                    var teamData = teamsdata.FirstOrDefault(x => x.TeamId == team.TeamId.ToString());
                    if (teamData == null)
                        continue;

                    // WWCD is now counted within this stage. It used to sum the team's WWCDs
                    // across every stage in the database.
                    rows.Add(new
                    {
                        rank = rankNum,
                        teamId = team.TeamId,
                        teamName = teamData.TeamName.ToUpper(),
                        logoUrl = MediaUrls.TeamLogo(team.TeamId),
                        wins = team.WWCD,
                        matchesPlayed = team.MatchesPlayed,
                        placementPts = team.PlacementPoints,
                        elimPts = team.KillPoints,
                        total = team.TotalPoints,
                    });
                    rankNum++;
                }

                PublishGraphic(GraphicEvents.OverallRankingsUpdated, new
                {
                    title = "OVERALL RANKINGS",
                    subtitle = $"AFTER MATCH {matches.MatchId} / {totalMatches}",
                    matchNumber = matches.MatchId,
                    totalMatches,
                    rows,
                });
            }
            catch (Exception e)
            {
                logger.LogError(e, "error in OverallRankings");
            }
        }
    }
}
