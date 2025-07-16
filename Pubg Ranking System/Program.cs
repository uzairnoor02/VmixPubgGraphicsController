using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Hangfire;
using StackExchange.Redis;
using VmixData.Models;
using VmixGraphicsBusiness;
using VmixGraphicsBusiness.vmixutils;
using Hangfire.Redis.StackExchange;
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
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                Configuration = builder.Build();

                var redisConnectionString = Configuration.GetConnectionString("RedisConnection");
                if (string.IsNullOrEmpty(redisConnectionString))
                {
                    MessageBox.Show("Redis connection string is missing in appsettings.json.");
                    return;
                }

                // Configure Redis
                ConfigGlobal.Initialize(Configuration);

                var services = new ServiceCollection();

                // ✅ Register Redis as a singleton
                services.AddSingleton<IConnectionMultiplexer>(provider =>
                    ConnectionMultiplexer.Connect(redisConnectionString));

                // ✅ Configure EF Core with MySQL
                services.AddDbContextPool<vmix_graphicsContext>(options =>
                {
                    var connectionString = Configuration.GetConnectionString("DefaultConnection");
                    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                });

                // ✅ Configure Hangfire
                services.ConfigureHangfire(Configuration);
                services.AddSingleton<IBackgroundJobClient, BackgroundJobClient>();

                ApplicationConfiguration.Initialize();
                services.AddSingleton<IConfiguration>(Configuration);
                ConfigureServices(services, Configuration);

                using var serviceProvider = services.BuildServiceProvider();

                // Initialize database
                await InitializeDatabaseAsync(serviceProvider);

                // ✅ Ensure Hangfire storage is initialized
                var redis = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
                GlobalConfiguration.Configuration.UseStorage(new RedisStorage(redis));

                var activator = new DependencyJobActivator(serviceProvider);
                GlobalConfiguration.Configuration.UseActivator(activator);

                // ✅ Remove all Hangfire jobs before starting
                ClearAllHangfireJobs();

                var serverOptions = new BackgroundJobServerOptions
                {
                    Queues = new[] { HangfireQueues.HighPriority, HangfireQueues.LowPriority, HangfireQueues.Default },
                    WorkerCount = Environment.ProcessorCount * 5,
                    Activator = activator
                };

                // ✅ Start multiple Hangfire servers
                _hangfireServers = new List<BackgroundJobServer>();
                for (int i = 0; i < 5; i++)
                {
                    _hangfireServers.Add(new BackgroundJobServer(serverOptions));
                }

                var dashboardThread = new System.Threading.Thread(() =>
                {
                    var host = Host.CreateDefaultBuilder()
                        .ConfigureWebHostDefaults(webBuilder =>
                        {
                            webBuilder.UseKestrel()
                                .UseUrls("http://localhost:5001")
                                .ConfigureServices((context, services) =>
                                {
                                    services.ConfigureHangfire(Configuration);
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

                var mainForm = serviceProvider.GetRequiredService<Form1>();

                // Before running the application, show the authentication form
                var authForm = serviceProvider.GetRequiredService<AuthenticationForm>();
                if (authForm.ShowDialog() != DialogResult.OK)
                {
                    // Authentication failed, exit the application
                    return;
                }

                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
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

        static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var dbInitializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            await dbInitializer.InitializeDatabaseAsync();

            // Load team data from JSON after database initialization
            var jsonTeamDataService = scope.ServiceProvider.GetRequiredService<JsonTeamDataService>();
            await jsonTeamDataService.LoadTeamDataAsync();
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
            services.AddTransient<JsonTeamDataService>();

            services.AddSingleton<IHostApplicationLifetime>(provider => provider.GetRequiredService<IHostApplicationLifetime>());

            // Register authentication form and related services
            services.AddScoped<AuthenticationForm>();
            services.AddScoped<GoogleSheetsAuthService>();
            services.AddScoped<AuthKeyService>();
        }

        public static void ConfigureHangfire(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IConnectionMultiplexer>(provider =>
                ConnectionMultiplexer.Connect(configuration.GetConnectionString("RedisConnection")));

            services.AddHangfire((provider, config) =>
            {
                var redis = provider.GetRequiredService<IConnectionMultiplexer>();

                config.UseRedisStorage(redis)
                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings();

                GlobalConfiguration.Configuration.UseStorage(new RedisStorage(redis));
            });

            services.AddHangfireServer(options =>
            {
                options.Queues = new[] { HangfireQueues.Default, HangfireQueues.HighPriority, HangfireQueues.LowPriority };
                options.WorkerCount = Environment.ProcessorCount * 2;
            });
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
                var tournamentData = JsonConvert.DeserializeObject<TournamentData>(jsonData);

                if (tournamentData == null)
                {
                    _logger.LogError("Failed to deserialize tournament data.");
                    return;
                }

                var tournament = await _context.Tournaments
                    .Include(t => t.Stages)
                    .ThenInclude(s => s.TeamsStages)
                    .FirstOrDefaultAsync(x => x.Name.ToLower() == tournamentData.TournamentName.ToLower());

                if (tournament == null)
                {
                    var result = MessageBox.Show(
                        $"Tournament '{tournamentData.TournamentName}' does not exist.\nDo you want to create it?",
                        "Create Tournament",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        tournament = new Tournament
                        {
                            Name = tournamentData.TournamentName,
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

                foreach (var stageData in tournamentData.Stages)
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
                                TournamentId=stage.TournamentId
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