using Microsoft.Extensions.Logging;
using VmixData.Models;
using VmixGraphicsBusiness.Utils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    public partial class PostMatch
    {
        /// <summary>This match's winning team (WWCD) -> overlay "ChampionsUpdated": team name,
        /// logo, roster with photos, team totals, and a per-player breakdown.</summary>
        public async Task WWCDStatsAsync(Match matches)
        {
            await using var _vmix_GraphicsContext = await _dbContextFactory.CreateDbContextAsync();
            try
            {
                var winnerPlayers = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId && x.Rank == 1)
                    .ToList();
                if (winnerPlayers.Count == 0)
                {
                    logger.LogWarning("WWCD: no rank-1 players saved for match {MatchId}.", matches.MatchId);
                    return;
                }

                var winnerTeamId = winnerPlayers.First().TeamId;
                var winnerTeam = _vmix_GraphicsContext.Teams
                    .FirstOrDefault(x => x.TeamId == winnerTeamId.ToString() && x.StageId == matches.StageId)
                    ?? _vmix_GraphicsContext.Teams.FirstOrDefault(x => x.TeamId == winnerTeamId.ToString());

                // Real points from the saved TeamPoints row, instead of the old hardcoded
                // "10 + kills" (which assumed a 10-point win on every scoring system).
                var teamPoints = _vmix_GraphicsContext.TeamPoints
                    .FirstOrDefault(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId && x.TeamId == winnerTeamId);

                int totalTeamKills = winnerPlayers.Sum(x => x.KillNum ?? 0);
                int totalTeamDamage = winnerPlayers.Sum(x => x.Damage ?? 0);

                var playerStats = winnerPlayers.Select(p => new
                {
                    playerName = p.PlayerName,
                    photoUrl = MediaUrls.PlayerPhoto(p.PlayerUId),
                    kills = p.KillNum ?? 0,
                    damage = p.Damage ?? 0,
                    knocks = p.Knockouts ?? 0,
                    assists = p.Assists ?? 0,
                    damageTaken = p.InDamage ?? 0,
                    // Was Burn + Smoke + Smoke (frags never counted, smokes twice).
                    throwables = p.useBurnGrenadeNum + p.UseSmokeGrenadeNum + p.UseFragGrenadeNum,
                    survivalTime = FormatSurvival(p.SurvivalTime),
                    contribution = totalTeamKills > 0 ? Math.Round((double)(p.KillNum ?? 0) / totalTeamKills * 100, 1) : 0,
                }).ToList();

                PublishGraphic(GraphicEvents.ChampionsUpdated, new
                {
                    label = "WINNER WINNER CHICKEN DINNER",
                    matchNumber = matches.MatchId,
                    teamId = winnerTeamId,
                    teamName = (winnerTeam?.TeamName ?? winnerTeamId.ToString()).ToUpper(),
                    teamLogoUrl = MediaUrls.TeamLogo(winnerTeamId),
                    players = playerStats.Select(p => new { p.playerName, p.photoUrl }).ToList(),
                    stats = new object[]
                    {
                        new { label = "ELIMS", value = totalTeamKills },
                        new { label = "DAMAGE", value = totalTeamDamage },
                        new { label = "POINTS", value = teamPoints?.TotalPoints ?? totalTeamKills },
                    },
                    playerStats,
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "error in WWCDStatsAsync");
            }
        }
    }
}
