// ManualDataInputForm.Designer.cs (Updated)

using System.Drawing;
using System.Windows.Forms;

namespace Pubg_Ranking_System
{
    partial class ManualDataInputForm
    {
        private System.ComponentModel.IContainer components = null;

        private ComboBox cmbTournament;
        private ComboBox cmbStage;
        private ComboBox cmbDay;
        private ComboBox cmbMatch;
        private RichTextBox txtPlayerJson;  // Changed to RichTextBox
        private RichTextBox txtTeamJson;    // Changed to RichTextBox
        private Button btnLoadPlayerFile;
        private Button btnLoadTeamFile;
        private Button btnValidatePlayer;
        private Button btnValidateTeam;
        private Button btnCreateBackup;
        private Button btnApplyToDb;
        private Label lblPlayerStatus;
        private Label lblTeamStatus;
        private GroupBox grpMatchSelection;
        private GroupBox grpPlayerData;
        private GroupBox grpTeamData;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            this.cmbTournament = new ComboBox();
            this.cmbStage = new ComboBox();
            this.cmbDay = new ComboBox();
            this.cmbMatch = new ComboBox();
            this.txtPlayerJson = new RichTextBox();  // Changed
            this.txtTeamJson = new RichTextBox();    // Changed
            this.btnLoadPlayerFile = new Button();
            this.btnLoadTeamFile = new Button();
            this.btnValidatePlayer = new Button();
            this.btnValidateTeam = new Button();
            this.btnCreateBackup = new Button();
            this.btnApplyToDb = new Button();
            this.lblPlayerStatus = new Label();
            this.lblTeamStatus = new Label();
            this.grpMatchSelection = new GroupBox();
            this.grpPlayerData = new GroupBox();
            this.grpTeamData = new GroupBox();

            this.SuspendLayout();

            // Form
            this.Text = "Manual Match Data Input";
            this.Size = new Size(900, 700);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Match Selection Group
            this.grpMatchSelection.Text = "Match Selection";
            this.grpMatchSelection.Location = new Point(10, 10);
            this.grpMatchSelection.Size = new Size(860, 80);

            Label lblTournament = new Label();
            lblTournament.Text = "Tournament:";
            lblTournament.Location = new Point(10, 25);
            lblTournament.Size = new Size(80, 20);

            this.cmbTournament.Location = new Point(100, 22);
            this.cmbTournament.Size = new Size(150, 25);
            this.cmbTournament.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbTournament.SelectedIndexChanged += (s, e) => LoadStages();

            Label lblStage = new Label();
            lblStage.Text = "Stage:";
            lblStage.Location = new Point(270, 25);
            lblStage.Size = new Size(50, 20);

            this.cmbStage.Location = new Point(330, 22);
            this.cmbStage.Size = new Size(150, 25);
            this.cmbStage.DropDownStyle = ComboBoxStyle.DropDownList;

            Label lblDay = new Label();
            lblDay.Text = "Day:";
            lblDay.Location = new Point(500, 25);
            lblDay.Size = new Size(40, 20);

            this.cmbDay.Location = new Point(550, 22);
            this.cmbDay.Size = new Size(80, 25);
            this.cmbDay.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbDay.Items.AddRange(new object[] { "1", "2", "3", "4", "5", "6", "7", "8" });

            Label lblMatch = new Label();
            lblMatch.Text = "Match:";
            lblMatch.Location = new Point(650, 25);
            lblMatch.Size = new Size(50, 20);

