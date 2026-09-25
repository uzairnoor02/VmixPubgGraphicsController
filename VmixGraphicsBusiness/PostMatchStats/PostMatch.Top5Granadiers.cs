using Microsoft.Extensions.Logging;
using VmixData.Models;
using VmixGraphicsBusiness.Utils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    partial class PostMatch
    {
        /// <summary>Top 5 grenade killers across the stage -> overlay "TopPlayersUpdated"
        /// (TopPlayersRenderer's podium / card row).</summary>
        public async Task TopGrenadiers(Match matches)
        {
            await using var _vmix_GraphicsContext = await _dbContextFactory.CreateDbContextAsync();
            try
            {
                var topGrenadiers = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.StageId == matches.StageId)
                    .GroupBy(x => x.PlayerUId)
                    .Select(g => new
                    {
                        PlayerUId = g.Key,
                        PlayerName = g.Select(x => x.PlayerName).FirstOrDefault(),
                        TeamId = g.Select(x => x.TeamId).FirstOrDefault(),
                        TotalGrenadeKills = g.Sum(x => x.KillNumByGrenade ?? 0)
                    })
                    .Where(x => x.TotalGrenadeKills > 0)
                    .OrderByDescending(x => x.TotalGrenadeKills)
                    .Take(5)
                    .ToList();

                var players = topGrenadiers.Select((p, i) => new
                {
                    rank = i + 1,
                    playerName = p.PlayerName,
                    value = p.TotalGrenadeKills,
                    statLabel = "GRENADE ELIMS",
                    photoUrl = MediaUrls.PlayerPhoto(p.PlayerUId),
                    teamLogoUrl = MediaUrls.TeamLogo(p.TeamId),
                }).ToList();

                PublishGraphic(GraphicEvents.TopPlayersUpdated, players);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "error in TopGrenadiers");
            }
        }
    }
}
