
using Hangfire;
using Microsoft.Extensions.Logging;
using VmixGraphicsBusiness.Utils;
using System.Collections.Generic;
using VmixGraphicsBusiness;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;
using VmixGraphicsBusiness.vmixutils;
using Microsoft.Extensions.DependencyInjection;
using VmixData.Models;
using VmixGraphicsBusiness.LiveMatch;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using VmixGraphicsBusiness.PostMatchStats;
using System.Diagnostics;
using VmixGraphicsBusiness.PreMatch;

namespace Pubg_Ranking_System
{
    public partial class Form1 : Form
    {
        private readonly Add_tournament _Add_tournament;
        private readonly GetLiveData _getLiveData;
        private readonly TournamentBusiness _tournamentBusiness;
        private readonly LiveStatsBusiness _liveStatsBusiness;
        private readonly IBackgroundJobClient _backgroundJobManager;
        private readonly ILogger<Form1> _logger;
        private readonly IConnectionMultiplexer _redisConnection;
        private readonly IDatabase _redisDb;
        private readonly IServiceProvider _serviceProvider;
        private readonly VmixData.Models.vmix_graphicsContext _vmix_GraphicsContext;
        private readonly PostMatch _postMatch;
        private readonly PreMatch _preMatch;
        private readonly Reset _reset;

        // Animation and UI enhancement variables
        private Timer _animationTimer;
        private int _animationStep = 0;
        private bool _isAnimating = false;

        public Form1(Add_tournament add_Tournament, GetLiveData getLiveData, LiveStatsBusiness liveStatsBusiness, TournamentBusiness tournamentBusiness, IBackgroundJobClient backgroundJobManager, ILogger<Form1> logger, IConnectionMultiplexer redisConnection, IServiceProvider serviceProvider, vmix_graphicsContext vmix_GraphicsContext, PostMatch postMatch, Reset reset, PreMatch preMatch)
        {
            _liveStatsBusiness = liveStatsBusiness;
            _Add_tournament = add_Tournament;
            _getLiveData = getLiveData;
            InitializeComponent();
            SetupModernUI();
            
            // Set the form to full screen
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            
            _backgroundJobManager = backgroundJobManager;
            _logger = logger;
            _tournamentBusiness = tournamentBusiness;
            _redisConnection = redisConnection;
            _redisDb = _redisConnection.GetDatabase();

            var tournamentnames = _tournamentBusiness.getAll().Select(x => x.Name).ToList();
            Stage_cmb.DataSource = _tournamentBusiness.getAllStages().Select(x => x.Name).ToList();
            TournamentName_cmb.DataSource = tournamentnames;

            var days = new List<string> { "1", "2", "3", "4", "5", "6", "7", "8" };
            var matches = new List<string> { "1", "2", "3", "4", "5", "6", "7", "8" };
            var mapNames = new List<string> { "Erangel", "Miramar", "Sanhok" };

            MapName_cmb.DataSource = mapNames;
            Day_cmb.DataSource = days;
            Match_cmb.DataSource = matches;

            _serviceProvider = serviceProvider;
            _vmix_GraphicsContext = vmix_GraphicsContext;
            _postMatch = postMatch;
            _preMatch = preMatch;
            _reset = reset;

            // Initialize animation timer
            _animationTimer = new Timer();
            _animationTimer.Interval = 50;
            _animationTimer.Tick += AnimationTimer_Tick;
        }

        private void SetupModernUI()
        {
            // Set form properties for modern look
            this.BackColor = Color.FromArgb(25, 25, 25);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1200, 800);

