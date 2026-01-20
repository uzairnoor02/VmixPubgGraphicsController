using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using VmixData.Models;
using VmixGraphicsBusiness;
using VmixGraphicsBusiness.LiveMatch;
using VmixGraphicsBusiness.PostMatchStats;
using VmixGraphicsBusiness.PreMatch;
using VmixGraphicsBusiness.Utils;
using VmixGraphicsBusiness.vmixutils;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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
        private ISubscriber _subscriber;
        private ApiCallProcessor ApiCallProcessor;

        public Form1(Add_tournament add_Tournament, GetLiveData getLiveData, LiveStatsBusiness liveStatsBusiness, TournamentBusiness tournamentBusiness,
     IBackgroundJobClient backgroundJobManager, ILogger<Form1> logger, IConnectionMultiplexer redisConnection, IServiceProvider serviceProvider,
     vmix_graphicsContext vmix_GraphicsContext, PostMatch postMatch, Reset reset, PreMatch preMatch, ApiCallProcessor apiCallProcessor)
        {
            _liveStatsBusiness = liveStatsBusiness;
            _Add_tournament = add_Tournament;
            _getLiveData = getLiveData;
            InitializeComponent();
            _backgroundJobManager = backgroundJobManager;
            _logger = logger;
            _tournamentBusiness = tournamentBusiness;
            _redisConnection = redisConnection;
            _redisDb = _redisConnection.GetDatabase();
            SubscribeToMatchStatus();

            var tournamentnames = _tournamentBusiness.getAll().Select(x => x.Name).ToList();
            Stage_cmb.DataSource = _tournamentBusiness.getAllStages().Select(x => x.Name).ToList();
            TournamentName_cmb.DataSource = tournamentnames;

            var days = new List<string>();
            days.Add("1"); days.Add("2"); days.Add("3"); days.Add("4"); days.Add("5"); days.Add("6"); days.Add("7"); days.Add("8");

            var matches = new List<string>();
            matches.Add("1"); matches.Add("2"); matches.Add("3"); matches.Add("4"); matches.Add("5"); matches.Add("6"); matches.Add("7"); matches.Add("8"); matches.Add("9"); matches.Add("10"); matches.Add("11"); matches.Add("12"); matches.Add("13"); matches.Add("14"); matches.Add("15"); matches.Add("16"); matches.Add("17");
            matches.Add("18"); matches.Add("19"); matches.Add("20"); matches.Add("21"); matches.Add("22"); matches.Add("23"); matches.Add("24"); matches.Add("25");

            var MapNames = new List<string>();
            MapNames.Add("Erangel"); MapNames.Add("Miramar"); MapNames.Add("Sanhok");
            MapName_cmb.DataSource = MapNames;
            Day_cmb.DataSource = days;
            Match_cmb.DataSource = matches;

            _serviceProvider = serviceProvider;
            _vmix_GraphicsContext = vmix_GraphicsContext;
            _postMatch = postMatch;
            _preMatch = preMatch;
            _reset = reset;

            // Load last match state from Redis
            LoadLastMatchState();
        }

        private void LoadLastMatchState()
        {
            try
            {
                var lastTournament = _redisDb.StringGet("LastMatch:Tournament");
                var lastStage = _redisDb.StringGet("LastMatch:Stage");
                var lastDay = _redisDb.StringGet("LastMatch:Day");
                var lastMatch = _redisDb.StringGet("LastMatch:Match");
                var lastMap = _redisDb.StringGet("LastMatch:Map");

                if (!lastTournament.IsNullOrEmpty)
                {
                    TournamentName_cmb.SelectedItem = lastTournament.ToString();
                    _logger.LogInformation($"Restored tournament: {lastTournament}");
                }

                if (!lastStage.IsNullOrEmpty)
                {
                    Stage_cmb.SelectedItem = lastStage.ToString();
                    _logger.LogInformation($"Restored stage: {lastStage}");
                }

                if (!lastDay.IsNullOrEmpty)
                {
                    Day_cmb.SelectedItem = lastDay.ToString();
                    _logger.LogInformation($"Restored day: {lastDay}");
                }

                if (!lastMatch.IsNullOrEmpty)
                {
                    Match_cmb.SelectedItem = lastMatch.ToString();
                    _logger.LogInformation($"Restored match: {lastMatch}");
                }

                if (!lastMap.IsNullOrEmpty)
                {
                    MapName_cmb.SelectedItem = lastMap.ToString();
                    _logger.LogInformation($"Restored map: {lastMap}");
                }

                _logger.LogInformation("Last match state loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading last match state from Redis");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            _logger.LogInformation("Form is closing.");

            if (MessageBox.Show("Are you sure you want to close the application?", "Confirm Exit", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                e.Cancel = true;
                return;
            }

            // Save current match state synchronously to avoid hanging
            try
            {
                _redisDb.StringSet("LastMatch:Tournament", TournamentName_cmb.SelectedItem?.ToString() ?? "");
                _redisDb.StringSet("LastMatch:Stage", Stage_cmb.SelectedItem?.ToString() ?? "");
                _redisDb.StringSet("LastMatch:Day", Day_cmb.SelectedItem?.ToString() ?? "");
                _redisDb.StringSet("LastMatch:Match", Match_cmb.SelectedItem?.ToString() ?? "");
                _redisDb.StringSet("LastMatch:Map", MapName_cmb.SelectedItem?.ToString() ?? "");

                _logger.LogInformation("Match state saved on close");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving match state on close");
            }

            _subscriber.UnsubscribeAll();

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
        private void Add_Tournament_btn_Click(object sender, EventArgs e)
        {
            _Add_tournament.Show();
        }
        private async void start_btn_Click(object sender, EventArgs e)
        {
            // Disable button to prevent double-clicks
            start_btn.Enabled = false;

            try
            {
                var result = await _tournamentBusiness.add_match(
                    TournamentName_cmb.Text,
                    Stage_cmb.Text,
                    Day_cmb.Text,
                    Match_cmb.Text
                );

                switch (result.statusCode)
                {
                    case 0:
                        // New match or empty match - start directly
                        await StartMatchAsync(result.match);
                        break;

                    case 1:
                        // In-progress match - ask to continue
                        var continueResult = MessageBox.Show(
                            result.message,
                            "Continue Match?",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question
                        );

                        if (continueResult == DialogResult.Yes)
                        {
                            await StartMatchAsync(result.match);
                        }
                        break;

                    case 2:
                        // Completed match - strong warning
                        var restartResult = MessageBox.Show(
                            result.message,
                            "RESTART COMPLETED MATCH? ",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning,
                            MessageBoxDefaultButton.Button2  // Default to "No"
                        );

                        if (restartResult == DialogResult.Yes)
                        {
                            // Show confirmation dialog again for completed matches
                            var confirmResult = MessageBox.Show(
                                "This action cannot be undone!\n\nType 'DELETE' to confirm:",
                                "Final Confirmation",
                                MessageBoxButtons.OKCancel,
                                MessageBoxIcon.Stop
                            );

                            if (confirmResult == DialogResult.OK)
                            {
                                // Better: Show input dialog to type "DELETE"
                                // For now, proceed with deletion
                                await _tournamentBusiness.DeleteMatchHistory(result.match);
                                await StartMatchAsync(result.match);

                                _logger.LogWarning(
                                    "COMPLETED match deleted and restarted: " +
                                    "Tournament={Tournament}, Stage={Stage}, Day={Day}, Match={Match}",
                                    TournamentName_cmb.Text, Stage_cmb.Text,
                                    Day_cmb.Text, Match_cmb.Text
                                );
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting match");
                MessageBox.Show(
                    $"Error starting match: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                start_btn.Enabled = true;
            }
        }

        private async Task StartMatchAsync(Match match)
        {
            _backgroundJobManager.Enqueue(HangfireQueues.HighPriority, () => _getLiveData.FetchAndPostData(match));
            _logger.LogInformation("Match started: MatchId={MatchId}, Day={Day}",
                match.MatchId, match.MatchDayId);
        }
        private async void reload_teams_btn_Click(object sender, EventArgs e)
        {
            try
            {
                var jsonTeamService = _serviceProvider.GetRequiredService<JsonTeamDataService>();
                await jsonTeamService.LoadTeamDataAsync();
                MessageBox.Show("Teams data reloaded successfully from JSON file.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Refresh the tournament dropdown
                await LoadTournamentsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reloading teams data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _logger.LogError(ex, "Error reloading teams data");
            }
        }
        private void manual_data_btn_Click(object sender, EventArgs e)
        {
            var manualInputForm = _serviceProvider.GetRequiredService<ManualDataInputForm>();
            manualInputForm.ShowDialog();
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

        private async void Form1_Load(object sender, EventArgs e)
        {
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
                // Log or handle exceptions
                _logger.LogInformation($"Error deleting folder: {ex.Message}");
            }

            // Sync keys with cloud on startup
            await SyncKeysWithCloudAsync();
        }

        private async Task<bool> ShowAuthenticationAsync()
        {
            try
            {
                using var authForm = new AuthenticationForm(_serviceProvider);
                var result = authForm.ShowDialog();

                if (result == DialogResult.OK && authForm.IsAuthenticated)
                {
                    var authKeyService = _serviceProvider.GetRequiredService<AuthKeyService>();
                    await authKeyService.SaveKeyToDbAsync(authForm.ValidatedKey);
                    _logger.LogInformation($"User authenticated successfully with key: {authForm.ValidatedKey}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication process");
                MessageBox.Show("Authentication error occurred. Application will close.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
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
        private async void stop_Click(object sender, EventArgs e)
        {
            stop_Click(false);
        }
        private async Task stop_Click(bool isautotriggered)
        {
            CancelAllHighPriorityJobs();
            _logger.LogInformation("Recurring job stopped.");


            // Remove all Redis keys related to the achievements
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

            this.start_btn.Enabled = true;

            // Restart the application
            System.Diagnostics.Process.Start(Application.ExecutablePath);
            Application.Exit();
            if (!isautotriggered)
            {
                MessageBox.Show("All jobs stopped match will be started fresh.");
            }
        }
        public void CancelAllHighPriorityJobs()
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    var monitoringApi = JobStorage.Current.GetMonitoringApi();
                    int deletedCount = 0;

                    // Get enqueued jobs from the high-priority queue
                    var enqueuedJobs = monitoringApi.EnqueuedJobs(HangfireQueues.HighPriority, 0, 1000);
                    enqueuedJobs.AddRange(monitoringApi.EnqueuedJobs(HangfireQueues.LowPriority, 0, 1000));
                    enqueuedJobs.AddRange(monitoringApi.EnqueuedJobs(HangfireQueues.Default, 0, 1000));
                    foreach (var job in enqueuedJobs)
                    {
                        _backgroundJobManager.Delete(job.Key);
                        deletedCount++;
                    }

                    // Get processing jobs
                    var processingJobs = monitoringApi.ProcessingJobs(0, 1000);
                    foreach (var job in processingJobs)
                    {
                        _backgroundJobManager.Delete(job.Key);
                        deletedCount++;

                    }

                    // Get scheduled jobs
                    var scheduledJobs = monitoringApi.ScheduledJobs(0, 1000);
                    foreach (var job in scheduledJobs)
                    {
                        _backgroundJobManager.Delete(job.Key);
                        deletedCount++;

                    }

                    _logger.LogInformation($"Deleted {deletedCount} jobs from high-priority queue");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting high-priority jobs");
                }
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            //TournamentName_cmb.Text, Stage_cmb.Text, Day_cmb.Text, Match_cmb.Text
            var tournament = _vmix_GraphicsContext.Tournaments.Where(x => x.Name == TournamentName_cmb.Text).FirstOrDefault();
            var stage = _vmix_GraphicsContext.Stages.Where(x => x.Name == Stage_cmb.Text).FirstOrDefault();
            var match = await _vmix_GraphicsContext.Matches.Where(x => x.TournamentId == tournament.TournamentId && x.StageId == stage.StageId && x.MatchDayId == int.Parse(Day_cmb.Text) && x.MatchId == int.Parse(Match_cmb.Text)).FirstOrDefaultAsync();
            _postMatch.TeamsToWatch(match);

        }

        private void button2_Click(object sender, EventArgs e)
        {
            _backgroundJobManager.Enqueue(HangfireQueues.HighPriority, () => _reset.ResetAll(_backgroundJobManager));
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            //TournamentName_cmb.Text, Stage_cmb.Text, Day_cmb.Text, Match_cmb.Text
            var tournament = _vmix_GraphicsContext.Tournaments.Where(x => x.Name == TournamentName_cmb.Text).FirstOrDefault();
            var stage = _vmix_GraphicsContext.Stages.Where(x => x.Name == Stage_cmb.Text).FirstOrDefault();
            var match = await _vmix_GraphicsContext.Matches.Where(x => x.TournamentId == tournament.TournamentId && x.StageId == stage.StageId && x.MatchDayId == int.Parse(Day_cmb.Text) && x.MatchId == int.Parse(Match_cmb.Text)).FirstOrDefaultAsync();
            _postMatch.MatchRankings(match);

        }

        private async void button4_Click(object sender, EventArgs e)
        {
            //TournamentName_cmb.Text, Stage_cmb.Text, Day_cmb.Text, Match_cmb.Text
            var tournament = _vmix_GraphicsContext.Tournaments.Where(x => x.Name == TournamentName_cmb.Text).FirstOrDefault();
            var stage = _vmix_GraphicsContext.Stages.Where(x => x.Name == Stage_cmb.Text).FirstOrDefault();
            var match = await _vmix_GraphicsContext.Matches.Where(x => x.TournamentId == tournament.TournamentId && x.StageId == stage.StageId && x.MatchDayId == int.Parse(Day_cmb.Text) && x.MatchId == int.Parse(Match_cmb.Text)).FirstOrDefaultAsync();
            _postMatch.OverallRankings(match);

        }

        private async void button5_Click(object sender, EventArgs e)
        {
            //TournamentName_cmb.Text, Stage_cmb.Text, Day_cmb.Text, Match_cmb.Text
            var tournament = _vmix_GraphicsContext.Tournaments.Where(x => x.Name == TournamentName_cmb.Text).FirstOrDefault();
            var stage = _vmix_GraphicsContext.Stages.Where(x => x.Name == Stage_cmb.Text).FirstOrDefault();
            var match = await _vmix_GraphicsContext.Matches.Where(x => x.TournamentId == tournament.TournamentId && x.StageId == stage.StageId && x.MatchDayId == int.Parse(Day_cmb.Text) && x.MatchId == int.Parse(Match_cmb.Text)).FirstOrDefaultAsync();
            _postMatch.MatchMvp(match);

        }

        private async void button7_Click(object sender, EventArgs e)
        {
            //TournamentName_cmb.Text, Stage_cmb.Text, Day_cmb.Text, Match_cmb.Text
            var tournament = _vmix_GraphicsContext.Tournaments.Where(x => x.Name == TournamentName_cmb.Text).FirstOrDefault();
            var stage = _vmix_GraphicsContext.Stages.Where(x => x.Name == Stage_cmb.Text).FirstOrDefault();
            var match = await _vmix_GraphicsContext.Matches.Where(x => x.TournamentId == tournament.TournamentId && x.StageId == stage.StageId && x.MatchDayId == int.Parse(Day_cmb.Text) && x.MatchId == int.Parse(Match_cmb.Text)).FirstOrDefaultAsync();
            _postMatch.WWCDStatsAsync(match);
            _postMatch.WWCDStatsAsync(match);
            _postMatch.MatchMvp(match);
            _postMatch.MatchRankings(match);
            _postMatch.OverallRankings(match);
            _postMatch.MatchSummary(match);
            _postMatch.DaySummary(match);
            _postMatch.Top5MatchMVP(match);
            _postMatch.Top5StageMVP(match);
            _postMatch.StageMVP(match);
            _postMatch.TopGrenadiers(match);
            _postMatch.TeamsToWatch(match);

        }

        private async void button6_Click(object sender, EventArgs e)
        {
            await setall();
        }
        private async Task<bool> setall()
        {

            //TournamentName_cmb.Text, Stage_cmb.Text, Day_cmb.Text, Match_cmb.Text
            var tournament = _vmix_GraphicsContext.Tournaments.Where(x => x.Name == TournamentName_cmb.Text).FirstOrDefault();
            var stage = _vmix_GraphicsContext.Stages.Where(x => x.Name == Stage_cmb.Text).FirstOrDefault();
            var match = await _vmix_GraphicsContext.Matches.Where(x => x.TournamentId == tournament.TournamentId && x.StageId == stage.StageId && x.MatchDayId == int.Parse(Day_cmb.Text) && x.MatchId == int.Parse(Match_cmb.Text)).FirstOrDefaultAsync();
            await _postMatch.WWCDStatsAsync(match);
            await _postMatch.MatchMvp(match);
            await _postMatch.MatchRankings(match);
            await _postMatch.OverallRankings(match);
            await _postMatch.DaySummary(match);
            await _postMatch.MatchSummary(match);
            await _postMatch.Top5MatchMVP(match);
            await _postMatch.Top5StageMVP(match);
            await _postMatch.StageMVP(match);
            await _postMatch.TopGrenadiers(match);
            await _postMatch.TeamsToWatch(match);
            return true;

        }

        private async void button8_Click(object sender, EventArgs e)
        {
            var tournament = _vmix_GraphicsContext.Tournaments.Where(x => x.Name == TournamentName_cmb.Text).FirstOrDefault();
            var stage = _vmix_GraphicsContext.Stages.Where(x => x.Name == Stage_cmb.Text).FirstOrDefault();
            var match = await _vmix_GraphicsContext.Matches.Where(x => x.TournamentId == tournament.TournamentId && x.StageId == stage.StageId && x.MatchDayId == int.Parse(Day_cmb.Text) && x.MatchId == int.Parse(Match_cmb.Text)).FirstOrDefaultAsync();

            _preMatch.MapTopPerformers(match, MapName_cmb.Text);
        }
        private void btnDatabaseBackup_Click(object sender, EventArgs e)
        {
            var backupForm = _serviceProvider.GetRequiredService<BackupForm>();
            backupForm.ShowDialog();
        }

        private async Task SubscribeToMatchStatus()
        {
            _subscriber = _redisConnection.GetSubscriber();

            var db = _redisConnection.GetDatabase();
            _subscriber.Subscribe("match-status-channel", (channel, value) =>
            {
                // This runs on a background thread, so use Invoke for UI updates
                this.Invoke(new Action(async () =>
                {
                    string status = value.ToString();

                    if (status == "Started")
                    {
                        _reset.ResetAll(_backgroundJobManager);
                        MessageBox.Show(
                            $"{db.StringGet(HelperRedis.MatchStatus)}",
                            "Match Started",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    }
                    else if (status == "Ended")
                    {

                        _backgroundJobManager.Enqueue(HangfireQueues.HighPriority, () => _reset.ResetAll(_backgroundJobManager));
                        await stop_Click(true);
                        await Task.Delay(5000);
                        await setall();
                        MessageBox.Show("Match has ended!", "Match Status",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);


                    }
                    else if (status == "Exception")
                    {
                        _backgroundJobManager.Enqueue(() => _reset.ResetAll(_backgroundJobManager));

                        await stop_Click(true);

                        await setall();
                        MessageBox.Show($"Match has ended! {db.StringGet(HelperRedis.MatchStatus)}", "Match Status",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);

                    }
                    this.start_btn.Enabled = status == "Ended" || status == "Exception";
                }));
            });
        }

    }
}