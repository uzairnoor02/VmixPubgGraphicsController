﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace Pubg_Ranking_System.Models;

public partial class Stage
{
    public int StageId { get; set; }

    public int TournamentId { get; set; }

    public string Name { get; set; }

    public int NumDays { get; set; }

    public int NumTeams { get; set; }

    public virtual ICollection<TeamsStage> TeamsStages { get; set; } = new List<TeamsStage>();

    public virtual Tournament Tournament { get; set; }
}