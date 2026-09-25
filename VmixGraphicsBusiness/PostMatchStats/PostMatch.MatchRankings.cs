using Microsoft.Extensions.Logging;
using VmixData.Models;
using VmixGraphicsBusiness.Utils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    public partial class PostMatch
    {
        /// <summary>This match's team table (placement / elim / total, WWCD flag) -> overlay
        /// "MatchRankingsUpdated", rendered by the shared RankingsRenderer.</summary>
        public async Task MatchRankings(Match matches)
        {
            await using var _vmix_GraphicsContext = await _dbContextFactory.CreateDbContextAsync();
            try
            {
                var totalMatches = _vmix_GraphicsContext.Matches.Count(x => x.StageId == matches.StageId);
                var teamRankings = _vmix_GraphicsContext.TeamPoints
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId)
                    .OrderByDescending(x => x.TotalPoints)
                    .ThenByDescending(x => x.PlacementPoints)
                    .ToList();
                var teamsdata = _vmix_GraphicsContext.Teams.Where(x => x.StageId == matches.StageId).ToList();
                var matchPlayers = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId)
                    .ToList();

                var rows = new List<object>();
                object[] winnerPlayers = Array.Empty<object>();
                int rankNum = 1;
                foreach (var team in teamRankings)
                {
                    var teamPlayers = matchPlayers.Where(x => x.TeamId == team.TeamId).ToList();
                    var wwcd = teamPlayers.Any(x => x.Rank == 1);
                    var teamData = teamsdata.FirstOrDefault(x => x.TeamId == team.TeamId.ToString());

                    rows.Add(new
                    {
                        rank = rankNum,
                        teamId = team.TeamId,
                        teamName = (teamData?.TeamName ?? team.TeamId.ToString()).ToUpper(),
                        logoUrl = MediaUrls.TeamLogo(team.TeamId),
                        wins = wwcd ? 1 : 0,
                        wwcd,
                        placementPts = team.PlacementPoints,
                        elimPts = team.KillPoints,
                        total = team.TotalPoints,
                    });

                    if (rankNum == 1)
                    {
                        winnerPlayers = teamPlayers
                            .Select(p => (object)new { playerName = p.PlayerName, photoUrl = MediaUrls.PlayerPhoto(p.PlayerUId) })
                            .ToArray();
                    }
                    rankNum++;
                }

                PublishGraphic(GraphicEvents.MatchRankingsUpdated, new
                {
                    title = "MATCH RANKINGS",
                    subtitle = $"MATCH {matches.MatchId} / {totalMatches}",
                    matchNumber = matches.MatchId,
                    totalMatches,
                    rows,
                    winnerPlayers,
                });
            }
            catch (Exception e)
            {
                logger.LogError(e, "error in MatchRankings");
            }
        }
    }
}
