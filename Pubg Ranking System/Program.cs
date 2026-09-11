using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Hangfire;
using VmixData.Models;
using VmixGraphicsBusiness;
using VmixGraphicsBusiness.vmixutils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using VmixGraphicsBusiness.Utils;
using VmixGraphicsBusiness.PostMatchStats;
using VmixGraphicsBusiness.LiveMatch;
using Microsoft.AspNetCore.Builder;
using Hangfire.Server;
using System.Diagnostics;
using System.Linq;
using Hangfire.Storage;
using VmixGraphicsBusiness.PreMatch;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Collections;
using VmixGraphicsBusiness.Auth;

namespace Pubg_Ranking_System
{
    internal static class Program
    {
        public static IConfiguration Configuration { get; private set; }
        private static List<BackgroundJobServer> _hangfireServers;

        [STAThread]
        static async Task Main()
        {
            try
            {

                //string encrypted = GoogleCredentialsEncryption.EncryptFile(@"D:\vmix files\VmixPubgGraphicsController-20240609T202836Z-001\VmixPubgGraphicsController\Pubg Ranking System\pubg-vmix-app.json");
                //File.WriteAllText("encrypted_output.txt", encrypted);
                //MessageBox.Show("Encrypted! Check encrypted_output.txt");
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                Configuration = builder.Build();


                ConfigGlobal.Initialize(Configuration);

                var services = new ServiceCollection();

                // In-process match state (replaces Redis for the live-match hot path - see
                // MatchStateStore.cs). Nothing here is shared across machines, so this no longer
                // needs a separate service that can fail to start (e.g. WSL not booting Redis).
                var matchState = new MatchStateStore();
                services.AddSingleton(matchState);

                // Overlay look/feel (chroma-key color, per-element show/hide) now lives here
                // instead of being hardcoded in the overlay HTML - controllable from the web
                // dashboard's Overlay Settings page. Singleton + its own small JSON file (see
                // OverlayConfigStore.cs), same "in-process, survives restart" pattern as matchState.
                services.AddSingleton<OverlayConfigStore>();

                // Hybrid database: try MySQL first, and if it isn't reachable within a few
                // seconds, fall back to a local SQLite file automatically so the app never fails
                // to start just because the DB server is down. Once MySQL comes back, restart the
                // app to pick it back up (this is a startup-time choice, not a live failover).
                ConfigureDatabase(services, Configuration);

                // Hangfire now runs entirely in-process (MemoryStorage, no Redis) and is only used
                // for the small set of non-time-critical fire-and-forget jobs left in this app
                // (achievement popups, reset/animation pushes). The real-time PUBG polling pipeline
                // no longer goes through Hangfire at all - see GetLiveData.FetchAndPostData, which
                // now calls straight into LiveStatsBusiness instead of enqueuing a job per tick.
                var hangfireStorage = new Hangfire.MemoryStorage.MemoryStorage();
                GlobalConfiguration.Configuration.UseStorage(hangfireStorage);
                services.ConfigureHangfire(hangfireStorage);
                services.AddSingleton<IBackgroundJobClient, BackgroundJobClient>();


                ApplicationConfiguration.Initialize();
                services.AddSingleton<IConfiguration>(Configuration);
                ConfigureServices(services, Configuration);


                // BUILD SERVICE PROVIDER
                using var serviceProvider = services.BuildServiceProvider();


                // Initialize database
                await InitializeDatabaseAsync(serviceProvider);


                var activator = new DependencyJobActivator(serviceProvider);
                GlobalConfiguration.Configuration.UseActivator(activator);


                // Remove all Hangfire jobs before starting
                ClearAllHangfireJobs();


                // A single server with a modest worker count is plenty for what's left on
                // Hangfire (lightweight fire-and-forget UI/animation/achievement jobs). The old
                // code ran 5 of these, each with ProcessorCount*5 workers, all pulling from the
                // same queues with no ordering guarantee - that over-parallelization was never
                // needed and made out-of-order job execution more likely, not less.
                var serverOptions = new BackgroundJobServerOptions
                {
                    Queues = new[] { HangfireQueues.HighPriority, HangfireQueues.LowPriority, HangfireQueues.Default },
                    WorkerCount = Math.Max(4, Environment.ProcessorCount),
                    Activator = activator
                };

                _hangfireServers = new List<BackgroundJobServer> { new BackgroundJobServer(serverOptions) };


                var dashboardThread = new System.Threading.Thread(() =>
                {
                    var host = Host.CreateDefaultBuilder()
                        .ConfigureWebHostDefaults(webBuilder =>
                        {
                            webBuilder.UseKestrel()
                                .UseUrls("http://localhost:5001")
                                .ConfigureServices((context, services) =>
                                {
                                    // Reuse the SAME storage instance as the main app so the
                                    // dashboard shows real, live job data instead of an empty
                                    // second in-memory store.
                                    services.ConfigureHangfire(hangfireStorage);
                                })
                                .Configure(app =>
                                {
                                    app.UseHangfireDashboard();
                                });
                        })
                        .Build();

                    host.Run();
                });
                dashboardThread.Start();

                // LAN-accessible live dashboard (SignalR hub + read-only REST snapshot) - anyone
                // on the network can open Chrome and watch live stats without RDP/physical access
                // to this PC. View-only for now; see LiveDashboardHost.cs for what's in/out of
                // scope for this first pass.
                LiveDashboardHost.Start(
                    serviceProvider,
                    matchState,
                    serviceProvider.GetRequiredService<IBackgroundJobClient>(),
                    serviceProvider.GetRequiredService<Reset>());

                var mainForm = serviceProvider.GetRequiredService<Form1>();


                // Before running the application, show the authentication form
                var authForm = serviceProvider.GetRequiredService<AuthenticationForm>();


                if (authForm.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                // Open the web dashboard automatically, the same way the WinForms window itself
                // opens automatically - removes the manual "go start a browser" step. This is a
                // pure convenience on top of LiveDashboardHost, which was already started above
                // and has had the time it took to fill in the auth form to come up, so the page
                // should be ready the moment the browser opens. Fire-and-forget: if there's no
                // default browser configured (e.g. running on a bare server) this must never stop
                // the WinForms app itself from starting, so any failure here is swallowed.
                var autoOpenSetting = Configuration["WebDashboard:AutoOpenBrowser"];
                var autoOpenBrowser = string.IsNullOrWhiteSpace(autoOpenSetting) || !autoOpenSetting.Equals("false", StringComparison.OrdinalIgnoreCase);
                if (autoOpenBrowser)
                {
                    try
                    {
                        var dashboardUrl = Configuration["WebDashboard:DashboardUrl"];
                        if (string.IsNullOrWhiteSpace(dashboardUrl))
                        {
                            dashboardUrl = "http://localhost:5050";
                        }
                        Process.Start(new ProcessStartInfo(dashboardUrl) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not auto-open the web dashboard: {ex.Message}");
                    }
                }

                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ERROR at some step:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                    "Application Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (_hangfireServers != null)
                {
                    foreach (var server in _hangfireServers)
                    {
                        server.Dispose();
                    }
                }
            }
        }
        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddProvider(new FileLoggerProvider("resources/logs"));
                loggingBuilder.SetMinimumLevel(LogLevel.Information);
            });

