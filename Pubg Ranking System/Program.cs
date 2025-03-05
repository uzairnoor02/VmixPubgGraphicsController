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

namespace Pubg_Ranking_System
{
    internal static class Program
    {
        public static IConfiguration Configuration { get; private set; }

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var builder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build();
            var services = new ServiceCollection();

            // Configure Entity Framework (MySQL)
            services.AddDbContextPool<vmix_graphicsContext>(options =>
            {
                var connectionString = Configuration.GetConnectionString("DefaultConnection");
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            });

            // Ensure Redis connection
            var redis = ConnectionMultiplexer.Connect(Configuration.GetConnectionString("RedisConnection"));
            services.AddSingleton<IConnectionMultiplexer>(redis);

            // Configure Hangfire with Redis storage
            services.AddHangfire(config => config.UseRedisStorage(redis));
            services.AddHangfireServer();

            // Register Dependencies
            ApplicationConfiguration.Initialize();
            services.AddSingleton<IConfiguration>(Configuration);
            ConfigureServices(services, Configuration);

            using var serviceProvider = services.BuildServiceProvider();

            // Start Hangfire Dashboard (optional)
            var dashboardThread = new System.Threading.Thread(() =>
            {
                var host = Host.CreateDefaultBuilder()
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseKestrel()
                            .UseUrls("http://localhost:5001") // Adjust as needed
                            .Configure(app => app.UseHangfireDashboard());
                    })
                    .Build();

                host.Run();
            });
            dashboardThread.Start();

            // Run main Windows Form
            var mainForm = serviceProvider.GetRequiredService<Form1>();
            Application.Run(mainForm);
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Register dependencies
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
    }
}
