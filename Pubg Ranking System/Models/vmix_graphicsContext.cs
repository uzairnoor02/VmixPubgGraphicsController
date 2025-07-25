﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Pubg_Ranking_System.Models;

public partial class vmix_graphicsContext : DbContext
{
    public vmix_graphicsContext(DbContextOptions<vmix_graphicsContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Match> Matches { get; set; }

    public virtual DbSet<Player> Players { get; set; }

    public virtual DbSet<PlayerStat> PlayerStats { get; set; }

    public virtual DbSet<Stage> Stages { get; set; }

    public virtual DbSet<Team> Teams { get; set; }

    public virtual DbSet<TeamPoint> TeamPoints { get; set; }

    public virtual DbSet<TeamsStage> TeamsStages { get; set; }

    public virtual DbSet<Tournament> Tournaments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasKey(e => e.MatchId).HasName("PRIMARY");

            entity.ToTable("matches");

            entity.HasIndex(e => e.TournamentId, "tournament_id");

            entity.Property(e => e.MatchId).HasColumnName("match_id");
            entity.Property(e => e.EndTime)
                .HasColumnType("datetime")
                .HasColumnName("end_time");
            entity.Property(e => e.MatchDayId).HasColumnName("match_day_id");
            entity.Property(e => e.StageId).HasColumnName("stage_id");
            entity.Property(e => e.StartTime)
                .HasColumnType("datetime")
                .HasColumnName("start_time");
            entity.Property(e => e.TournamentId).HasColumnName("tournament_id");

            entity.HasOne(d => d.Tournament).WithMany(p => p.Matches)
                .HasForeignKey(d => d.TournamentId)
                .HasConstraintName("matches_ibfk_1");
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("players");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PlayerDisplayName)
                .HasMaxLength(45)
                .HasColumnName("player_display_name");
            entity.Property(e => e.PlayerGameName)
                .HasMaxLength(45)
                .HasColumnName("player_game_name");
        });

        modelBuilder.Entity<PlayerStat>(entity =>
        {
            entity.HasKey(e => e.PlayerStatId).HasName("PRIMARY");

            entity.ToTable("player_stats");

            entity.HasIndex(e => e.MatchId, "match_id");

            entity.Property(e => e.PlayerStatId).HasColumnName("player_stat_id");
            entity.Property(e => e.Assists).HasColumnName("assists");
            entity.Property(e => e.Character)
                .HasMaxLength(50)
                .HasColumnName("character");
            entity.Property(e => e.CurWeaponId).HasColumnName("cur_weapon_id");
            entity.Property(e => e.Damage).HasColumnName("damage");
            entity.Property(e => e.DriveDistance).HasColumnName("drive_distance");
            entity.Property(e => e.GotAirdropNum).HasColumnName("got_airdrop_num");
            entity.Property(e => e.HeadshotNum).HasColumnName("headshot_num");
            entity.Property(e => e.Heal).HasColumnName("heal");
            entity.Property(e => e.HealTeammateNum).HasColumnName("heal_teammate_num");
            entity.Property(e => e.Health).HasColumnName("health");
            entity.Property(e => e.HealthMax).HasColumnName("health_max");
            entity.Property(e => e.InDamage).HasColumnName("in_damage");
            entity.Property(e => e.IsFiring).HasColumnName("is_firing");
            entity.Property(e => e.IsOutsideBlueCircle).HasColumnName("is_outside_blue_circle");
            entity.Property(e => e.KillNum).HasColumnName("kill_num");
            entity.Property(e => e.KillNumBeforeDie).HasColumnName("kill_num_before_die");
            entity.Property(e => e.KillNumByGrenade).HasColumnName("kill_num_by_grenade");
            entity.Property(e => e.KillNumInVehicle).HasColumnName("kill_num_in_vehicle");
            entity.Property(e => e.Knockouts).HasColumnName("knockouts");
            entity.Property(e => e.LiveState).HasColumnName("live_state");
            entity.Property(e => e.MarchDistance).HasColumnName("march_distance");
            entity.Property(e => e.MatchId).HasColumnName("match_id");
            entity.Property(e => e.MaxKillDistance).HasColumnName("max_kill_distance");
            entity.Property(e => e.OutsideBlueCircleTime).HasColumnName("outside_blue_circle_time");
            entity.Property(e => e.PicUrl)
                .HasMaxLength(500)
                .HasColumnName("pic_url");
            entity.Property(e => e.PlayerKey).HasColumnName("player_key");
            entity.Property(e => e.PlayerName)
                .HasMaxLength(255)
                .HasColumnName("player_name");
            entity.Property(e => e.PlayerOpenId)
                .HasMaxLength(50)
                .HasColumnName("player_open_id");
            entity.Property(e => e.PlayerUId).HasColumnName("player_uID");
            entity.Property(e => e.PosX).HasColumnName("posX");
            entity.Property(e => e.PosY).HasColumnName("posY");
            entity.Property(e => e.PosZ).HasColumnName("posZ");
            entity.Property(e => e.Rank).HasColumnName("rank");
            entity.Property(e => e.RescueTimes).HasColumnName("rescue_times");
            entity.Property(e => e.ShowPicUrl).HasColumnName("show_pic_url");
            entity.Property(e => e.SurvivalTime).HasColumnName("survival_time");
            entity.Property(e => e.TeamId).HasColumnName("team_id");
            entity.Property(e => e.UseFragGrenadeNum).HasColumnName("use_frag_grenade_num");
            entity.Property(e => e.UseSmokeGrenadeNum).HasColumnName("use_smoke_grenade_num");

            entity.HasOne(d => d.Match).WithMany(p => p.PlayerStats)
                .HasForeignKey(d => d.MatchId)
                .HasConstraintName("player_stats_ibfk_1");
        });

        modelBuilder.Entity<Stage>(entity =>
        {
            entity.HasKey(e => e.StageId).HasName("PRIMARY");

            entity.ToTable("stages");

            entity.HasIndex(e => e.TournamentId, "tournament_id");

            entity.Property(e => e.StageId).HasColumnName("stage_id");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.NumDays).HasColumnName("num_days");
            entity.Property(e => e.NumTeams).HasColumnName("num_teams");
            entity.Property(e => e.TournamentId).HasColumnName("tournament_id");

            entity.HasOne(d => d.Tournament).WithMany(p => p.Stages)
                .HasForeignKey(d => d.TournamentId)
                .HasConstraintName("stages_ibfk_1");
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("teams");

            entity.HasIndex(e => e.TournamentId, "fr_tournament_id_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TeamId)
                .IsRequired()
                .HasMaxLength(45)
                .HasColumnName("team_id");
            entity.Property(e => e.TeamName)
                .IsRequired()
                .HasMaxLength(45)
                .HasColumnName("team_name");
            entity.Property(e => e.TournamentId).HasColumnName("tournament_id");

            entity.HasOne(d => d.Tournament).WithMany(p => p.Teams)
                .HasForeignKey(d => d.TournamentId)
                .HasConstraintName("fr_tournament_id");
        });

        modelBuilder.Entity<TeamPoint>(entity =>
        {
            entity.HasKey(e => e.TeamPointId).HasName("PRIMARY");

            entity.ToTable("team_points");

            entity.HasIndex(e => e.MatchId, "match_id");

            entity.Property(e => e.TeamPointId).HasColumnName("team_point_id");
            entity.Property(e => e.KillPoints).HasColumnName("kill_points");
            entity.Property(e => e.MatchId).HasColumnName("match_id");
            entity.Property(e => e.PlacementPoints).HasColumnName("placement_points");
            entity.Property(e => e.TeamId).HasColumnName("team_id");
            entity.Property(e => e.TotalPoints).HasColumnName("total_points");

            entity.HasOne(d => d.Match).WithMany(p => p.TeamPoints)
                .HasForeignKey(d => d.MatchId)
                .HasConstraintName("team_points_ibfk_1");
        });

        modelBuilder.Entity<TeamsStage>(entity =>
        {
            entity.HasKey(e => e.TeamStageDayId).HasName("PRIMARY");

            entity.ToTable("teams_stages");

            entity.HasIndex(e => e.StageId, "stage_id");

            entity.HasIndex(e => e.TournamentId, "tournament_id");

            entity.Property(e => e.TeamStageDayId).HasColumnName("team_stage_day_id");
            entity.Property(e => e.DayNumber).HasColumnName("day_number");
            entity.Property(e => e.StageId).HasColumnName("stage_id");
            entity.Property(e => e.TeamId).HasColumnName("team_id");
            entity.Property(e => e.TeamName)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("team_name");
            entity.Property(e => e.TournamentId).HasColumnName("tournament_id");

            entity.HasOne(d => d.Stage).WithMany(p => p.TeamsStages)
                .HasForeignKey(d => d.StageId)
                .HasConstraintName("teams_stages_ibfk_2");

            entity.HasOne(d => d.Tournament).WithMany(p => p.TeamsStages)
                .HasForeignKey(d => d.TournamentId)
                .HasConstraintName("teams_stages_ibfk_1");
        });

        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.HasKey(e => e.TournamentId).HasName("PRIMARY");

            entity.ToTable("tournaments");

            entity.Property(e => e.TournamentId).HasColumnName("tournament_id");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("name");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}