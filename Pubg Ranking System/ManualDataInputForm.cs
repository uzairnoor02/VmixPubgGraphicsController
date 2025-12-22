// ManualDataInputForm.cs

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
                MessageBox.Show($"Error loading tournaments: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show($"Error loading stages: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                        _logger.LogInformation("Player JSON file loaded: {FileName}", openFileDialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading player JSON file");
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
                        _logger.LogInformation("Team JSON file loaded: {FileName}", openFileDialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading team JSON file");
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

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var playerData = JsonSerializer.Deserialize<LivePlayersList>(txtPlayerJson.Text, options);

                if (playerData?.PlayerInfoList == null || !playerData.PlayerInfoList.Any())
                {
                    throw new Exception("No player data found or empty list");
                }

                lblPlayerStatus.Text = $"✓ Valid - {playerData.PlayerInfoList.Count} players";
                lblPlayerStatus.ForeColor = System.Drawing.Color.Green;

                var teamCount = playerData.PlayerInfoList.Select(p => p.TeamId).Distinct().Count();

                MessageBox.Show(
                    $"Player JSON is valid!\n\n" +
                    $"Players found: {playerData.PlayerInfoList.Count}\n" +
                    $"Teams: {teamCount}\n" +
                    $"Alive players: {playerData.PlayerInfoList.Count(p => p.LiveState == 0)}",
                    "Validation Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                _logger.LogInformation("Player JSON validated: {Count} players, {Teams} teams",
                    playerData.PlayerInfoList.Count, teamCount);
            }
            catch (JsonException ex)
            {
                lblPlayerStatus.Text = "✗ Invalid JSON";
                lblPlayerStatus.ForeColor = System.Drawing.Color.Red;
                _logger.LogError(ex, "Invalid player JSON format");
                MessageBox.Show($"Invalid JSON:\n{ex.Message}", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                lblPlayerStatus.Text = "✗ Validation failed";
                lblPlayerStatus.ForeColor = System.Drawing.Color.Red;
                _logger.LogError(ex, "Player JSON validation failed");
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

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var teamData = JsonSerializer.Deserialize<TeamInfoList>(txtTeamJson.Text, options);

                if (teamData?.teamInfoList == null || !teamData.teamInfoList.Any())
                {
                    throw new Exception("No team data found or empty list");
                }

                lblTeamStatus.Text = $"✓ Valid - {teamData.teamInfoList.Count} teams";
                lblTeamStatus.ForeColor = System.Drawing.Color.Green;

                var totalKills = teamData.teamInfoList.Sum(t => t.killNum);
                var aliveTeams = teamData.teamInfoList.Count(t => t.liveMemberNum > 0);

                MessageBox.Show(
                    $"Team JSON is valid!\n\n" +
                    $"Teams found: {teamData.teamInfoList.Count}\n" +
                    $"Total kills: {totalKills}\n" +
                    $"Teams alive: {aliveTeams}",
                    "Validation Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                _logger.LogInformation("Team JSON validated: {Count} teams, {Kills} total kills",
                    teamData.teamInfoList.Count, totalKills);
            }
            catch (JsonException ex)
            {
                lblTeamStatus.Text = "✗ Invalid JSON";
                lblTeamStatus.ForeColor = System.Drawing.Color.Red;
                _logger.LogError(ex, "Invalid team JSON format");
                MessageBox.Show($"Invalid JSON:\n{ex.Message}", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                lblTeamStatus.Text = "✗ Validation failed";
                lblTeamStatus.ForeColor = System.Drawing.Color.Red;
                _logger.LogError(ex, "Team JSON validation failed");
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
                btnCreateBackup.Text = "Checking...";

                var match = await GetOrCreateMatchAsync(checkOnly: true);

                if (match != null)
                {
                    // Match exists - backup would be useful
                    MessageBox.Show(
                        "Match exists in database.\n\n" +
                        "Backup feature is currently disabled, but SQL backup files\n" +
                        "will be automatically created during the apply operation.",
                        "Backup Info",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        "This will be a new match entry.\n\n" +
                        "No backup needed.",
                        "New Match",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in backup check process");
                MessageBox.Show($"Error:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnCreateBackup.Enabled = true;
                btnCreateBackup.Text = "Create Backup (Optional)";
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

            // Validate JSON before proceeding
            LivePlayersList playerData;
            TeamInfoList teamData;

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                playerData = JsonSerializer.Deserialize<LivePlayersList>(txtPlayerJson.Text, options);
                teamData = JsonSerializer.Deserialize<TeamInfoList>(txtTeamJson.Text, options);

                if (playerData?.PlayerInfoList == null || !playerData.PlayerInfoList.Any())
                {
                    throw new Exception("Player data is empty or invalid");
                }

                if (teamData?.teamInfoList == null || !teamData.teamInfoList.Any())
                {
                    throw new Exception("Team data is empty or invalid");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Invalid JSON data:\n{ex.Message}", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Confirm operation
            var confirmResult = MessageBox.Show(
                "Apply data to database?\n\n" +
                $"Players: {playerData.PlayerInfoList.Count}\n" +
                $"Teams: {teamData.teamInfoList.Count}\n\n" +
                "SQL backup files will be created automatically.\n\n" +
                "Continue?",
                "Confirm Data Update",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1
            );

            if (confirmResult != DialogResult.Yes)
                return;

            try
            {
                btnApplyToDb.Enabled = false;
                btnApplyToDb.Text = "Applying...";
                this.Cursor = Cursors.WaitCursor;

                _logger.LogInformation("Starting manual data apply process");

                // Get or create match
                var match = await GetOrCreateMatchAsync(checkOnly: false);

                // Apply player data using PostMatch method
                _logger.LogInformation("Saving player info for Match {MatchId}, Day {Day}",
                    match.MatchId, match.MatchDayId);
                await _postMatch.savePlayersinfo(playerData, match,teamData);

                // Small delay to ensure player data is committed
                await Task.Delay(1000);

                // Apply team data using PostMatch method
                _logger.LogInformation("Saving team info for Match {MatchId}, Day {Day}",
                    match.MatchId, match.MatchDayId);
                await _postMatch.saveTeamsinfo(teamData, match, playerData);

                MessageBox.Show(
                    "Data applied successfully!\n\n" +
                    $"Players: {playerData.PlayerInfoList.Count}\n" +
                    $"Teams: {teamData.teamInfoList.Count}\n\n" +
                    "SQL backup files have been created in the backup directory.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                _logger.LogInformation(
                    "Manual data applied successfully: Tournament={Tournament}, Stage={Stage}, Match={MatchId}, Day={Day}",
                    cmbTournament.SelectedItem, cmbStage.SelectedItem, match.MatchId, match.MatchDayId
                );

                this.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying data to database");
                MessageBox.Show(
                    $"Error applying data:\n\n{ex.Message}\n\n" +
                    "Check the SQL backup files in the backup directory for manual recovery if needed.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                btnApplyToDb.Enabled = true;
                btnApplyToDb.Text = "Apply to Database";
                this.Cursor = Cursors.Default;
            }
        }

        private bool ValidateMatchSelection()
        {
            if (cmbTournament.SelectedItem == null)
            {
                MessageBox.Show("Please select a tournament!", "Missing Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (cmbStage.SelectedItem == null)
            {
                MessageBox.Show("Please select a stage!", "Missing Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (cmbDay.SelectedItem == null)
            {
                MessageBox.Show("Please select a day!", "Missing Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (cmbMatch.SelectedItem == null)
            {
                MessageBox.Show("Please select a match!", "Missing Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private async Task<Match> GetOrCreateMatchAsync(bool checkOnly = false)
        {
            var tournament = await _context.Tournaments
                .FirstOrDefaultAsync(x => x.Name == cmbTournament.SelectedItem.ToString());

            if (tournament == null)
                throw new Exception($"Tournament '{cmbTournament.SelectedItem}' not found in database!");

            var stage = await _context.Stages
                .FirstOrDefaultAsync(x => x.Name == cmbStage.SelectedItem.ToString() &&
                                         x.TournamentId == tournament.TournamentId);

            if (stage == null)
                throw new Exception($"Stage '{cmbStage.SelectedItem}' not found for tournament '{tournament.Name}'!");

            int matchDay = int.Parse(cmbDay.SelectedItem.ToString());
            int matchNumber = int.Parse(cmbMatch.SelectedItem.ToString());

            var match = await _context.Matches
                .FirstOrDefaultAsync(x =>
                    x.TournamentId == tournament.TournamentId &&
                    x.StageId == stage.StageId &&
                    x.MatchDayId == matchDay &&
                    x.MatchId == matchNumber);

            if (match == null)
            {
                if (checkOnly)
                {
                    // Just checking, don't create
                    return null;
                }

                // Create new match
                match = new Match
                {
                    TournamentId = tournament.TournamentId,
                    StageId = stage.StageId,
                    MatchDayId = matchDay,
                    MatchId = matchNumber,
                };

                _context.Matches.Add(match);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Created new match: Tournament={Tournament}, Stage={Stage}, Day={Day}, Match={Match}",
                    tournament.Name, stage.Name, matchDay, matchNumber
                );

                MessageBox.Show(
                    $"New match created!\n\n" +
                    $"Tournament: {tournament.Name}\n" +
                    $"Stage: {stage.Name}\n" +
                    $"Day: {matchDay}\n" +
                    $"Match: {matchNumber}",
                    "Match Created",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }

            return match;
        }
    }
}