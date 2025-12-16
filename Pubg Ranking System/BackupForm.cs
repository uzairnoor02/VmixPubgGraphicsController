using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VmixData.Models;

namespace Pubg_Ranking_System
{
    public class BackupForm : Form
    {
        private readonly vmix_graphicsContext _context;
        private readonly ILogger<BackupForm> _logger;
        private readonly TournamentDataBackupService _backupService;

        private ComboBox cmbTournament;
        private ComboBox cmbStage;
        private CheckBox chkFullBackup;
        private Button btnCreateBackup;
        private Button btnOpenBackupFolder;
        private Label lblStatus;
        private GroupBox grpOptions;
        private TextBox txtBackupPath;

        public BackupForm(
            vmix_graphicsContext context,
            ILogger<BackupForm> logger,
            TournamentDataBackupService backupService)
        {
            _context = context;
            _logger = logger;
            _backupService = backupService;

            InitializeUI();
            LoadData();
        }

        private void InitializeUI()
        {
            this.Text = "Database Backup";
            this.Size = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            grpOptions = new GroupBox
            {
                Text = "Backup Options",
                Location = new Point(10, 10),
                Size = new Size(560, 250)
            };

            chkFullBackup = new CheckBox
            {
                Text = "Full Database Backup (All Tournaments)",
                Location = new Point(15, 25),
                Size = new Size(300, 25),
                Checked = true
            };
            chkFullBackup.CheckedChanged += ChkFullBackup_CheckedChanged;

            Label lblTournament = new Label
            {
                Text = "Tournament:",
                Location = new Point(15, 60),
                Size = new Size(80, 20),
                Enabled = false
            };

            cmbTournament = new ComboBox
            {
                Location = new Point(110, 57),
                Size = new Size(200, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            cmbTournament.SelectedIndexChanged += (s, e) => LoadStages();

            Label lblStage = new Label
            {
                Text = "Stage:",
                Location = new Point(15, 95),
                Size = new Size(80, 20),
                Enabled = false
            };

            cmbStage = new ComboBox
            {
                Location = new Point(110, 92),
                Size = new Size(200, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };

            Label lblPath = new Label
            {
                Text = "Backup will be saved to:",
                Location = new Point(15, 130),
                Size = new Size(530, 20)
            };

            txtBackupPath = new TextBox
            {
                Location = new Point(15, 155),
                Size = new Size(530, 25),
                ReadOnly = true,
                BackColor = Color.White,
                Text = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "PubgBackups", "TournamentBackups")
            };

            btnCreateBackup = new Button
            {
                Text = "Create Backup",
                Location = new Point(15, 195),
                Size = new Size(150, 35),
                BackColor = Color.LightGreen
            };
            btnCreateBackup.Click += BtnCreateBackup_Click;

            btnOpenBackupFolder = new Button
            {
                Text = "Open Backup Folder",
                Location = new Point(175, 195),
                Size = new Size(150, 35)
            };
            btnOpenBackupFolder.Click += BtnOpenBackupFolder_Click;

            lblStatus = new Label
            {
                Location = new Point(15, 280),
                Size = new Size(560, 60),
                ForeColor = Color.Gray,
                Text = "Ready to create backup..."
            };

            grpOptions.Controls.AddRange(new Control[] {
                chkFullBackup, lblTournament, cmbTournament,
                lblStage, cmbStage, lblPath, txtBackupPath,
                btnCreateBackup, btnOpenBackupFolder
            });

            this.Controls.AddRange(new Control[] { grpOptions, lblStatus });
        }

        private async void LoadData()
        {
            try
            {
                var tournaments = await _context.Tournaments
                    .Select(x => new { x.TournamentId, x.Name })
                    .ToListAsync();

                cmbTournament.DisplayMember = "Name";
                cmbTournament.ValueMember = "TournamentId";
                cmbTournament.DataSource = tournaments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tournaments");
            }
        }

        private async void LoadStages()
        {
            if (cmbTournament.SelectedValue == null) return;

            try
            {
                int tournamentId = (int)cmbTournament.SelectedValue;
                var stages = await _context.Stages
                    .Where(x => x.TournamentId == tournamentId)
                    .Select(x => new { x.StageId, x.Name })
                    .ToListAsync();

                cmbStage.DisplayMember = "Name";
                cmbStage.ValueMember = "StageId";
                cmbStage.DataSource = stages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading stages");
            }
        }

        private void ChkFullBackup_CheckedChanged(object sender, EventArgs e)
        {
            bool isSpecificBackup = !chkFullBackup.Checked;

            cmbTournament.Enabled = isSpecificBackup;
            cmbStage.Enabled = isSpecificBackup;

            foreach (Control ctrl in grpOptions.Controls)
            {
                if (ctrl is Label lbl &&
                    (lbl.Text.Contains("Tournament") || lbl.Text.Contains("Stage")))
                {
                    lbl.Enabled = isSpecificBackup;
                }
            }
        }

        private async void BtnCreateBackup_Click(object sender, EventArgs e)
        {
            try
            {
                btnCreateBackup.Enabled = false;
                btnCreateBackup.Text = "Creating backup...";
                lblStatus.Text = "Backup in progress... Please wait...";
                lblStatus.ForeColor = Color.Blue;

                int? tournamentId = null;
                int? stageId = null;

                if (!chkFullBackup.Checked)
                {
                    if (cmbTournament.SelectedValue != null)
                        tournamentId = (int)cmbTournament.SelectedValue;

                    if (cmbStage.SelectedValue != null)
                        stageId = (int)cmbStage.SelectedValue;
                }

                string backupPath = await _backupService.CreateFullDatabaseBackupAsync(tournamentId, stageId);

                lblStatus.Text = $"✓ Backup created successfully!\n{backupPath}";
                lblStatus.ForeColor = Color.Green;

                var result = MessageBox.Show(
                    $"Backup created successfully!\n\n{backupPath}\n\nOpen backup folder?",
                    "Success",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information
                );

                if (result == DialogResult.Yes)
                {
                    Process.Start("explorer.exe", System.IO.Path.GetDirectoryName(backupPath));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating backup");
                lblStatus.Text = $"✗ Error: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"Error:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnCreateBackup.Enabled = true;
                btnCreateBackup.Text = "Create Backup";
            }
        }

        private void BtnOpenBackupFolder_Click(object sender, EventArgs e)
        {
            try
            {
                string folderPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "PubgBackups", "TournamentBackups");

                if (!System.IO.Directory.Exists(folderPath))
                    System.IO.Directory.CreateDirectory(folderPath);

                Process.Start("explorer.exe", folderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening folder:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}