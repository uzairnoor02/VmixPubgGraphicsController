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

                // ✅ Ensure Hangfire storage is initialized
                var redis = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
                GlobalConfiguration.Configuration.UseStorage(new RedisStorage(redis));

                var activator = new DependencyJobActivator(serviceProvider);
                GlobalConfiguration.Configuration.UseActivator(activator);

                // ✅ Remove all Hangfire jobs before starting
                ClearAllHangfireJobs();

                var serverOptions = new BackgroundJobServerOptions
                {
                    Queues = new[] {  HangfireQueues.HighPriority, HangfireQueues.LowPriority, HangfireQueues.Default },
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
            services.AddScoped<PreMatch>();
            services.AddTransient<SetPlayerAchievements>();
            services.AddScoped<GetLiveData>();
            services.AddSingleton<Form1>();
            services.AddScoped<ApiCallProcessor>();
            services.AddScoped<Reset>();

            services.AddSingleton<IHostApplicationLifetime>(provider => provider.GetRequiredService<IHostApplicationLifetime>());
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
}
