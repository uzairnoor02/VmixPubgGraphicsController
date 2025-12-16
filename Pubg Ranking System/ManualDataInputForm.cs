using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using VmixData.Models;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness;
using VmixGraphicsBusiness.PostMatchStats;

namespace Pubg_Ranking_System
{
    public partial class ManualDataInputForm : Form
    {
        private readonly ILogger<ManualDataInputForm> _logger;
        private readonly PostMatch _postMatch;
        private readonly vmix_graphicsContext _context;
        private readonly TournamentBusiness _tournamentBusiness;

        public ManualDataInputForm(
            ILogger<ManualDataInputForm> logger,
            PostMatch postMatch,
            vmix_graphicsContext context,
            TournamentBusiness tournamentBusiness)
        {
            _logger = logger;
            _postMatch = postMatch;
            _context = context;
            _tournamentBusiness = tournamentBusiness;

            InitializeComponent();
            LoadTournamentData();
        }

        private void LoadTournamentData()
        {
            try
            {
                var tournaments = _tournamentBusiness.getAll().Select(x => x.Name).ToList();
                cmbTournament.DataSource = tournaments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tournament data");
            }
        }

        private void LoadStages()
        {
            try
            {
                if (cmbTournament.SelectedItem != null)
                {
                    var stages = _tournamentBusiness.getAllStages()
                        .Where(s => s.Tournament.Name == cmbTournament.SelectedItem.ToString())
                        .Select(x => x.Name)
                        .ToList();
                    cmbStage.DataSource = stages;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading stages");
            }
        }

        private void BtnLoadPlayerFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.Title = "Load Player Data JSON";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        txtPlayerJson.Text = File.ReadAllText(openFileDialog.FileName);
                        lblPlayerStatus.Text = $"Loaded: {Path.GetFileName(openFileDialog.FileName)}";
                        lblPlayerStatus.ForeColor = System.Drawing.Color.Blue;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading file: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnLoadTeamFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.Title = "Load Team Data JSON";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        txtTeamJson.Text = File.ReadAllText(openFileDialog.FileName);
                        lblTeamStatus.Text = $"Loaded: {Path.GetFileName(openFileDialog.FileName)}";
                        lblTeamStatus.ForeColor = System.Drawing.Color.Blue;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading file: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnValidatePlayer_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtPlayerJson.Text))
                {
                    MessageBox.Show("Please enter or load player JSON data!", "No Data",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var playerData = JsonSerializer.Deserialize<LivePlayersList>(txtPlayerJson.Text);

                if (playerData?.PlayerInfoList == null || !playerData.PlayerInfoList.Any())
                {
                    throw new Exception("No player data found or empty list");
                }

                lblPlayerStatus.Text = $"✓ Valid - {playerData.PlayerInfoList.Count} players";
                lblPlayerStatus.ForeColor = System.Drawing.Color.Green;

                MessageBox.Show(
                    $"Player JSON is valid!\n\nPlayers found: {playerData.PlayerInfoList.Count}",
                    "Validation Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (JsonException ex)
            {
                lblPlayerStatus.Text = "✗ Invalid JSON";
                lblPlayerStatus.ForeColor = System.Drawing.Color.Red;
                MessageBox.Show($"Invalid JSON:\n{ex.Message}", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                lblPlayerStatus.Text = "✗ Validation failed";
                lblPlayerStatus.ForeColor = System.Drawing.Color.Red;
                MessageBox.Show($"Error:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnValidateTeam_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtTeamJson.Text))
                {
                    MessageBox.Show("Please enter or load team JSON data!", "No Data",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var teamData = JsonSerializer.Deserialize<TeamInfoList>(txtTeamJson.Text);

                if (teamData?.teamInfoList == null || !teamData.teamInfoList.Any())
                {
                    throw new Exception("No team data found or empty list");
                }

                lblTeamStatus.Text = $"✓ Valid - {teamData.teamInfoList.Count} teams";
                lblTeamStatus.ForeColor = System.Drawing.Color.Green;

                MessageBox.Show(
                    $"Team JSON is valid!\n\nTeams found: {teamData.teamInfoList.Count}",
                    "Validation Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (JsonException ex)
            {
                lblTeamStatus.Text = "✗ Invalid JSON";
                lblTeamStatus.ForeColor = System.Drawing.Color.Red;
                MessageBox.Show($"Invalid JSON:\n{ex.Message}", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                lblTeamStatus.Text = "✗ Validation failed";
                lblTeamStatus.ForeColor = System.Drawing.Color.Red;
                MessageBox.Show($"Error:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnCreateBackup_Click(object sender, EventArgs e)
        {
            if (!ValidateMatchSelection())
                return;

            try
            {
                btnCreateBackup.Enabled = false;
                btnCreateBackup.Text = "Creating backup...";

                var match = await GetMatchFromSelection();

                var backupService = new ManualDataBackupService(_context, _logger);
                string backupPath = await backupService.CreateFullMatchBackupAsync(match);

                MessageBox.Show(
                    $"Backup created!\n\nLocation:\n{backupPath}",
                    "Backup Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                btnApplyToDb.Enabled = true;
                _logger.LogInformation("Backup created: {BackupPath}", backupPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating backup");
                MessageBox.Show($"Error creating backup:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnCreateBackup.Enabled = true;
                btnCreateBackup.Text = "1. Create Backup";
            }
        }

        private async void BtnApplyToDb_Click(object sender, EventArgs e)
        {
            if (!ValidateMatchSelection())
                return;

            if (string.IsNullOrWhiteSpace(txtPlayerJson.Text) || string.IsNullOrWhiteSpace(txtTeamJson.Text))
            {
                MessageBox.Show("Please provide both Player and Team JSON data!", "Missing Data",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirmResult = MessageBox.Show(
                "⚠️ WARNING ⚠️\n\n" +
                "This will OVERWRITE existing match data!\n\n" +
                "Make sure you created a backup.\n\n" +
                "Continue?",
                "Confirm Data Update",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2
            );

            if (confirmResult != DialogResult.Yes)
                return;

            try
            {
                btnApplyToDb.Enabled = false;
                btnApplyToDb.Text = "Applying...";

                var match = await GetMatchFromSelection();
                var playerData = JsonSerializer.Deserialize<LivePlayersList>(txtPlayerJson.Text);
                var teamData = JsonSerializer.Deserialize<TeamInfoList>(txtTeamJson.Text);

                await _postMatch.savePlayersinfo(playerData, match);
                await Task.Delay(1000);
                await _postMatch.saveTeamsinfo(teamData, match, playerData);

                MessageBox.Show(
                    "Data applied successfully!\n\n" +
                    $"Players: {playerData.PlayerInfoList.Count}\n" +
                    $"Teams: {teamData.teamInfoList.Count}",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                _logger.LogInformation(
                    "Manual data applied: Match={MatchId}, Day={Day}",
                    match.MatchId, match.MatchDayId
                );

                this.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying data");
                MessageBox.Show(
                    $"Error:\n{ex.Message}\n\nCheck SQL backup files for recovery.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                btnApplyToDb.Enabled = true;
                btnApplyToDb.Text = "2. Apply to Database";
            }
        }

        private bool ValidateMatchSelection()
        {
            if (cmbTournament.SelectedItem == null || cmbStage.SelectedItem == null ||
                cmbDay.SelectedItem == null || cmbMatch.SelectedItem == null)
            {
                MessageBox.Show("Please select all match details!",
                    "Missing Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private async Task<Match> GetMatchFromSelection()
        {
            var tournament = await _context.Tournaments
                .FirstOrDefaultAsync(x => x.Name == cmbTournament.SelectedItem.ToString());

            if (tournament == null)
                throw new Exception("Tournament not found!");

            var stage = await _context.Stages
                .FirstOrDefaultAsync(x => x.Name == cmbStage.SelectedItem.ToString() &&
                                         x.TournamentId == tournament.TournamentId);

            if (stage == null)
                throw new Exception("Stage not found!");

            var match = await _context.Matches
                .FirstOrDefaultAsync(x =>
                    x.TournamentId == tournament.TournamentId &&
                    x.StageId == stage.StageId &&
                    x.MatchDayId == int.Parse(cmbDay.SelectedItem.ToString()) &&
                    x.MatchId == int.Parse(cmbMatch.SelectedItem.ToString()));

            if (match == null)
            {
                throw new Exception("Match not found in database!");
            }

            return match;
        }
    }
}