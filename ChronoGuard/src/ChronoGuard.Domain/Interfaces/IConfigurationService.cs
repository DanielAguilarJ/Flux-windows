using ChronoGuard.Domain.Configuration;

namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Service for managing application configuration
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets the current application configuration
    /// </summary>
    Task<AppConfiguration> GetConfigurationAsync();

    /// <summary>
    /// Saves the application configuration
    /// </summary>
    Task SaveConfigurationAsync(AppConfiguration configuration);

    /// <summary>
    /// Gets a specific configuration value
    /// </summary>
    Task<T?> GetValueAsync<T>(string key);

    /// <summary>
    /// Sets a specific configuration value
    /// </summary>
    Task SetValueAsync<T>(string key, T value);

    /// <summary>
    /// Resets configuration to defaults
    /// </summary>
    Task ResetToDefaultsAsync();

    /// <summary>
    /// Event raised when configuration changes
    /// </summary>
    event EventHandler<AppConfiguration>? ConfigurationChanged;
}

/// <summary>
/// Application configuration model
/// </summary>
public class AppConfiguration
{
    public GeneralSettings General { get; set; } = new();
    public LocationSettings Location { get; set; } = new();
    public NotificationSettings Notifications { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

public class GeneralSettings
{
    public bool AutoStart { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool CheckForUpdates { get; set; } = true;
    public string Language { get; set; } = "es-ES";
    public string GlobalHotkey { get; set; } = "Ctrl+Alt+F";
}

public class LocationSettings
{
    public LocationMethod Method { get; set; } = LocationMethod.Auto;
    public double? ManualLatitude { get; set; }
    public double? ManualLongitude { get; set; }
    public string? ManualCity { get; set; }
    public LocationUpdateFrequency UpdateFrequency { get; set; } = LocationUpdateFrequency.Daily;
    public bool AllowIpLocation { get; set; } = true;
}

public class NotificationSettings
{
    public bool Enabled { get; set; } = true;
    public NotificationLevel Level { get; set; } = NotificationLevel.Basic;
    public TimeSpan QuietHoursStart { get; set; } = TimeSpan.FromHours(22);
    public TimeSpan QuietHoursEnd { get; set; } = TimeSpan.FromHours(8);
    public bool ShowTransitionNotifications { get; set; } = false;
    public bool ShowSleepReminders { get; set; } = true;
}

public class AdvancedSettings
{
    public List<string> ExcludedApplications { get; set; } = new();
    public bool MultiMonitorSupport { get; set; } = false;
    public int TransitionUpdateIntervalMs { get; set; } = 30000;
    public bool UseHardwareAcceleration { get; set; } = true;
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
}

public enum LocationMethod
{
    Auto,
    WindowsLocationApi,
    IpAddress,
    Manual
}

public enum LocationUpdateFrequency
{
    Never,
    OnStartup,
    Daily,
    Weekly
}

public enum NotificationLevel
{
    Silent,
    Basic,
    Detailed
}

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}
