using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

public static class GlobalSettings
{
    private static readonly object _lock = new object();
    private static IConfiguration _configuration;
    private static string _appSettingsFilePath;

    public static string VmixUrl { get; private set; }
    public static string PcobUrl { get; private set; }

    public static void Initialize(IConfiguration configuration)
    {
        _configuration = configuration;
        _appSettingsFilePath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

        VmixUrl = _configuration["VmixUrl"];
        PcobUrl = _configuration["pcobUrl"];
    }

    public static void UpdateUrls(string newVmixUrl, string newPcobUrl)
    {
        lock (_lock)
        {
            VmixUrl = newVmixUrl;
            PcobUrl = newPcobUrl;

            UpdateAppSettings();
        }
    }

    private static void UpdateAppSettings()
    {
        try
        {
            var json = File.ReadAllText(_appSettingsFilePath);
            var jsonObj = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (jsonObj != null)
            {
                if (jsonObj.ContainsKey("VmixUrl"))
                {
                    jsonObj["VmixUrl"] = VmixUrl;
                }

                if (jsonObj.ContainsKey("pcobUrl"))
                {
                    jsonObj["pcobUrl"] = PcobUrl;
                }

                var updatedJson = JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_appSettingsFilePath, updatedJson);
            }
        }
        catch (Exception ex)
        {Console.Write(ex.ToString());
        }
    }
}
