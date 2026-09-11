using System.Text.Json;

namespace VmixGraphicsBusiness.Utils
{
    /// <summary>
    /// Everything about how the vMix overlay looks and what it shows, controllable from the web
    /// dashboard's "Overlay Settings" page instead of being hardcoded in the HTML/HTML source.
    /// </summary>
    public class OverlayConfig
    {
        // Chroma-key background color for the overlay page, as a hex string (e.g. "#00FF00"). The
        // /overlay route paints its <body> this exact color so it can be keyed out with vMix's
        // Chroma Key effect - change it here instead of editing overlay source whenever a client's
        // vMix setup needs a different key color (some prefer green, some blue/magenta to avoid
        // clashing with team colors or skin tones on camera).
        public string ChromaKeyColor { get; set; } = "#00FF00";

        // Per-element show/hide toggles, keyed by a stable string id rather than individual bool
        // properties so new overlay elements (new achievement types, etc.) can be added later
        // without a breaking config-shape change. An id missing from this dictionary is treated as
        // visible by the frontend, so old configs keep working when a new element is introduced.
        public Dictionary<string, bool> ElementVisibility { get; set; } = new()
        {
            ["leaderboard"] = true,
            ["eliminationFeed"] = true,
            ["teamEliminatedBanner"] = true,
            ["achievement.grenadeElim"] = true,
            ["achievement.vehicleKill"] = true,
            ["achievement.airdropLoot"] = true,
            ["achievement.firstKill"] = true,
            ["achievement.knockout"] = true,
            ["achievement.chickenDinner"] = true,
        };

        // Free-form per-element settings (position, scale, accent color, duration, etc.) as raw
        // JSON the React frontend owns the shape of. Kept generic here on purpose so overlay
        // layout/behavior tweaks don't need a C# change and a WinForms redeploy - only a frontend
        // one - matching the "remove manual interventions, make this configurable from the web"
        // brief this was built for.
        public Dictionary<string, JsonElement> ElementSettings { get; set; } = new();
    }

    /// <summary>
    /// Loads/saves <see cref="OverlayConfig"/> to a small JSON file next to the app (same pattern
    /// as MatchStateStore's snapshot: atomic write via a .tmp file + copy, corrupt/missing file
    /// just means "start from defaults" rather than a startup failure) and raises
    /// <see cref="ConfigChanged"/> so callers can push the update to connected browsers over
    /// SignalR the instant it changes - no manual refresh needed on the overlay or the dashboard.
    /// </summary>
    public class OverlayConfigStore
    {
        private readonly object _lock = new();
        private readonly string _path;
        private OverlayConfig _config;

        public event Action<OverlayConfig>? ConfigChanged;

        public OverlayConfigStore(string? directory = null)
        {
            var dir = directory ?? Path.Combine(AppContext.BaseDirectory, "state");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "overlay_config.json");
            _config = Load();
        }

        public OverlayConfig Get()
        {
            lock (_lock)
            {
                return _config;
            }
        }

        public void Update(OverlayConfig newConfig)
        {
            lock (_lock)
            {
                _config = newConfig;
                Save(newConfig);
            }
            ConfigChanged?.Invoke(newConfig);
        }

        private OverlayConfig Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var json = File.ReadAllText(_path);
                    var loaded = JsonSerializer.Deserialize<OverlayConfig>(json);
                    if (loaded is not null)
                    {
                        return loaded;
                    }
                }
            }
            catch
            {
                // A corrupt/missing config file just means we start from defaults - never fatal.
            }
            return new OverlayConfig();
        }

        private void Save(OverlayConfig config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                var tmpPath = _path + ".tmp";
                File.WriteAllText(tmpPath, json);
                File.Copy(tmpPath, _path, overwrite: true);
                File.Delete(tmpPath);
            }
            catch
            {
                // Best-effort persistence - a setting failing to save to disk should never take
                // down the request that set it; it'll just need to be re-applied after a restart.
            }
        }
    }
}
