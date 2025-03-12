using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Hangfire;
using StackExchange.Redis;
using VmixData.Models;
using VmixGraphicsBusiness;
using VmixGraphicsBusiness.vmixutils;
using System;
using System.IO;
using System.Windows.Forms;
using Hangfire.Redis.StackExchange;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using VmixGraphicsBusiness.Utils;
using VmixGraphicsBusiness.PostMatchStats;
using VmixGraphicsBusiness.LiveMatch;
using static VmixGraphicsBusiness.LiveMatch.GetLiveData;

namespace Pubg_Ranking_System
{
    internal static class Program
    {
        public static IConfiguration Configuration { get; private set; }
        private static List<BackgroundJobServer> _hangfireServers;

        [STAThread]
        static void Main()
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
                var redis = ConnectionMultiplexer.Connect(redisConnectionString);
                ConfigGlobal.Initialize(Configuration);
                var services = new ServiceCollection();
                services.AddDbContextPool<vmix_graphicsContext>(options =>
                {
                    var connectionString = Configuration.GetConnectionString("DefaultConnection");
                    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                });

                services.AddSingleton<IConnectionMultiplexer>(redis);

                services.ConfigureHangfire(Configuration);

                services.AddSingleton<IBackgroundJobClient, BackgroundJobClient>();

                ApplicationConfiguration.Initialize();
                services.AddSingleton<IConfiguration>(Configuration);
                ConfigureServices(services, Configuration);

                using var serviceProvider = services.BuildServiceProvider();

                GlobalConfiguration.Configuration.UseRedisStorage(redis);
                 var serverOptions = new BackgroundJobServerOptions
                {
                    Queues = new[] { HangfireQueues.Default, HangfireQueues.HighPriority, HangfireQueues.LowPriority },
                    WorkerCount = Environment.ProcessorCount * 6, // Adjust worker count as needed
                    Activator = new DependencyJobActivator(serviceProvider)
                };

                // Create multiple servers
                _hangfireServers = new List<BackgroundJobServer>();
                for (int i = 0; i < 2; i++) // Adjust the number of servers as needed
                {
                    _hangfireServers.Add(new BackgroundJobServer(serverOptions));
                }
                using (var scope = serviceProvider.CreateScope())
                {
                    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
                    recurringJobManager.RemoveIfExists(HangfireJobNames.FetchAndPostDataJob);
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

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddProvider(new FileLoggerProvider("resources/logs"));
                loggingBuilder.SetMinimumLevel(LogLevel.Information);
            });

            services.AddTransient<VMIXDataoperations>(); // Add this line
            services.AddTransient<LiveStatsBusiness>();
            services.AddTransient<TournamentBusiness>();
            services.AddTransient<Add_tournament>();
            services.AddTransient<PostMatch>();
            services.AddTransient<SetPlayerAchievements>();
            services.AddTransient<vmi_layerSetOnOff>();
            services.AddTransient<GetLiveData>();
            services.AddSingleton<Form1>();
            services.AddScoped<ApiCallProcessor>();
            services.AddScoped<Reset>();

            services.AddSingleton<IHostApplicationLifetime>(provider => provider.GetRequiredService<IHostApplicationLifetime>());
        }

        public static void ConfigureHangfire(this IServiceCollection services, IConfiguration configuration)
        {
            var redisConnectionString = configuration.GetConnectionString("RedisConnection");
            var redis = ConnectionMultiplexer.Connect(redisConnectionString);

            services.AddHangfire(config =>
            {
                config.UseRedisStorage(redis)
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings();
            });
            services.AddHangfireServer(options =>
            {
                options.Queues = new[] { HangfireQueues.Default, HangfireQueues.HighPriority, HangfireQueues.LowPriority };
                options.WorkerCount = Environment.ProcessorCount * 2;
            });
            GlobalConfiguration.Configuration.UseRedisStorage(redis);
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
                return _serviceProvider.GetService(jobType) ?? throw new InvalidOperationException($"JobActivator returned NULL instance of the '{jobType.Name}' type.");
            }
        }
    }
}