            this.cmbMatch.Location = new Point(710, 22);
            this.cmbMatch.Size = new Size(80, 25);
            this.cmbMatch.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbMatch.Items.AddRange(new object[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" });

            this.grpMatchSelection.Controls.AddRange(new Control[] {
                lblTournament, this.cmbTournament, lblStage, this.cmbStage,
                lblDay, this.cmbDay, lblMatch, this.cmbMatch
            });

            // Player Data Group
            this.grpPlayerData.Text = "Player Data JSON";
            this.grpPlayerData.Location = new Point(10, 100);
            this.grpPlayerData.Size = new Size(860, 250);

            this.txtPlayerJson.Location = new Point(10, 20);
            this.txtPlayerJson.Size = new Size(840, 180);
            this.txtPlayerJson.ScrollBars = RichTextBoxScrollBars.Both;
            this.txtPlayerJson.Font = new Font("Consolas", 9F);
            this.txtPlayerJson.WordWrap = true;

            this.btnLoadPlayerFile.Text = "Load File";
            this.btnLoadPlayerFile.Location = new Point(10, 210);
            this.btnLoadPlayerFile.Size = new Size(120, 30);
            this.btnLoadPlayerFile.Click += BtnLoadPlayerFile_Click;

            this.btnValidatePlayer.Text = "Validate";
            this.btnValidatePlayer.Location = new Point(140, 210);
            this.btnValidatePlayer.Size = new Size(120, 30);
            this.btnValidatePlayer.Click += BtnValidatePlayer_Click;

            this.lblPlayerStatus.Location = new Point(270, 215);
            this.lblPlayerStatus.Size = new Size(580, 20);
            this.lblPlayerStatus.ForeColor = Color.Gray;
            this.lblPlayerStatus.Text = "No data loaded";

            this.grpPlayerData.Controls.AddRange(new Control[] {
                this.txtPlayerJson, this.btnLoadPlayerFile,
                this.btnValidatePlayer, this.lblPlayerStatus
            });

            // Team Data Group
            this.grpTeamData.Text = "Team Data JSON";
            this.grpTeamData.Location = new Point(10, 360);
            this.grpTeamData.Size = new Size(860, 250);

            this.txtTeamJson.Location = new Point(10, 20);
            this.txtTeamJson.Size = new Size(840, 180);
            this.txtTeamJson.ScrollBars = RichTextBoxScrollBars.Both;
            this.txtTeamJson.Font = new Font("Consolas", 9F);
            this.txtTeamJson.WordWrap = true;

            this.btnLoadTeamFile.Text = "Load File";
            this.btnLoadTeamFile.Location = new Point(10, 210);
            this.btnLoadTeamFile.Size = new Size(120, 30);
            this.btnLoadTeamFile.Click += BtnLoadTeamFile_Click;

            this.btnValidateTeam.Text = "Validate";
            this.btnValidateTeam.Location = new Point(140, 210);
            this.btnValidateTeam.Size = new Size(120, 30);
            this.btnValidateTeam.Click += BtnValidateTeam_Click;

            this.lblTeamStatus.Location = new Point(270, 215);
            this.lblTeamStatus.Size = new Size(580, 20);
            this.lblTeamStatus.ForeColor = Color.Gray;
            this.lblTeamStatus.Text = "No data loaded";

            this.grpTeamData.Controls.AddRange(new Control[] {
                this.txtTeamJson, this.btnLoadTeamFile,
                this.btnValidateTeam, this.lblTeamStatus
            });

            // Action Buttons
            this.btnCreateBackup.Text = "Create Backup (Optional)";
            this.btnCreateBackup.Location = new Point(10, 620);
            this.btnCreateBackup.Size = new Size(200, 35);
            this.btnCreateBackup.BackColor = Color.LightBlue;
            this.btnCreateBackup.Click += BtnCreateBackup_Click;

            this.btnApplyToDb.Text = "Apply to Database";
            this.btnApplyToDb.Location = new Point(220, 620);
            this.btnApplyToDb.Size = new Size(200, 35);
            this.btnApplyToDb.BackColor = Color.LightGreen;
            this.btnApplyToDb.Click += BtnApplyToDb_Click;

            // Add controls to form
            this.Controls.AddRange(new Control[] {
                this.grpMatchSelection, this.grpPlayerData, this.grpTeamData,
                this.btnCreateBackup, this.btnApplyToDb
            });

            this.ResumeLayout(false);
        }
    }
}