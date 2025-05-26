using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChronoGuard.Infrastructure.Services
{
    public class ConfigManager
    {
        private readonly string configPath;
        public ConfigManager(string appDataPath)
        {
            configPath = Path.Combine(appDataPath, "config.json");
        }
        public async Task<AppConfig> LoadConfigAsync()
        {
            if (!File.Exists(configPath))
                return new AppConfig();
            var json = await File.ReadAllTextAsync(configPath);
            return JsonSerializer.Deserialize<AppConfig>(json);
        }
        public async Task SaveConfigAsync(AppConfig config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(configPath, json);
        }
    }
    public class AppConfig
    {
        public string Version { get; set; } = "1.0";
        public GeneralConfig General { get; set; } = new();
        public LocationConfig Location { get; set; } = new();
        public string ActiveProfile { get; set; } = "classic";
        public NotificationConfig Notifications { get; set; } = new();
    }
    public class GeneralConfig
    {
        public bool AutoStart { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool CheckForUpdates { get; set; } = true;
    }
    public class LocationConfig
    {
        public string Method { get; set; } = "auto";
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string City { get; set; }
        public string UpdateFrequency { get; set; } = "daily";
    }
    public class NotificationConfig
    {
        public bool Enabled { get; set; } = true;
        public string Level { get; set; } = "basic";
        public QuietHoursConfig QuietHours { get; set; } = new();
    }
    public class QuietHoursConfig
    {
        public string Start { get; set; } = "22:00";
        public string End { get; set; } = "08:00";
    }
}
