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
        /// <summary>
        /// Resolves the Match row for a pcob GameID, creating one if this GameID has never been
        /// seen before. Day and match-number are derived, never asked for: Day reuses the day slot
        /// this Tournament+Stage already has for `referenceDate` (or starts the next one if that
        /// date is new to this Tournament+Stage), and MatchId auto-increments within that day.
        ///
        /// `referenceDate` is the calendar date this Match actually belongs to -- GetLiveData.
        /// RunAutoTrackingAsync passes today (the live case), while ManualDataInputForm's
        /// getallinfo-restore path passes the date derived from that game's own FinishedStartTime,
        /// so a match played yesterday still lands on yesterday's day slot, not today's.
        /// </summary>
        public async Task<Match> GetOrCreateMatchByGameIdAsync(int tournamentId, int stageId, string gameId, DateTime referenceDate)
        {
            var existing = await _context.Matches.FirstOrDefaultAsync(x => x.GameId == gameId);
            if (existing != null) return existing;

            var date = referenceDate.Date;
            var sameDateMatch = await _context.Matches
                .Where(x => x.TournamentId == tournamentId && x.StageId == stageId && x.StartTime.Date == date)
                .OrderByDescending(x => x.MatchDayId)
                .FirstOrDefaultAsync();

            int dayId;
            if (sameDateMatch != null)
            {
                dayId = sameDateMatch.MatchDayId;
            }
            else
            {
                var maxDay = await _context.Matches
                    .Where(x => x.TournamentId == tournamentId && x.StageId == stageId)
                    .Select(x => (int?)x.MatchDayId)
                    .MaxAsync() ?? 0;
                dayId = maxDay + 1;
            }

            var maxMatchNum = await _context.Matches
                .Where(x => x.TournamentId == tournamentId && x.StageId == stageId && x.MatchDayId == dayId)
                .Select(x => (int?)x.MatchId)
                .MaxAsync() ?? 0;

            var match = new Match
            {
                MatchId = maxMatchNum + 1,
                MatchDayId = dayId,
                StageId = stageId,
                TournamentId = tournamentId,
                GameId = gameId,
                StartTime = date
            };

            await _context.Matches.AddAsync(match);
            await _context.SaveChangesAsync();
            return match;
        }

        /// <summary>
        /// Whether final results have already been saved for this match (PlayerStats/TeamPoints
        /// exist). The auto-tracking loop uses this to decide whether a finished pcob game still
        /// needs createPostMtachStats run, or was already captured on an earlier poll.
        /// </summary>
        public async Task<bool> HasResultsAsync(Match match)
        {
            bool hasPlayerStats = await _context.PlayerStats
                .AnyAsync(x => x.MatchId == match.MatchId && x.DayId == match.MatchDayId && x.StageId == match.StageId);
            if (hasPlayerStats) return true;

            return await _context.TeamPoints
                .AnyAsync(x => x.MatchId == match.MatchId && x.DayId == match.MatchDayId && x.StageId == match.StageId);
        }

        /// <summary>
        /// The most recent match on `referenceDate` for this Tournament+Stage that has a pcob
        /// GameID (so it was actually attempted, not just a placeholder row) but no saved results
        /// -- i.e. it was tracked but never concluded (crash, pcob restarted mid-match, etc).
        /// RunAutoTrackingAsync uses this to recognize "a new, unrecognized GameID showed up, but
        /// the last match never finished" and ask the operator what to do instead of silently
        /// creating the next match number.
        /// </summary>
        public async Task<Match> FindIncompleteMatchAsync(int tournamentId, int stageId, DateTime referenceDate)
        {
            var date = referenceDate.Date;
            var candidates = await _context.Matches
                .Where(x => x.TournamentId == tournamentId && x.StageId == stageId &&
                            x.StartTime.Date == date && x.GameId != null)
                .OrderByDescending(x => x.MatchId)
                .ToListAsync();

            foreach (var candidate in candidates)
            {
                if (!await HasResultsAsync(candidate))
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// Re-points an existing (incomplete) Match row at a different pcob GameID -- used when
        /// the operator confirms a new game is a restart/continuation of a match that never
        /// concluded, rather than treating the new game as the next match number.
        /// </summary>
        public async Task<Match> ReassignGameIdAsync(Match match, string newGameId)
        {
            var tracked = await _context.Matches.FindAsync(match.Id);
            if (tracked == null) return match;

            tracked.GameId = newGameId;
            await _context.SaveChangesAsync();
            return tracked;
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
