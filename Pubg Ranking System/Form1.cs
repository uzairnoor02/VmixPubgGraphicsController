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

        public Form1(Add_tournament add_Tournament, GetLiveData getLiveData, LiveStatsBusiness liveStatsBusiness, TournamentBusiness tournamentBusiness, IBackgroundJobClient backgroundJobManager, ILogger<Form1> logger, IConnectionMultiplexer redisConnection, IServiceProvider serviceProvider, vmix_graphicsContext vmix_GraphicsContext, PostMatch postMatch, Reset reset, PreMatch preMatch)
        {
            _liveStatsBusiness = liveStatsBusiness;
            _Add_tournament = add_Tournament;
            _getLiveData = getLiveData;
            InitializeComponent();
            _backgroundJobManager = backgroundJobManager;
            _logger = logger;
            _tournamentBusiness = tournamentBusiness;
            _redisConnection = redisConnection;
            _redisDb = _redisConnection.GetDatabase(); // Initialize Redis database

            var tournamentnames = _tournamentBusiness.getAll().Select(x => x.Name).ToList();
            Stage_cmb.DataSource = _tournamentBusiness.getAllStages().Select(x => x.Name).ToList();
            TournamentName_cmb.DataSource = tournamentnames;
            var days = new List<string>();
            days.Add("1"); days.Add("2"); days.Add("3"); days.Add("4"); days.Add("5"); days.Add("6"); days.Add("7"); days.Add("8");
            var matches = new List<string>();
            matches.Add("1"); matches.Add("2"); matches.Add("3"); matches.Add("4"); matches.Add("5"); matches.Add("6"); matches.Add("7"); matches.Add("8");

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
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            _logger.LogInformation("Form is closing.");

            if (MessageBox.Show("Are you sure you want to close the application?", "Confirm Exit", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                return;
            }

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
            var result = await _tournamentBusiness.add_match(TournamentName_cmb.Text, Stage_cmb.Text, Day_cmb.Text, Match_cmb.Text);
            if (result.Item2 == 0)
            {
                // EnqueueFetchAndPostDataJob(result.Item3, _backgroundJobManager, _serviceProvider);
                _getLiveData.FetchAndPostData(result.Item3);
                _logger.LogInformation("Recurring job started for match {MatchId}.", result.Item3.MatchId);
            }
            else
            {
                if (MessageBox.Show(result.Item1, "", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _getLiveData.FetchAndPostData(result.Item3);
                    // MessageBox.Show("Recurring job started.");
                    _logger.LogInformation("Recurring job started for match {MatchId}.", result.Item3.MatchId);
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Define the output folder path
            string outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "resources");

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
        }
        private async void stop_Click(object sender, EventArgs e)
        {
            _backgroundJobManager.Delete(HangfireJobNames.FetchAndPostDataJob);
            MessageBox.Show("Recurring job stopped.");
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
        }
        public static void EnqueueFetchAndPostDataJob(Match match, IBackgroundJobClient recurringJobManager, IServiceProvider serviceProvider)
        {
            var getLiveData = serviceProvider.GetRequiredService<GetLiveData>();
            recurringJobManager.Enqueue(HangfireQueues.HighPriority,
                () => getLiveData.FetchAndPostData(match));
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
            _reset.ResetAll();
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

        }

        private async void button6_Click(object sender, EventArgs e)
        {
            //TournamentName_cmb.Text, Stage_cmb.Text, Day_cmb.Text, Match_cmb.Text
            var tournament = _vmix_GraphicsContext.Tournaments.Where(x => x.Name == TournamentName_cmb.Text).FirstOrDefault();
            var stage = _vmix_GraphicsContext.Stages.Where(x => x.Name == Stage_cmb.Text).FirstOrDefault();
            var match = await _vmix_GraphicsContext.Matches.Where(x => x.TournamentId == tournament.TournamentId && x.StageId == stage.StageId && x.MatchDayId == int.Parse(Day_cmb.Text) && x.MatchId == int.Parse(Match_cmb.Text)).FirstOrDefaultAsync();
            _postMatch.WWCDStatsAsync(match);
            _postMatch.MatchMvp(match);
            _postMatch.MatchRankings(match);
            _postMatch.OverallRankings(match);
            _postMatch.OverallRankings(match);

        }

        private async void button8_Click(object sender, EventArgs e)
        {
            var tournament = _vmix_GraphicsContext.Tournaments.Where(x => x.Name == TournamentName_cmb.Text).FirstOrDefault();
            var stage = _vmix_GraphicsContext.Stages.Where(x => x.Name == Stage_cmb.Text).FirstOrDefault();
            var match = await _vmix_GraphicsContext.Matches.Where(x => x.TournamentId == tournament.TournamentId && x.StageId == stage.StageId && x.MatchDayId == int.Parse(Day_cmb.Text) && x.MatchId == int.Parse(Match_cmb.Text)).FirstOrDefaultAsync();
            
            _preMatch.MapTopPerformers(match,MapName_cmb.Text);
        }

    }
}
