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
using Microsoft.AspNetCore.Builder;
using Hangfire.Server;
using System.Diagnostics;

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

                // ✅ Ensure Hangfire storage is initialized before any job execution
                var redis = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
                GlobalConfiguration.Configuration.UseStorage(new RedisStorage(redis));

                var activator = new DependencyJobActivator(serviceProvider);
                GlobalConfiguration.Configuration.UseActivator(activator);

                var serverOptions = new BackgroundJobServerOptions
                {
                    Queues = new[] { HangfireQueues.Default, HangfireQueues.HighPriority, HangfireQueues.LowPriority },
                    WorkerCount = Environment.ProcessorCount * 6,
                    Activator = activator
                };

                // ✅ Create multiple Hangfire servers
                _hangfireServers = new List<BackgroundJobServer>();
                for (int i = 0; i < 2; i++) // Adjust number of servers as needed
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

            services.AddScoped<VMIXDataoperations>();
            services.AddTransient<LiveStatsBusiness>();
            services.AddScoped<TournamentBusiness>();
            services.AddTransient<Add_tournament>();
            services.AddScoped<PostMatch>();
            services.AddTransient<SetPlayerAchievements>();
            services.AddScoped<GetLiveData>();
            services.AddSingleton<Form1>();
            services.AddScoped<ApiCallProcessor>();  
            services.AddScoped<Reset>();

            services.AddSingleton<IHostApplicationLifetime>(provider => provider.GetRequiredService<IHostApplicationLifetime>());
        }

        public static void ConfigureHangfire(this IServiceCollection services, IConfiguration configuration)
        {
            // ✅ Ensure Redis is registered as a singleton
            services.AddSingleton<IConnectionMultiplexer>(provider =>
                ConnectionMultiplexer.Connect(configuration.GetConnectionString("RedisConnection")));

            services.AddHangfire((provider, config) =>
            {
                var redis = provider.GetRequiredService<IConnectionMultiplexer>();

                config.UseRedisStorage(redis)
                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings();

                // ✅ Set Hangfire global storage instance
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
                return _serviceProvider.GetService(jobType) ;
            }
        }


        public class HangfireActivator(IServiceProvider container) : JobActivator
        {
            public override object ActivateJob(Type type) => container.GetService(type);
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
        public static void ConfigureHangfire1(this IApplicationBuilder app, IConfiguration configuration, IServiceProvider serviceProvider)
        {
            GlobalConfiguration.Configuration.UseActivator(new HangfireActivator(serviceProvider));
            GlobalConfiguration.Configuration.UseFilter(new ActivityServerFilter());
            var hangfireConfig = serviceProvider.GetRequiredService<HangfireConfiguration>();
            var dashboardOptions = new DashboardOptions
            {
                DashboardTitle = "Hangfire Dashboard",
                AppPath = "/swagger",
                
            };

            app.UseHangfireDashboard("/hangfire", dashboardOptions);
        }

    }

    public class HangfireConfiguration
    {
        public int JobExpirationTimeout { set; get; } = 6;
        public string ServerPrefix { set; get; } = "Vmix-";
        public int AutomaticRetry { set; get; } = 5;
        public int[] DelaysInSeconds { set; get; } = [1,1,1,1,1];
        public int DefaultWorkerCount { set; get; } = 40;
        public int RegistrationWorkerCount { set; get; } = 9;

        public string RegisterLogsCleanupCron { set; get; } = "0 0 * * *";
        public string ReconcileFailedCustomersCron { set; get; } = "0 23 * * *";
    }
}