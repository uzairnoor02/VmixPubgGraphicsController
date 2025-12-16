using System;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using VmixData.Models;
using Microsoft.EntityFrameworkCore;

namespace VmixGraphicsBusiness
{
    public class TournamentBusiness(vmix_graphicsContext _context)
    {
        public List<Tournament> getAll()
        {
            return _context.Tournaments.ToList();
        }
        public List<Stage> getAllStages()
        {
            return _context.Stages.ToList();
        }
        public (string, int) Add_stage_btn_Click(Stage stage, string tournamentname)
        {
            Tournament tournament = _context.Tournaments.Where(x => x.Name == tournamentname).FirstOrDefault();
            if (!string.IsNullOrEmpty(stage.Name))
            {
                stage.TournamentId = tournament.TournamentId;
                _context.Stages.Add(stage);
                _context.SaveChanges();
                return ("Stage saved successfully", 1);
            }
            else
            {
                return ("Please Input all contents to add stage", 0);
            }
        }

        public (string, int) Save_Click(Stage stage, string tournamentname)
        {
            Tournament tournament = _context.Tournaments.Where(x => x.Name == tournamentname).FirstOrDefault()!;
            try
            {
                if (!string.IsNullOrEmpty(stage.Name))
                {
                    _context.Stages.Add(stage);
                    _context.SaveChanges();
                    return ("Stage saved successfully", 1);
                }
                else
                {
                    return ("Please Input all contents to add stage", 0);
                }
            }
            catch (Exception ex)
            {
                return ("Saved", 1);
            }
        }
        public async Task<(string, int)> add_tournament_btn_Click(Tournament tournament)
        {
            try
            {
                _context.Tournaments.Add(tournament);
                await _context.SaveChangesAsync();
                tournament = _context.Tournaments.Where(x => x.Name == tournament.Name).FirstOrDefault();
                return ("Tournament saved successfully", 1);
            }
            catch (Exception ex)
            {
                return ($"Exception occcured {ex}", 0);
            }
        }
        public async Task<(string message, int statusCode, Match match, bool isCompleted)> add_match(
     string tournamentName, string stageName, string day, string matchNumber)
        {
            var tournament = await _context.Tournaments
                .FirstOrDefaultAsync(x => x.Name == tournamentName);

            var stage = await _context.Stages
                .FirstOrDefaultAsync(x => x.Name == stageName && x.TournamentId == tournament.TournamentId);

            var match = await _context.Matches
                .FirstOrDefaultAsync(x =>
                    x.TournamentId == tournament.TournamentId &&
                    x.StageId == stage.StageId &&
                    x.MatchDayId == int.Parse(day) &&
                    x.MatchId == int.Parse(matchNumber));

            if (match == null)
            {
                // Create new match
                match = new Match
                {
                    MatchId = int.Parse(matchNumber),
                    MatchDayId = int.Parse(day),
                    StageId = stage.StageId,
                    TournamentId = tournament.TournamentId
                };
                await _context.Matches.AddAsync(match);
                await _context.SaveChangesAsync();

                return ("New match created successfully.", 0, match, false);
            }

            // Check if match has data (is completed or in progress)
            bool hasPlayerStats = await _context.PlayerStats
                .AnyAsync(x => x.MatchId == match.MatchId &&
                              x.DayId == match.MatchDayId &&
                              x.StageId == match.StageId);

            bool hasTeamPoints = await _context.TeamPoints
                .AnyAsync(x => x.MatchId == match.MatchId &&
                              x.DayId == match.MatchDayId &&
                              x.StageId == match.StageId);

            // Check if match is completed (has final rank 1 - winner exists)
            bool isCompleted = await _context.PlayerStats
                .AnyAsync(x => x.MatchId == match.MatchId &&
                              x.DayId == match.MatchDayId &&
                              x.StageId == match.StageId &&
                              x.Rank == 1);

            if (isCompleted)
            {
                return (
                    $"⚠️ MATCH ALREADY COMPLETED ⚠️\n\n" +
                    $"Match {matchNumber} on Day {day} has finished data.\n\n" +
                    $"Starting this match will:\n" +
                    $"• Delete all existing player stats\n" +
                    $"• Delete all team points\n" +
                    $"• Reset rankings\n\n" +
                    $"Are you sure you want to RESTART this completed match?",
                    2,
                    match,
                    true
                );
            }
            else if (hasPlayerStats || hasTeamPoints)
            {
                return (
                    $"Match {matchNumber} on Day {day} has partial data.\n\n" +
                    $"Do you want to continue from where it left off?",
                    1,
                    match,
                    false
                );
            }

            return ("Match exists but has no data. Ready to start.", 0, match, false);
        }
        public async Task DeleteMatchHistory(Match match)
        {
            var matchinfo = _context.TeamPoints.Where(x => x.DayId == match.MatchDayId & x.MatchId == match.MatchId & x.StageId == match.StageId);
            var playerstatsinfo = _context.PlayerStats.Where(x => x.DayId == match.MatchDayId & x.MatchId == match.MatchId & x.StageId == match.StageId);
            if (matchinfo != null)
            {
                _context.TeamPoints.RemoveRange(matchinfo);
            }
            if (playerstatsinfo != null)
            {
                _context.PlayerStats.RemoveRange(playerstatsinfo);
            }
            await _context.SaveChangesAsync();


        }
    }
}
