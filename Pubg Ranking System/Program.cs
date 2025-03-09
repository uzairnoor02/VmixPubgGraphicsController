using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Hangfire;
using StackExchange.Redis;
using VmixData.Models;
using VmixGraphicsBusiness;
using VmixGraphicsBusiness.vmixutils;
using VmixGraphicsBusiness.MatchBusiness;
using System;
using System.IO;
using System.Windows.Forms;
using Hangfire.Redis.StackExchange;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using VmixGraphicsBusiness.Utils;

namespace Pubg_Ranking_System
{
    internal static class Program
    {
        public static IConfiguration Configuration { get; private set; }
        private static BackgroundJobServer _hangfireServer;

        [STAThread]
        static void Main()
        {
            try
            {
                // Load configuration
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                Configuration = builder.Build();

                // Validate configuration
                var redisConnectionString = Configuration.GetConnectionString("RedisConnection");
                if (string.IsNullOrEmpty(redisConnectionString))
                {
                    MessageBox.Show("Redis connection string is missing in appsettings.json.");
                    return;
                }

                // Configure Redis
                var redis = ConnectionMultiplexer.Connect(redisConnectionString);
                GlobalSettings.Initialize(Configuration);

                // Configure services for Windows Forms
                var services = new ServiceCollection();

                // Configure Entity Framework (MySQL)
                services.AddDbContextPool<vmix_graphicsContext>(options =>
                {
                    var connectionString = Configuration.GetConnectionString("DefaultConnection");
                    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                });

                // Register Redis in the Windows Forms DI container
                services.AddSingleton<IConnectionMultiplexer>(redis);

                // Register Hangfire services in the Windows Forms DI container
                services.ConfigureHangfire(Configuration);

                // Register IBackgroundJobClient for service-based APIs
                services.AddSingleton<IBackgroundJobClient, BackgroundJobClient>();

                // Register dependencies for Windows Forms
                ApplicationConfiguration.Initialize();
                services.AddSingleton<IConfiguration>(Configuration);
                ConfigureServices(services, Configuration);

                // Build service provider for Windows Forms
                using var serviceProvider = services.BuildServiceProvider();

                // Initialize Hangfire JobStorage
                GlobalConfiguration.Configuration.UseRedisStorage(redis);

                // Start Hangfire servers explicitly with custom job activator
                _hangfireServer = new BackgroundJobServer(new BackgroundJobServerOptions
                {
                    Queues = new[] { HangfireQueues.Default, HangfireQueues.HighPriority, HangfireQueues.LowPriority },
                    WorkerCount = Environment.ProcessorCount * 2, // Adjust worker count as needed
                    Activator = new DependencyJobActivator(serviceProvider)
                });

                // Remove the recurring job if it exists at the start
                using (var scope = serviceProvider.CreateScope())
                {
                    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

                    recurringJobManager.RemoveIfExists(HangfireJobNames.FetchAndPostDataJob);
                }

                // Start Hangfire Dashboard in a separate thread
                var dashboardThread = new System.Threading.Thread(() =>
                {
                    var host = Host.CreateDefaultBuilder()
                        .ConfigureWebHostDefaults(webBuilder =>
                        {
                            webBuilder.UseKestrel()
                                .UseUrls("http://localhost:5001") // Adjust as needed
                                .ConfigureServices((context, services) =>
                                {
                                    // Register Hangfire services in the ASP.NET Core pipeline
                                    services.ConfigureHangfire(Configuration);
                                })
                                .Configure(app =>
                                {
                                    // Enable Hangfire Dashboard
                                    app.UseHangfireDashboard();
                                });
                        })
                        .Build();

                    host.Run();
                });
                dashboardThread.Start();

                // Run main Windows Form
                var mainForm = serviceProvider.GetRequiredService<Form1>();
                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
            finally
            {
                // Dispose of the Hangfire server when the application exits
                _hangfireServer?.Dispose();
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

            // Register dependencies for Windows Forms
            services.AddSingleton<LiveStatsBusiness>();
            services.AddSingleton<TournamentBusiness>();
            services.AddSingleton<Add_tournament>();
            services.AddSingleton<PostMatch>();
            services.AddSingleton<SetPlayerAchievements>();
            services.AddSingleton<vmi_layerSetOnOff>();
            services.AddSingleton<VMIXDataoperations>();
            services.AddSingleton<GetLiveData>();
            services.AddSingleton<Form1>();
        }

        public static void ConfigureHangfire(this IServiceCollection services, IConfiguration configuration)
        {
            var redisConnectionString = configuration.GetConnectionString("RedisConnection");
            var redis = ConnectionMultiplexer.Connect(redisConnectionString);

            // Configure Hangfire to use Redis storage
            services.AddHangfire(config => config.UseRedisStorage(redis));
            services.AddHangfireServer(options =>
            {
                options.Queues = new[] { HangfireQueues.Default, HangfireQueues.HighPriority, HangfireQueues.LowPriority };
                options.WorkerCount = Environment.ProcessorCount * 2; // Adjust worker count as needed
            });

            // Initialize Hangfire JobStorage
            GlobalConfiguration.Configuration.UseRedisStorage(redis);
        }
    }
}
