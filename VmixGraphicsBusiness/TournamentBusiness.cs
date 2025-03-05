using System;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using VmixData.Models;
using Microsoft.EntityFrameworkCore;

namespace VmixGraphicsBusiness
{
    public class TournamentBusiness(vmix_graphicsContext _vmix_GraphicsContext)
    {
        public List<Tournament> getAll()
        {
            return _vmix_GraphicsContext.Tournaments.ToList();
        }
        public List<Stage> getAllStages()
        {
            return _vmix_GraphicsContext.Stages.ToList();
        }
        public (string, int) Add_stage_btn_Click(Stage stage, string tournamentname)
        {
            Tournament tournament = _vmix_GraphicsContext.Tournaments.Where(x => x.Name == tournamentname).FirstOrDefault();
            if (!string.IsNullOrEmpty(stage.Name))
            {
                stage.TournamentId = tournament.TournamentId;
                _vmix_GraphicsContext.Stages.Add(stage);
                _vmix_GraphicsContext.SaveChanges();
                return ("Stage saved successfully", 1);
            }
            else
            {
                return ("Please Input all contents to add stage", 0);
            }
        }

        public (string, int) Save_Click(Stage stage, string tournamentname)
        {
            Tournament tournament = _vmix_GraphicsContext.Tournaments.Where(x => x.Name == tournamentname).FirstOrDefault()!;
            try
            {
                if (!string.IsNullOrEmpty(stage.Name))
                {
                    _vmix_GraphicsContext.Stages.Add(stage);
                    _vmix_GraphicsContext.SaveChanges();
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
                _vmix_GraphicsContext.Tournaments.Add(tournament);
                await _vmix_GraphicsContext.SaveChangesAsync();
                tournament = _vmix_GraphicsContext.Tournaments.Where(x => x.Name == tournament.Name).FirstOrDefault();
                return ("Tournament saved successfully", 1);
            }
            catch (Exception ex)
            {
                return ($"Exception occcured {ex}", 0);
            }
        }
        public async Task<(string, int,Match)> add_match(string tournamentName,string stageName,string DayNumber, string matchnumber)
        {
            var tournament = _vmix_GraphicsContext.Tournaments.Where(x => x.Name == tournamentName).FirstOrDefault();
            var stage=_vmix_GraphicsContext.Stages.Where(x=>x.Name==stageName).FirstOrDefault();
            var match=await _vmix_GraphicsContext.Matches.Where(x => x.TournamentId == tournament.TournamentId && x.StageId == stage.StageId && x.MatchDayId == int.Parse(DayNumber) && x.MatchId==int.Parse(matchnumber)).FirstOrDefaultAsync();
            if (match != null )
            {
                return ("Match already exist, Do you still want to continue?", 1,match!);
            }
            else
            {
                _vmix_GraphicsContext.Matches.Add(new Match()
                {
                    MatchDayId= int.Parse(DayNumber),
                    MatchId= int.Parse(matchnumber),
                    TournamentId=tournament.TournamentId,
                    StageId=stage.StageId,
                    StartTime= DateTime.Now,
                });
                await _vmix_GraphicsContext.SaveChangesAsync();

                match = await _vmix_GraphicsContext.Matches.Where(x => x.TournamentId == tournament.TournamentId && x.StageId == stage.StageId && x.MatchDayId == int.Parse(DayNumber) && x.MatchId == int.Parse(matchnumber)).FirstOrDefaultAsync();
                match.MatchName = "match_" + match.Id;
                _vmix_GraphicsContext.SaveChanges();
                return ("", 0,match!);
            }
        }
    }
}