            services.AddScoped<VMIXDataoperations>();
            services.AddTransient<LiveStatsBusiness>();
            services.AddScoped<TournamentBusiness>();
            services.AddTransient<Add_tournament>();
            services.AddTransient<PostMatch>();
            services.AddScoped<PreMatch>();
            services.AddTransient<SetPlayerAchievements>();
            services.AddScoped<GetLiveData>();
            services.AddSingleton<Form1>();
            services.AddScoped<ApiCallProcessor>();
            services.AddScoped<Reset>();
            services.AddTransient<DatabaseInitializer>(); 
            services.AddScoped<TournamentDataBackupService>();
            services.AddTransient<BackupForm>();
            services.AddTransient<JsonTeamDataService>();

            // ✅ ADD THESE HERE - BEFORE BuildServiceProvider()
            services.AddTransient<ManualDataInputForm>();
            services.AddScoped<ManualDataBackupService>();

            services.AddSingleton<IHostApplicationLifetime>(provider =>
                provider.GetRequiredService<IHostApplicationLifetime>());

            // Register authentication form and related services
            services.AddScoped<AuthenticationForm>();
            services.AddScoped<GoogleSheetsAuthService>();
            services.AddScoped<AuthKeyService>();
        }
        static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var dbInitializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            await dbInitializer.InitializeDatabaseAsync();

