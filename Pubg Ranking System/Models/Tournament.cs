﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace Pubg_Ranking_System.Models;

public partial class Tournament
{
    public int TournamentId { get; set; }

    public string Name { get; set; }

    public virtual ICollection<Match> Matches { get; set; } = new List<Match>();

    public virtual ICollection<Stage> Stages { get; set; } = new List<Stage>();

    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();

    public virtual ICollection<TeamsStage> TeamsStages { get; set; } = new List<TeamsStage>();
}