
namespace Pubg_Ranking_System
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            
            // Header Panel
            headerPanel = new Panel();
            titleLabel = new Label();
            closeButton = new Button();
            minimizeButton = new Button();
            
            // Main Content Panel
            mainPanel = new Panel();
            
            // Left Panel - Tournament Selection
            leftPanel = new Panel();
            tournamentGroupBox = new GroupBox();
            TournamentName_cmb = new ComboBox();
            Stage_cmb = new ComboBox();
            Day_cmb = new ComboBox();
            Match_cmb = new ComboBox();
            MapName_cmb = new ComboBox();
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            label4 = new Label();
            label5 = new Label();
            
            // Center Panel - Match Controls
            centerPanel = new Panel();
            matchControlsGroupBox = new GroupBox();
            start_btn = new Button();
            stop_btn = new Button();
            reload_teams_btn = new Button();
            Add_Tournament_btn = new Button();
            
            // Right Panel - Post Match Stats
            rightPanel = new Panel();
            postMatchGroupBox = new GroupBox();
            teamsToWatch_btn = new Button();
            matchRankings_btn = new Button();
            overallRankings_btn = new Button();
            matchMvp_btn = new Button();
            setAll_btn = new Button();
            reset_btn = new Button();
            
            // Bottom Panel - Pre Match
            bottomPanel = new Panel();
            preMatchGroupBox = new GroupBox();
            mapTopPerformers_btn = new Button();
            
            SuspendLayout();
            
            // Form
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1200, 800);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(25, 25, 25);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10F);
            this.Icon = (Icon)resources.GetObject("$this.Icon");
            this.Name = "Form1";
            this.Text = "PUBG Ranking System Dashboard";
            this.Load += Form1_Load;
            
            // Header Panel
            headerPanel.BackColor = Color.FromArgb(0, 122, 204);
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Height = 80;
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(closeButton);
            headerPanel.Controls.Add(minimizeButton);
            
            // Title Label
            titleLabel.Text = "PUBG RANKING SYSTEM";
            titleLabel.Font = new Font("Segoe UI", 18, FontStyle.Bold);
            titleLabel.ForeColor = Color.White;
            titleLabel.AutoSize = false;
            titleLabel.Size = new Size(400, 40);
            titleLabel.Location = new Point(50, 20);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            
            // Close Button
            closeButton.Text = "✕";
            closeButton.Size = new Size(40, 30);
            closeButton.Location = new Point(1150, 25);
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.BackColor = Color.Transparent;
            closeButton.ForeColor = Color.White;
            closeButton.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            closeButton.Cursor = Cursors.Hand;
            closeButton.Click += (s, e) => this.Close();
            
            // Minimize Button
            minimizeButton.Text = "–";
            minimizeButton.Size = new Size(40, 30);
            minimizeButton.Location = new Point(1100, 25);
            minimizeButton.FlatStyle = FlatStyle.Flat;
            minimizeButton.FlatAppearance.BorderSize = 0;
            minimizeButton.BackColor = Color.Transparent;
            minimizeButton.ForeColor = Color.White;
            minimizeButton.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            minimizeButton.Cursor = Cursors.Hand;
            minimizeButton.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            
            // Main Panel
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.BackColor = Color.Transparent;
            mainPanel.Padding = new Padding(20);
            
            // Left Panel
            leftPanel.Size = new Size(300, 500);
            leftPanel.Location = new Point(20, 20);
            leftPanel.BackColor = Color.FromArgb(45, 45, 48);
            leftPanel.Controls.Add(tournamentGroupBox);
            
            // Tournament GroupBox
            tournamentGroupBox.Text = "Tournament Selection";
            tournamentGroupBox.ForeColor = Color.White;
            tournamentGroupBox.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            tournamentGroupBox.Dock = DockStyle.Fill;
            tournamentGroupBox.Padding = new Padding(15);
            
            // Tournament ComboBox
            TournamentName_cmb.Size = new Size(250, 30);
            TournamentName_cmb.Location = new Point(15, 50);
            TournamentName_cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            TournamentName_cmb.BackColor = Color.FromArgb(60, 60, 63);
            TournamentName_cmb.ForeColor = Color.White;
            TournamentName_cmb.FlatStyle = FlatStyle.Flat;
            
            // Stage ComboBox
            Stage_cmb.Size = new Size(250, 30);
            Stage_cmb.Location = new Point(15, 110);
            Stage_cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            Stage_cmb.BackColor = Color.FromArgb(60, 60, 63);
            Stage_cmb.ForeColor = Color.White;
            Stage_cmb.FlatStyle = FlatStyle.Flat;
            
            // Day ComboBox
            Day_cmb.Size = new Size(250, 30);
            Day_cmb.Location = new Point(15, 170);
            Day_cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            Day_cmb.BackColor = Color.FromArgb(60, 60, 63);
            Day_cmb.ForeColor = Color.White;
            Day_cmb.FlatStyle = FlatStyle.Flat;
            
            // Match ComboBox
            Match_cmb.Size = new Size(250, 30);
            Match_cmb.Location = new Point(15, 230);
            Match_cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            Match_cmb.BackColor = Color.FromArgb(60, 60, 63);
            Match_cmb.ForeColor = Color.White;
            Match_cmb.FlatStyle = FlatStyle.Flat;
            
            // Map ComboBox
            MapName_cmb.Size = new Size(250, 30);
            MapName_cmb.Location = new Point(15, 290);
            MapName_cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            MapName_cmb.BackColor = Color.FromArgb(60, 60, 63);
            MapName_cmb.ForeColor = Color.White;
            MapName_cmb.FlatStyle = FlatStyle.Flat;
            
            // Labels
            label1.Text = "Tournament";
            label1.Location = new Point(15, 30);
            label1.Size = new Size(100, 20);
            label1.ForeColor = Color.LightGray;
            
            label2.Text = "Stage";
            label2.Location = new Point(15, 90);
            label2.Size = new Size(100, 20);
            label2.ForeColor = Color.LightGray;
            
            label3.Text = "Day";
            label3.Location = new Point(15, 150);
            label3.Size = new Size(100, 20);
            label3.ForeColor = Color.LightGray;
            
            label4.Text = "Match";
            label4.Location = new Point(15, 210);
            label4.Size = new Size(100, 20);
            label4.ForeColor = Color.LightGray;
            
            label5.Text = "Map";
            label5.Location = new Point(15, 270);
            label5.Size = new Size(100, 20);
            label5.ForeColor = Color.LightGray;
            
            // Add controls to tournament group box
            tournamentGroupBox.Controls.AddRange(new Control[] {
                TournamentName_cmb, Stage_cmb, Day_cmb, Match_cmb, MapName_cmb,
                label1, label2, label3, label4, label5
            });
            
            // Center Panel
            centerPanel.Size = new Size(300, 500);
            centerPanel.Location = new Point(340, 20);
            centerPanel.BackColor = Color.FromArgb(45, 45, 48);
            centerPanel.Controls.Add(matchControlsGroupBox);
            
            // Match Controls GroupBox
            matchControlsGroupBox.Text = "Match Controls";
            matchControlsGroupBox.ForeColor = Color.White;
            matchControlsGroupBox.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            matchControlsGroupBox.Dock = DockStyle.Fill;
            matchControlsGroupBox.Padding = new Padding(15);
            
            // Buttons styling method
            void StyleButton(Button btn, Color color, Point location)
            {
                btn.Size = new Size(250, 45);
                btn.Location = location;
                btn.FlatStyle = FlatStyle.Flat;
                btn.BackColor = color;
                btn.ForeColor = Color.White;
                btn.Font = new Font("Segoe UI", 11, FontStyle.Bold);
                btn.FlatAppearance.BorderSize = 0;
                btn.Cursor = Cursors.Hand;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(
                    Math.Min(255, color.R + 30),
                    Math.Min(255, color.G + 30),
                    Math.Min(255, color.B + 30));
            }
            
            // Style buttons
            StyleButton(Add_Tournament_btn, Color.FromArgb(0, 150, 136), new Point(15, 40));
            Add_Tournament_btn.Text = "➕ Add Tournament";
            Add_Tournament_btn.Click += Add_Tournament_btn_Click;
            
            StyleButton(start_btn, Color.FromArgb(76, 175, 80), new Point(15, 100));
            start_btn.Text = "▶️ Start Match";
            start_btn.Click += start_btn_Click;
            
            StyleButton(stop_btn, Color.FromArgb(244, 67, 54), new Point(15, 160));
            stop_btn.Text = "⏹️ Stop Match";
            stop_btn.Click += stop_Click;
            
            StyleButton(reload_teams_btn, Color.FromArgb(255, 152, 0), new Point(15, 220));
            reload_teams_btn.Text = "🔄 Reload Teams";
            reload_teams_btn.Click += reload_teams_btn_Click;
            
            matchControlsGroupBox.Controls.AddRange(new Control[] {
                Add_Tournament_btn, start_btn, stop_btn, reload_teams_btn
            });
            
            // Right Panel
            rightPanel.Size = new Size(300, 500);
            rightPanel.Location = new Point(660, 20);
            rightPanel.BackColor = Color.FromArgb(45, 45, 48);
            rightPanel.Controls.Add(postMatchGroupBox);
            
            // Post Match GroupBox
            postMatchGroupBox.Text = "Post Match Statistics";
            postMatchGroupBox.ForeColor = Color.White;
            postMatchGroupBox.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            postMatchGroupBox.Dock = DockStyle.Fill;
            postMatchGroupBox.Padding = new Padding(15);
            
            // Post match buttons
            StyleButton(teamsToWatch_btn, Color.FromArgb(63, 81, 181), new Point(15, 40));
            teamsToWatch_btn.Text = "👥 Teams to Watch";
            teamsToWatch_btn.Click += TeamsToWatch_Click;
            
            StyleButton(matchRankings_btn, Color.FromArgb(103, 58, 183), new Point(15, 95));
            matchRankings_btn.Text = "📊 Match Rankings";
            matchRankings_btn.Click += MatchRankings_Click;
            
            StyleButton(overallRankings_btn, Color.FromArgb(156, 39, 176), new Point(15, 150));
            overallRankings_btn.Text = "🏆 Overall Rankings";
            overallRankings_btn.Click += OverallRankings_Click;
            
            StyleButton(matchMvp_btn, Color.FromArgb(233, 30, 99), new Point(15, 205));
            matchMvp_btn.Text = "⭐ Match MVP";
            matchMvp_btn.Click += MatchMvp_Click;
            
            StyleButton(setAll_btn, Color.FromArgb(0, 150, 136), new Point(15, 260));
            setAll_btn.Text = "📋 Set All Stats";
            setAll_btn.Click += SetAll_Click;
            
            StyleButton(reset_btn, Color.FromArgb(96, 125, 139), new Point(15, 315));
            reset_btn.Text = "🔄 Reset All";
            reset_btn.Click += Reset_Click;
            
            postMatchGroupBox.Controls.AddRange(new Control[] {
                teamsToWatch_btn, matchRankings_btn, overallRankings_btn,
                matchMvp_btn, setAll_btn, reset_btn
            });
            
            // Bottom Panel
            bottomPanel.Size = new Size(1160, 180);
            bottomPanel.Location = new Point(20, 540);
            bottomPanel.BackColor = Color.FromArgb(45, 45, 48);
            bottomPanel.Controls.Add(preMatchGroupBox);
            
            // Pre Match GroupBox
            preMatchGroupBox.Text = "Pre Match Analysis";
            preMatchGroupBox.ForeColor = Color.White;
            preMatchGroupBox.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            preMatchGroupBox.Dock = DockStyle.Fill;
            preMatchGroupBox.Padding = new Padding(15);
            
            StyleButton(mapTopPerformers_btn, Color.FromArgb(121, 85, 72), new Point(15, 40));
            mapTopPerformers_btn.Text = "🗺️ Map Top Performers";
            mapTopPerformers_btn.Click += MapTopPerformers_Click;
            mapTopPerformers_btn.Size = new Size(300, 45);
            
            preMatchGroupBox.Controls.Add(mapTopPerformers_btn);
            
            // Add panels to main panel
            mainPanel.Controls.AddRange(new Control[] {
                leftPanel, centerPanel, rightPanel, bottomPanel
            });
            
            // Add main controls to form
            this.Controls.AddRange(new Control[] {
                headerPanel, mainPanel
            });
            
            ResumeLayout(false);
        }

        #endregion

        private Panel headerPanel;
        private Label titleLabel;
        private Button closeButton;
        private Button minimizeButton;
        private Panel mainPanel;
        private Panel leftPanel;
        private Panel centerPanel;
        private Panel rightPanel;
        private Panel bottomPanel;
        
        private GroupBox tournamentGroupBox;
        private GroupBox matchControlsGroupBox;
        private GroupBox postMatchGroupBox;
        private GroupBox preMatchGroupBox;
        
        private Button Add_Tournament_btn;
        private Button start_btn;
        private Button stop_btn;
        private Button reload_teams_btn;
        
        private ComboBox TournamentName_cmb;
        private ComboBox Stage_cmb;
        private ComboBox Day_cmb;
        private ComboBox Match_cmb;
        private ComboBox MapName_cmb;
        
        private Label label1;
        private Label label2;
        private Label label3;
        private Label label4;
        private Label label5;
        
        private Button teamsToWatch_btn;
        private Button matchRankings_btn;
        private Button overallRankings_btn;
        private Button matchMvp_btn;
        private Button setAll_btn;
        private Button reset_btn;
        private Button mapTopPerformers_btn;
    }
}
