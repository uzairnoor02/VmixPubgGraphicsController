using Microsoft.Extensions.Logging;
using VmixData.Models;
using VmixGraphicsBusiness.Utils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    partial class PostMatch
    {
        /// <summary>MVP of this match -> overlay "PlayerHighlightUpdated" (the MVP / Star
        /// Player card). Score unchanged: survival 40%, damage 40%, kills 20%.</summary>
        public async Task MatchMvp(Match matches)
        {
            await using var _vmix_GraphicsContext = await _dbContextFactory.CreateDbContextAsync();
            try
            {
                var mvpPlayer = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId)
                    .Select(p => new
                    {
                        Player = p,
                        Score = (p.SurvivalTime * 0.4) + (p.Damage * 0.4) + (p.KillNum * 0.2)
                    })
                    .OrderByDescending(p => p.Score)
                    .FirstOrDefault();

                if (mvpPlayer == null)
                {
                    logger.LogWarning("MatchMvp: no player stats saved for match {MatchId}.", matches.MatchId);
                    return;
                }

                var player = mvpPlayer.Player;
                var teamdata = _vmix_GraphicsContext.Teams.FirstOrDefault(x => x.TeamId == player.TeamId.ToString());
                var totalTeamKills = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId && x.TeamId == player.TeamId)
                    .Sum(x => x.KillNum ?? 0);
                var contribution = totalTeamKills > 0 ? Math.Round((double)(player.KillNum ?? 0) / totalTeamKills * 100, 1) : 0;

                PublishGraphic(GraphicEvents.PlayerHighlightUpdated, new
                {
                    label = "MVP OF THE MATCH",
                    matchNumber = matches.MatchId,
                    playerName = player.PlayerName,
                    teamName = (teamdata?.TeamName ?? player.TeamId.ToString()).ToUpper(),
                    photoUrl = MediaUrls.PlayerPhoto(player.PlayerUId),
                    teamLogoUrl = MediaUrls.TeamLogo(player.TeamId),
                    stats = new object[]
                    {
                        new { label = "ELIMS", value = player.KillNum ?? 0 },
                        new { label = "DAMAGE", value = player.Damage ?? 0 },
                        new { label = "SURVIVAL", value = FormatSurvival(player.SurvivalTime) },
                        new { label = "ASSISTS", value = player.Assists ?? 0 },
                        new { label = "KNOCKS", value = player.Knockouts ?? 0 },
                        new { label = "CONTRIBUTION", value = $"{contribution:0.#}%" },
                    },
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "error in MatchMvp");
            }
        }
    }
}