            // Load team data from JSON after database initialization
            var jsonTeamDataService = scope.ServiceProvider.GetRequiredService<JsonTeamDataService>();
            await jsonTeamDataService.LoadTeamDataAsync();
        }

        //private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        //{
        //    services.AddLogging(loggingBuilder =>
        //    {
        //        loggingBuilder.ClearProviders();
        //        loggingBuilder.AddProvider(new FileLoggerProvider("resources/logs"));
        //        loggingBuilder.SetMinimumLevel(LogLevel.Information);
        //    });

        //    services.AddScoped<VMIXDataoperations>();
        //    services.AddTransient<LiveStatsBusiness>();
        //    services.AddScoped<TournamentBusiness>();
        //    services.AddTransient<Add_tournament>();
        //    services.AddTransient<PostMatch>();
        //    services.AddScoped<PreMatch>();
        //    services.AddTransient<SetPlayerAchievements>();
        //    services.AddScoped<GetLiveData>();
        //    services.AddSingleton<Form1>();
        //    services.AddScoped<ApiCallProcessor>();
        //    services.AddScoped<Reset>();
        //    services.AddTransient<DatabaseInitializer>();
        //    services.AddTransient<JsonTeamDataService>();

        //    services.AddSingleton<IHostApplicationLifetime>(provider => provider.GetRequiredService<IHostApplicationLifetime>());

        //    // Register authentication form and related services
        //    services.AddScoped<AuthenticationForm>();
        //    services.AddScoped<GoogleSheetsAuthService>();
        //    services.AddScoped<AuthKeyService>();
        //}

        /// <summary>Registers Hangfire against a shared, already-created in-process storage
        /// instance. No AddHangfireServer() here - Main() creates the single real worker server;
        /// the dashboard host calls this too, but only to read the same storage, never to spin up
        /// its own competing workers.</summary>
        public static void ConfigureHangfire(this IServiceCollection services, JobStorage storage)
        {
            services.AddHangfire(config =>
            {
                config.UseStorage(storage)
                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings();
            });
        }

        /// <summary>Tries to reach MySQL within a short timeout. Returns false (never throws) on
        /// any failure so the caller can fall back to SQLite instead of the app failing to start.</summary>
        private static bool TryProbeMySql(string connectionString, TimeSpan timeout, out ServerVersion? serverVersion)
        {
            serverVersion = null;
            if (string.IsNullOrWhiteSpace(connectionString)) return false;
            try
            {
                var detectTask = Task.Run(() => ServerVersion.AutoDetect(connectionString));
                if (detectTask.Wait(timeout) && detectTask.Status == TaskStatus.RanToCompletion)
                {
                    serverVersion = detectTask.Result;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Hybrid DB wiring: use MySQL when it's reachable, otherwise fall back to a
        /// local SQLite file automatically so a down/unreachable DB server can never stop the app
        /// from starting. This is a startup-time choice - if MySQL comes back mid-session, restart
        /// the app to pick it back up.</summary>
        private static void ConfigureDatabase(IServiceCollection services, IConfiguration configuration)
        {
            var mysqlConnectionString = configuration.GetConnectionString("DefaultConnection");

            Action<DbContextOptionsBuilder> configureOptions;

            if (TryProbeMySql(mysqlConnectionString!, TimeSpan.FromSeconds(4), out var serverVersion))
            {
                Console.WriteLine("Database: MySQL is reachable, using it.");
                configureOptions = options => options.UseMySql(mysqlConnectionString, serverVersion!);
            }
            else
            {
                var stateDir = Path.Combine(AppContext.BaseDirectory, "state");
                Directory.CreateDirectory(stateDir);
                var sqlitePath = Path.Combine(stateDir, "vmix_fallback.db");
                Console.WriteLine($"Database: MySQL unreachable, falling back to local SQLite ({sqlitePath}). This session's data will not sync to MySQL until it's back and the app is restarted.");
                configureOptions = options => options.UseSqlite($"Data Source={sqlitePath}");
            }

            services.AddDbContextPool<vmix_graphicsContext>(configureOptions);
            // A pooled factory too, so code that needs several short-lived contexts (instead of
            // one held for a long time) can request IDbContextFactory<vmix_graphicsContext> -
            // e.g. post-match processing, to avoid one DbContext's change tracker growing for an
            // entire match's duration. See PostMatch.cs.
            services.AddPooledDbContextFactory<vmix_graphicsContext>(configureOptions);
        }

        public class DependencyJobActivator : JobActivator
        {
            private readonly IServiceProvider _serviceProvider;

            public DependencyJobActivator(IServiceProvider serviceProvider)
            {
                _serviceProvider = serviceProvider;
            }

            public override object ActivateJob(Type jobType)
            {
                return _serviceProvider.GetService(jobType);
            }
        }

        public class ActivityServerFilter : IServerFilter
        {
            public void OnPerforming(PerformingContext filterContext)
            {
                var activity = new Activity("HangfireJob");
                activity.Start();
                filterContext.Items["Activity"] = activity;
            }

            public void OnPerformed(PerformedContext filterContext)
            {
                if (!filterContext.Items.TryGetValue("Activity", out var item)) return;
                var activity = (Activity)item;
                activity?.Stop();
            }
        }

        public static void ClearAllHangfireJobs()
        {
            try
            {
                var monitoringApi = JobStorage.Current.GetMonitoringApi();

                // Remove all recurring jobs
                using (var connection = JobStorage.Current.GetConnection())
                {
                    foreach (var recurringJob in connection.GetRecurringJobs())
                    {
                        RecurringJob.RemoveIfExists(recurringJob.Id);
                    }
                }

                // Remove scheduled jobs
                foreach (var job in monitoringApi.ScheduledJobs(0, int.MaxValue))
                {
                    BackgroundJob.Delete(job.Key);
                }

                // Remove enqueued jobs
                foreach (var queueDto in monitoringApi.Queues())
                {
                    string queueName = queueDto.Name; // Extract queue name

                    foreach (var job in monitoringApi.EnqueuedJobs(queueName, 0, int.MaxValue))
                    {
                        BackgroundJob.Delete(job.Key);
                    }
                }

                // Remove processing jobs
                foreach (var job in monitoringApi.ProcessingJobs(0, int.MaxValue))
                {
                    BackgroundJob.Delete(job.Key);
                }

                // Remove failed jobs
                foreach (var job in monitoringApi.FailedJobs(0, int.MaxValue))
                {
                    BackgroundJob.Delete(job.Key);
                }

                Console.WriteLine("All Hangfire jobs have been cleared.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing Hangfire jobs: {ex.Message}");
            }
        }
    }

    //Add DatabaseInitializer class
    public class DatabaseInitializer
    {
        private readonly vmix_graphicsContext _context;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(vmix_graphicsContext context, ILogger<DatabaseInitializer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                await _context.Database.EnsureCreatedAsync();
                _logger.LogInformation("Database created/initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating/initializing the database.");
                throw; // Re-throw the exception to prevent the application from running with a potentially uninitialized database.
            }
        }
    }

    public class JsonTeamDataService
    {
        private readonly vmix_graphicsContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<JsonTeamDataService> _logger;

        public JsonTeamDataService(vmix_graphicsContext context, IConfiguration configuration, ILogger<JsonTeamDataService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task LoadTeamDataAsync()
        {
            try
            {
                var jsonFilePath = _configuration["JsonTeamDataPath"];
                if (string.IsNullOrEmpty(jsonFilePath))
                {
                    _logger.LogWarning("JsonTeamDataPath is not configured in appsettings.json.");
                    return;
                }

                if (!File.Exists(jsonFilePath))
                {
                    _logger.LogError($"JSON file not found at path: {jsonFilePath}");
                    return;
                }

                var jsonData = await File.ReadAllTextAsync(jsonFilePath);
                var tournamentjsonData = JsonConvert.DeserializeObject<TournamentData>(jsonData);

                if (tournamentjsonData == null)
                {
                    _logger.LogError("Failed to deserialize tournament data.");
                    return;
                }

                var tournament = await _context.Tournaments
                    .Include(t => t.Stages)
                    .ThenInclude(s => s.TeamsStages)
                    .FirstOrDefaultAsync(x => x.Name.ToLower() == tournamentjsonData.TournamentName.ToLower());

                if (tournament == null)
                {
                    var result = MessageBox.Show(
                        $"Tournament '{tournamentjsonData.TournamentName}' does not exist.\nDo you want to create it?",
                        "Create Tournament",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        tournament = new Tournament
                        {
                            Name = tournamentjsonData.TournamentName,
                            Stages = new List<Stage>()
                        };

                        _context.Tournaments.Add(tournament);
                        await _context.SaveChangesAsync();

                        MessageBox.Show($"Tournament '{tournament.Name}' created successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Tournament data import cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                foreach (var stageData in tournamentjsonData.Stages)
                {
                    // Check if stage exists
                    var stage = tournament.Stages.FirstOrDefault(s => s.Name.ToLower() == stageData.StageName.ToLower());
                    if (stage == null)
                    {
                        stage = new Stage
                        {
                            Name = stageData.StageName,
                            TournamentId = tournament.TournamentId
                        };

                        _context.Stages.Add(stage);
                        _context.SaveChanges();
                        tournament.Stages.Add(stage);

                        _logger.LogInformation($"Created new stage '{stage.Name}' for tournament '{tournament.Name}'.");
                    }

                    foreach (var teamData in stageData.Teams)
                    {
                        // Check if the team exists in this stage
                        var existingTeam = await _context.Teams
                            .FirstOrDefaultAsync(t => t.TeamId == teamData.TeamId.ToString() && t.StageId == stage.StageId);

                        if (existingTeam == null)
                        {
                            var newTeam = new Team
                            {
                                TeamId = teamData.TeamId.ToString(),
                                TeamName = teamData.TeamName,
                                StageId = stage.StageId,
                                TournamentId = stage.TournamentId
                            };

                            _context.Teams.Add(newTeam);
                            _logger.LogInformation($"Added team {newTeam.TeamName} to stage '{stage.Name}'.");
                        }
                        else
                        {
                            existingTeam.TeamName = teamData.TeamName;
                            _logger.LogInformation($"Updated existing team '{existingTeam.TeamName}' in stage '{stage.Name}'.");
                        }
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Tournament, stages, and teams loaded and synced successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading team data from JSON.");
            }
        }

    }

    // Define data structures for JSON deserializationusing System.Text.Json.Serialization;
    public class TournamentData
    {
        [JsonProperty("tournament_name")]
        public string TournamentName { get; set; }

        [JsonProperty("stages")]
        public List<StageData> Stages { get; set; }
    }

    public class StageData
    {
        [JsonProperty("stage_name")]
        public string StageName { get; set; }

        [JsonProperty("teams")]
        public List<TeamData> Teams { get; set; }
    }

    public class TeamData
    {
        [JsonProperty("team_id")]
        public int TeamId { get; set; }

        [JsonProperty("team_name")]
        public string TeamName { get; set; }
    }


}