            // Add rounded corners effect
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));

            // Enable double buffering for smooth animations
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
        }

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (_isAnimating)
            {
                _animationStep++;
                this.Invalidate();

                if (_animationStep >= 100)
                {
                    _animationStep = 0;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            DrawModernBackground(e.Graphics);
            DrawAnimatedElements(e.Graphics);
        }

        private void DrawModernBackground(Graphics g)
        {
            // Create gradient background
            using (var brush = new LinearGradientBrush(
                this.ClientRectangle,
                Color.FromArgb(45, 45, 48),
                Color.FromArgb(25, 25, 25),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(brush, this.ClientRectangle);
            }

            // Draw header area
            using (var headerBrush = new LinearGradientBrush(
                new Rectangle(0, 0, Width, 80),
                Color.FromArgb(0, 122, 204),
                Color.FromArgb(0, 102, 184),
                LinearGradientMode.Horizontal))
            {
                g.FillRectangle(headerBrush, 0, 0, Width, 80);
            }

            // Draw title
            using (var titleFont = new Font("Segoe UI", 24, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(Color.White))
            {
                var titleText = "PUBG Ranking System Dashboard";
                var titleSize = g.MeasureString(titleText, titleFont);
                g.DrawString(titleText, titleFont, titleBrush,
                    (Width - titleSize.Width) / 2, 25);
            }

            // Draw decorative elements
            DrawDecorativeElements(g);
        }

        private void DrawDecorativeElements(Graphics g)
        {
            // Draw animated circles
            using (var pen = new Pen(Color.FromArgb(100, 0, 122, 204), 2))
            {
                for (int i = 0; i < 5; i++)
                {
                    int size = 50 + i * 20;
                    int x = Width - 150 + (int)(Math.Sin((_animationStep + i * 20) * 0.1) * 20);
                    int y = 100 + i * 30 + (int)(Math.Cos((_animationStep + i * 20) * 0.1) * 10);
                    g.DrawEllipse(pen, x, y, size, size);
                }
            }
        }

        private void DrawAnimatedElements(Graphics g)
        {
            if (_isAnimating)
            {
                // Draw pulsing effect around active elements
                using (var pen = new Pen(Color.FromArgb(150, 0, 255, 127), 3))
                {
                    float pulseSize = 5 + (float)(Math.Sin(_animationStep * 0.2) * 3);
                    // Add pulsing effects to important buttons
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            _logger.LogInformation("Form is closing.");

            // Show modern confirmation dialog
            var result = ShowModernMessageBox("Are you sure you want to close the application?",
                "Confirm Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.No)
            {
                e.Cancel = true;
                return;
            }

            // Stop animation timer
            _animationTimer?.Stop();
            _animationTimer?.Dispose();

            string processName = "Pubg Ranking System";
            try
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    process.Kill();
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error terminating process {processName}: {ex.Message}");
            }
        }

        private DialogResult ShowModernMessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            // Create a modern-looking message box
            var form = new Form()
            {
                Width = 400,
                Height = 200,
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };

            var lblMessage = new Label()
            {
                Left = 20,
                Top = 20,
                Width = 360,
                Height = 80,
                Text = message,
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var btnYes = CreateModernButton("Yes", new Point(100, 120));
            btnYes.DialogResult = DialogResult.Yes;

            var btnNo = CreateModernButton("No", new Point(220, 120));
            btnNo.DialogResult = DialogResult.No;

            form.Controls.Add(lblMessage);
            form.Controls.Add(btnYes);
            form.Controls.Add(btnNo);

            return form.ShowDialog();
        }

        private Button CreateModernButton(string text, Point location)
        {
            var button = new Button()
            {
                Text = text,
                Location = location,
                Size = new Size(80, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 142, 224);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 102, 184);

            return button;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            // Start animations
            _isAnimating = true;
            _animationTimer.Start();

            // Show authentication form first
            //if (!await ShowAuthenticationAsync())
            //{
            //    Application.Exit();
            //    return;
            //}

            // Define the output folder path
            string outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "resources");

            // Load tournaments on form load
            LoadTournamentsAsync().ConfigureAwait(false);

            try
            {
                if (Directory.Exists(outputFolder))
                {
                    Directory.Delete(outputFolder, true);
                    _logger.LogInformation($"Deleted folder: {outputFolder}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error deleting folder: {ex.Message}");
            }

            // Sync keys with cloud on startup
            await SyncKeysWithCloudAsync();

            // Show welcome animation
            await ShowWelcomeAnimation();
        }

        private async Task ShowWelcomeAnimation()
        {
            // Fade in effect
            this.Opacity = 0;
            for (double opacity = 0; opacity <= 1; opacity += 0.05)
            {
                this.Opacity = opacity;
                await Task.Delay(20);
            }
        }

        private async Task SyncKeysWithCloudAsync()
        {
            try
            {
                var authKeyService = _serviceProvider.GetRequiredService<AuthKeyService>();
                await authKeyService.SyncKeysWithCloudAsync();
                _logger.LogInformation("Keys synced with cloud successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing keys with cloud");
            }
        }

        private async Task LoadTournamentsAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<vmix_graphicsContext>();
                var tournaments = await context.Tournaments.Select(t => t.Name).ToListAsync();
                TournamentName_cmb.DataSource = tournaments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tournaments");
            }
        }

        // Event handlers for buttons with enhanced UI feedback
        private async void Add_Tournament_btn_Click(object sender, EventArgs e)
        {
            await AnimateButtonClick((Button)sender);
            _Add_tournament.Show();
        }

        private async void start_btn_Click(object sender, EventArgs e)
        {
            await AnimateButtonClick((Button)sender);

            var result = await _tournamentBusiness.add_match(TournamentName_cmb.Text, Stage_cmb.Text, Day_cmb.Text, Match_cmb.Text);
            if (result.Item2 == 0)
            {
                _getLiveData.FetchAndPostData(result.Item3);
                _logger.LogInformation("Recurring job started for match {MatchId}.", result.Item3.MatchId);
                ShowSuccessNotification("Match started successfully!");
            }
            else
            {
                var confirmResult = ShowModernMessageBox(result.Item1, "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirmResult == DialogResult.Yes)
                {
                    _getLiveData.FetchAndPostData(result.Item3);
                    _logger.LogInformation("Recurring job started for match {MatchId}.", result.Item3.MatchId);
                    ShowSuccessNotification("Match started successfully!");
                }
            }
        }

        private async void reload_teams_btn_Click(object sender, EventArgs e)
        {
            await AnimateButtonClick((Button)sender);

            try
            {
                var jsonTeamService = _serviceProvider.GetRequiredService<JsonTeamDataService>();
                await jsonTeamService.LoadTeamDataAsync();
                ShowSuccessNotification("Teams data reloaded successfully!");
                await LoadTournamentsAsync();
            }
            catch (Exception ex)
            {
                ShowErrorNotification($"Error reloading teams data: {ex.Message}");
                _logger.LogError(ex, "Error reloading teams data");
            }
        }

        private async void stop_Click(object sender, EventArgs e)
        {
            await AnimateButtonClick((Button)sender);

            _backgroundJobManager.Delete(HangfireJobNames.FetchAndPostDataJob);
            ShowSuccessNotification("Match stopped successfully!");
            _logger.LogInformation("Recurring job stopped.");

            // Remove Redis keys
            var redisKeys = new List<string>
            {
                $"{HelperRedis.VehicleEliminationsKey}:*",
                $"{HelperRedis.GrenadeEliminationsKey}:*",
                $"{HelperRedis.AirDropLootedKey}:*",
                $"{HelperRedis.PlayerInfolist}",
                $"{HelperRedis.TeamInfoList}",
                $"isEliminated:rank",
                $"{HelperRedis.isEliminated}:*",
                HelperRedis.FirstBloodKey
            };

            foreach (var keyPattern in redisKeys)
            {
                var server = _redisConnection.GetServer(_redisConnection.GetEndPoints().First());
                var keys = server.Keys(pattern: keyPattern).ToArray();
                if (keys.Any())
                {
                    await _redisDb.KeyDeleteAsync(keys);
                }
            }
        }

        // Post-match button handlers with animations
        private async void TeamsToWatch_Click(object sender, EventArgs e)
        {
            await AnimateButtonClick((Button)sender);
            var match = await GetSelectedMatch();
            if (match != null)
            {
                _postMatch.TeamsToWatch(match);
                ShowSuccessNotification("Teams to Watch updated!");
            }
        }

        private async void MatchRankings_Click(object sender, EventArgs e)
        {
            await AnimateButtonClick((Button)sender);
            var match = await GetSelectedMatch();
            if (match != null)
            {
                _postMatch.MatchRankings(match);
                ShowSuccessNotification("Match Rankings updated!");
            }
        }

        private async void OverallRankings_Click(object sender, EventArgs e)
        {
            await AnimateButtonClick((Button)sender);
            var match = await GetSelectedMatch();
            if (match != null)
            {
                _postMatch.OverallRankings(match);
                ShowSuccessNotification("Overall Rankings updated!");
            }
        }

        private async void MatchMvp_Click(object sender, EventArgs e)
        {
            await AnimateButtonClick((Button)sender);
            var match = await GetSelectedMatch();
            if (match != null)
            {
                _postMatch.MatchMvp(match);
                ShowSuccessNotification("Match MVP updated!");
            }
        }

        private async void SetAll_Click(object sender, EventArgs e)
        {
            await AnimateButtonClick((Button)sender);
            var match = await GetSelectedMatch();
            if (match != null)
            {
                _postMatch.WWCDStatsAsync(match);
                _postMatch.MatchMvp(match);
                _postMatch.MatchRankings(match);
                _postMatch.OverallRankings(match);
                _postMatch.MatchSummary(match);
                _postMatch.Top5MatchMVP(match);
                _postMatch.Top5StageMVP(match);
                _postMatch.TopGrenadiers(match);
                ShowSuccessNotification("All statistics updated!");
            }
        }

        private async void Reset_Click(object sender, EventArgs e)
        {
            await AnimateButtonClick((Button)sender);
            _reset.ResetAll();
            ShowSuccessNotification("System reset completed!");
        }

        private async void MapTopPerformers_Click(object sender, EventArgs e)
        {
            await AnimateButtonClick((Button)sender);
            var match = await GetSelectedMatch();
            if (match != null)
            {
                _preMatch.MapTopPerformers(match, MapName_cmb.Text);
                ShowSuccessNotification("Map Top Performers updated!");
            }
        }

        private async Task<Match> GetSelectedMatch()
        {
            try
            {
                var tournament = _vmix_GraphicsContext.Tournaments.Where(x => x.Name == TournamentName_cmb.Text).FirstOrDefault();
                var stage = _vmix_GraphicsContext.Stages.Where(x => x.Name == Stage_cmb.Text).FirstOrDefault();

                if (tournament == null || stage == null)
                {
                    ShowErrorNotification("Please select valid tournament and stage!");
                    return null;
                }

                var match = await _vmix_GraphicsContext.Matches
                    .Where(x => x.TournamentId == tournament.TournamentId &&
                               x.StageId == stage.StageId &&
                               x.MatchDayId == int.Parse(Day_cmb.Text) &&
                               x.MatchId == int.Parse(Match_cmb.Text))
                    .FirstOrDefaultAsync();

                if (match == null)
                {
                    ShowErrorNotification("Match not found!");
                }

                return match;
            }
            catch (Exception ex)
            {
                ShowErrorNotification($"Error getting match: {ex.Message}");
                return null;
            }
        }

        private async Task AnimateButtonClick(Button button)
        {
            var originalColor = button.BackColor;
            var originalSize = button.Size;

            // Scale down effect
            button.Size = new Size(originalSize.Width - 5, originalSize.Height - 5);
            button.BackColor = Color.FromArgb(0, 142, 224);
            await Task.Delay(100);

            // Scale back up
            button.Size = originalSize;
            button.BackColor = originalColor;
        }

        private void ShowSuccessNotification(string message)
        {
            ShowNotification(message, Color.FromArgb(0, 150, 0));
        }

        private void ShowErrorNotification(string message)
        {
            ShowNotification(message, Color.FromArgb(200, 0, 0));
        }

        private void ShowNotification(string message, Color backgroundColor)
        {
            var notification = new Form()
            {
                Width = 300,
                Height = 80,
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                BackColor = backgroundColor,
                ForeColor = Color.White,
                TopMost = true,
                ShowInTaskbar = false
            };

            notification.Location = new Point(
                this.Location.X + this.Width - notification.Width - 20,
                this.Location.Y + 100
            );

            var label = new Label()
            {
                Text = message,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White
            };

            notification.Controls.Add(label);
            notification.Show();

            // Auto-close after 3 seconds
            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 3000;
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                notification.Close();
            };
            timer.Start();
        }

        public static void EnqueueFetchAndPostDataJob(Match match, IBackgroundJobClient recurringJobManager, IServiceProvider serviceProvider)
        {
            var getLiveData = serviceProvider.GetRequiredService<GetLiveData>();
            recurringJobManager.Enqueue(HangfireQueues.HighPriority,
                () => getLiveData.FetchAndPostData(match));
        }
    }
}
