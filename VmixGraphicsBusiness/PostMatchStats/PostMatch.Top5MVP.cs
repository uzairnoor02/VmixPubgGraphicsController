using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VmixData.Models;
using VmixGraphicsBusiness.Utils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    partial class PostMatch
    {
        /// <summary>Top 5 players of this match by MVP score -> overlay "MvpRankingsUpdated"
        /// (MvpRankingsRenderer's MvpRow[] shape). Score unchanged: survival 40%, damage 40%,
        /// kills 20%.</summary>
        public async Task Top5MatchMVP(Match matches)
        {
            await using var _vmix_GraphicsContext = await _dbContextFactory.CreateDbContextAsync();
            try
            {
                var top5MVPs = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId)
                    .Select(p => new
                    {
                        Player = p,
                        Score = (p.SurvivalTime * 0.4) + (p.Damage * 0.4) + (p.KillNum * 0.2)
                    })
                    .OrderByDescending(p => p.Score)
                    .Take(5)
                    .ToList();

                if (!top5MVPs.Any())
                {
                    logger.LogWarning("Top5MatchMVP: no player stats saved for match {MatchId}.", matches.MatchId);
                    return;
                }

                var teams = _vmix_GraphicsContext.Teams.Where(x => x.StageId == matches.StageId).ToList();
                var matchPlayers = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId)
                    .ToList();

                var rows = new List<object>();
                int rank = 1;
                foreach (var mvp in top5MVPs)
                {
                    var player = mvp.Player;
                    var teamdata = teams.FirstOrDefault(x => x.TeamId == player.TeamId.ToString());
                    var totalTeamKills = matchPlayers.Where(x => x.TeamId == player.TeamId).Sum(x => x.KillNum ?? 0);

                    rows.Add(new
                    {
                        rank,
                        playerName = player.PlayerName,
                        teamName = teamdata?.TeamName ?? player.TeamId.ToString(),
                        kills = player.KillNum ?? 0,
                        damage = player.Damage ?? 0,
                        assists = player.Assists ?? 0,
                        knocks = player.Knockouts ?? 0,
                        survivalTime = FormatSurvival(player.SurvivalTime),
                        rating = mvp.Score.HasValue ? Math.Round(mvp.Score.Value, 1) : (double?)null,
                        contribution = totalTeamKills > 0 ? Math.Round((double)(player.KillNum ?? 0) / totalTeamKills * 100, 1) : 0,
                        logoUrl = MediaUrls.TeamLogo(player.TeamId),
                        photoUrl = MediaUrls.PlayerPhoto(player.PlayerUId),
                    });
                    rank++;
                }

                PublishGraphic(GraphicEvents.MvpRankingsUpdated, rows);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Top5MatchMVP");
            }
        }

        /// <summary>Top 5 players across the whole stage -> overlay "MvpRankingsUpdated" (same
        /// slot as the match version; whichever was run last is what the table shows).</summary>
        public async Task Top5StageMVP(Match matches)
        {
            await using var _vmix_GraphicsContext = await _dbContextFactory.CreateDbContextAsync();
            try
            {
                var top5StageMVPs = (await LoadStageMvpsAsync(_vmix_GraphicsContext, matches.StageId)).Take(5).ToList();
                if (!top5StageMVPs.Any())
                {
                    logger.LogWarning("Top5StageMVP: no player stats saved for stage {StageId}.", matches.StageId);
                    return;
                }

                var rows = new List<object>();
                int rank = 1;
                foreach (var p in top5StageMVPs)
                {
                    rows.Add(new
                    {
                        rank,
                        playerName = p.PlayerName,
                        teamName = p.TeamName,
                        kills = p.TotalKills,
                        damage = p.TotalDamage,
                        assists = p.TotalAssists,
                        knocks = p.TotalKnockouts,
                        survivalTime = FormatSurvival(p.AvgSurvivalTime),
                        rating = Math.Round(p.Score, 1),
                        contribution = p.Contribution,
                        matchesPlayed = p.MatchesPlayed,
                        logoUrl = MediaUrls.TeamLogo(p.TeamId),
                        photoUrl = MediaUrls.PlayerPhoto(p.PlayerUId),
                    });
                    rank++;
                }

                PublishGraphic(GraphicEvents.MvpRankingsUpdated, rows);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Top5StageMVP");
            }
        }

        /// <summary>The single best player of the stage -> overlay "PlayerHighlightUpdated"
        /// with the "STAGE MVP" label.</summary>
        public async Task StageMVP(Match matches)
        {
            await using var _vmix_GraphicsContext = await _dbContextFactory.CreateDbContextAsync();
            try
            {
                var best = (await LoadStageMvpsAsync(_vmix_GraphicsContext, matches.StageId)).FirstOrDefault();
                if (best == null)
                {
                    logger.LogWarning("StageMVP: no player stats saved for stage {StageId}.", matches.StageId);
                    return;
                }

                PublishGraphic(GraphicEvents.PlayerHighlightUpdated, new
                {
                    label = "STAGE MVP",
                    playerName = best.PlayerName,
                    teamName = best.TeamName.ToUpper(),
                    photoUrl = MediaUrls.PlayerPhoto(best.PlayerUId),
                    teamLogoUrl = MediaUrls.TeamLogo(best.TeamId),
                    stats = new object[]
                    {
                        new { label = "ELIMS", value = best.TotalKills },
                        new { label = "DAMAGE", value = best.TotalDamage },
                        new { label = "AVG SURVIVAL", value = FormatSurvival(best.AvgSurvivalTime) },
                        new { label = "ASSISTS", value = best.TotalAssists },
                        new { label = "KNOCKS", value = best.TotalKnockouts },
                        new { label = "MATCHES", value = best.MatchesPlayed },
                    },
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in StageMVP");
            }
        }

        private sealed record StageMvp(long PlayerUId, string PlayerName, int TeamId, string TeamName,
            int TotalKills, int TotalDamage, double AvgSurvivalTime, int TotalAssists, int TotalKnockouts,
            int MatchesPlayed, double Score, double Contribution);

        /// <summary>Stage-wide player aggregates ordered by MVP score. Aggregation is done in
        /// memory: grouping PlayerStats and then taking g.First() for the name, as the old query
        /// did, is not reliably translatable by EF Core.</summary>
        private static async Task<List<StageMvp>> LoadStageMvpsAsync(vmix_graphicsContext db, int stageId)
        {
            var stats = await db.PlayerStats.Where(x => x.StageId == stageId).AsNoTracking().ToListAsync();
            var teams = await db.Teams.Where(x => x.StageId == stageId).AsNoTracking().ToListAsync();
            var teamKills = stats.GroupBy(x => x.TeamId).ToDictionary(g => g.Key, g => g.Sum(x => x.KillNum ?? 0));

            return stats
                .GroupBy(x => x.PlayerUId)
                .Select(g =>
                {
                    var first = g.First();
                    int kills = g.Sum(x => x.KillNum ?? 0);
                    int damage = g.Sum(x => x.Damage ?? 0);
                    double avgSurvival = g.Average(x => x.SurvivalTime);
                    int teamTotalKills = teamKills.TryGetValue(first.TeamId, out var tk) ? tk : 0;
                    return new StageMvp(
                        g.Key,
                        first.PlayerName,
                        first.TeamId,
                        teams.FirstOrDefault(t => t.TeamId == first.TeamId.ToString())?.TeamName ?? first.TeamId.ToString(),
                        kills,
                        damage,
                        avgSurvival,
                        g.Sum(x => x.Assists ?? 0),
                        g.Sum(x => x.Knockouts ?? 0),
                        g.Count(),
                        (avgSurvival * 0.4) + (damage * 0.4) + (kills * 0.2),
                        teamTotalKills > 0 ? Math.Round((double)kills / teamTotalKills * 100, 1) : 0);
                })
                .OrderByDescending(p => p.Score)
                .ToList();
        }
    }
}
