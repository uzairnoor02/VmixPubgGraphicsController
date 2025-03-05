using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Hangfire;
using Hangfire.MemoryStorage;
using OfficeOpenXml;
using Vmix_Hangfire_Graphics.Business;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Hangfire.Redis.StackExchange;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddJsonFile("appsettings.json", optional: false);
        // Add services to the container.
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddScoped<GetTotalPlayersHangfireJobBusiness>();
        builder.Services.AddSingleton<LiveStatsBusiness>();

        // Add Hangfire services
        builder.Services.AddHangfire(config =>
        {
            config.UseMemoryStorage();
        });

        // Redis Connection
        builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect("localhost:6379"));
       


        // Redis Connection (Ensure Redis is running)
        var redis = ConnectionMultiplexer.Connect("localhost:6379");
        builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

        // Configure Hangfire to use Redis instead of SQL Server
        builder.Services.AddHangfire(config => config.UseRedisStorage(redis));
        builder.Services.AddHangfireServer();

        var app = builder.Build();

        // Optional: Hangfire Dashboard for monitoring jobs
        app.UseHangfireDashboard();
        app.UseHangfireServer();

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();


        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var configuration = services.GetRequiredService<IConfiguration>();
            var backgroundJobClient = services.GetRequiredService<IBackgroundJobClient>();
            var liveStatsBusiness = services.GetRequiredService<LiveStatsBusiness>();

            var getTotalPlayersHangfireJobBusiness = new GetTotalPlayersHangfireJobBusiness(configuration, backgroundJobClient, liveStatsBusiness);
            getTotalPlayersHangfireJobBusiness.ScheduleRecurringJob();
        }
        app.UseHangfireDashboard("/hangfire");
        app.Run();
    }

}
