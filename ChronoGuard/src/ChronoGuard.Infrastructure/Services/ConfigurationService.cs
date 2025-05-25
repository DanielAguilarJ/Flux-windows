using ChronoGuard.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChronoGuard.Infrastructure.Services;

/// <summary>
/// Service for managing application configuration with persistence
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly string _configFilePath;
    private AppConfiguration _configuration;
    private readonly object _configLock = new();

    public event EventHandler<AppConfiguration>? ConfigurationChanged;

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
        
        // Set up configuration file path
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var chronoGuardPath = Path.Combine(appDataPath, "ChronoGuard");
        _configFilePath = Path.Combine(chronoGuardPath, "config.json");

        // Ensure directory exists
        Directory.CreateDirectory(chronoGuardPath);

        // Initialize with defaults and load from file
        _configuration = CreateDefaultConfiguration();
        _ = Task.Run(LoadConfigurationAsync);
    }

    /// <summary>
    /// Gets the current application configuration
    /// </summary>
    public async Task<AppConfiguration> GetConfigurationAsync()
    {
        await Task.CompletedTask; // Keep async interface for future database support
        
        lock (_configLock)
        {
            return CloneConfiguration(_configuration);
        }
    }

    /// <summary>
    /// Saves the application configuration
    /// </summary>
    public async Task SaveConfigurationAsync(AppConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        configuration.LastModified = DateTime.UtcNow;

        lock (_configLock)
        {
            _configuration = CloneConfiguration(configuration);
        }

        await SaveConfigurationToFileAsync(configuration);
        
        _logger.LogInformation("Configuration saved successfully");
        ConfigurationChanged?.Invoke(this, configuration);
    }

    /// <summary>
    /// Gets a specific configuration value by key path
    /// </summary>
    public async Task<T?> GetValueAsync<T>(string key)
    {
        var config = await GetConfigurationAsync();
        return GetValueFromConfiguration<T>(config, key);
    }

    /// <summary>
    /// Sets a specific configuration value by key path
    /// </summary>
    public async Task SetValueAsync<T>(string key, T value)
    {
        var config = await GetConfigurationAsync();
        SetValueInConfiguration(config, key, value);
        await SaveConfigurationAsync(config);
    }

    /// <summary>
    /// Resets configuration to defaults
    /// </summary>
    public async Task ResetToDefaultsAsync()
    {
        var defaultConfig = CreateDefaultConfiguration();
        await SaveConfigurationAsync(defaultConfig);
        
        _logger.LogInformation("Configuration reset to defaults");
    }

    /// <summary>
    /// Loads configuration from file
    /// </summary>
    private async Task LoadConfigurationAsync()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                _logger.LogInformation("Configuration file not found, using defaults");
                await SaveConfigurationAsync(_configuration);
                return;
            }

            var json = await File.ReadAllTextAsync(_configFilePath);
            var loadedConfig = JsonSerializer.Deserialize<AppConfiguration>(json, GetJsonOptions());

            if (loadedConfig != null)
            {
                // Merge with defaults to ensure all properties are set
                var mergedConfig = MergeWithDefaults(loadedConfig);
                
                lock (_configLock)
                {
                    _configuration = mergedConfig;
                }

                _logger.LogInformation("Configuration loaded successfully");
            }
            else
            {
                _logger.LogWarning("Could not deserialize configuration, using defaults");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration from file, using defaults");
        }
    }

    /// <summary>
    /// Saves configuration to file
    /// </summary>
    private async Task SaveConfigurationToFileAsync(AppConfiguration configuration)
    {
        try
        {
            var json = JsonSerializer.Serialize(configuration, GetJsonOptions());
            await File.WriteAllTextAsync(_configFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration to file");
            throw;
        }
    }

    /// <summary>
    /// Creates default configuration
    /// </summary>
    private static AppConfiguration CreateDefaultConfiguration()
    {
        return new AppConfiguration
        {
            General = new GeneralSettings
            {
                AutoStart = true,
                MinimizeToTray = true,
                CheckForUpdates = true,
                Language = "es-ES",
                GlobalHotkey = "Ctrl+Alt+F"
            },
            Location = new LocationSettings
            {
                Method = LocationMethod.Auto,
                UpdateFrequency = LocationUpdateFrequency.Daily,
                AllowIpLocation = true
            },
            Notifications = new NotificationSettings
            {
                Enabled = true,
                Level = NotificationLevel.Basic,
                QuietHoursStart = TimeSpan.FromHours(22),
                QuietHoursEnd = TimeSpan.FromHours(8),
                ShowTransitionNotifications = false,
                ShowSleepReminders = true
            },
            Advanced = new AdvancedSettings
            {
                ExcludedApplications = new List<string>(),
                MultiMonitorSupport = false,
                TransitionUpdateIntervalMs = 30000,
                UseHardwareAcceleration = true,
                LogLevel = LogLevel.Information
            }
        };
    }

    /// <summary>
    /// Merges loaded configuration with defaults to ensure all properties exist
    /// </summary>
    private static AppConfiguration MergeWithDefaults(AppConfiguration loaded)
    {
        var defaults = CreateDefaultConfiguration();

        return new AppConfiguration
        {
            General = new GeneralSettings
            {
                AutoStart = loaded.General?.AutoStart ?? defaults.General.AutoStart,
                MinimizeToTray = loaded.General?.MinimizeToTray ?? defaults.General.MinimizeToTray,
                CheckForUpdates = loaded.General?.CheckForUpdates ?? defaults.General.CheckForUpdates,
                Language = loaded.General?.Language ?? defaults.General.Language,
                GlobalHotkey = loaded.General?.GlobalHotkey ?? defaults.General.GlobalHotkey
            },
            Location = new LocationSettings
            {
                Method = loaded.Location?.Method ?? defaults.Location.Method,
                ManualLatitude = loaded.Location?.ManualLatitude,
                ManualLongitude = loaded.Location?.ManualLongitude,
                ManualCity = loaded.Location?.ManualCity,
                UpdateFrequency = loaded.Location?.UpdateFrequency ?? defaults.Location.UpdateFrequency,
                AllowIpLocation = loaded.Location?.AllowIpLocation ?? defaults.Location.AllowIpLocation
            },
            Notifications = new NotificationSettings
            {
                Enabled = loaded.Notifications?.Enabled ?? defaults.Notifications.Enabled,
                Level = loaded.Notifications?.Level ?? defaults.Notifications.Level,
                QuietHoursStart = loaded.Notifications?.QuietHoursStart ?? defaults.Notifications.QuietHoursStart,
                QuietHoursEnd = loaded.Notifications?.QuietHoursEnd ?? defaults.Notifications.QuietHoursEnd,
                ShowTransitionNotifications = loaded.Notifications?.ShowTransitionNotifications ?? defaults.Notifications.ShowTransitionNotifications,
                ShowSleepReminders = loaded.Notifications?.ShowSleepReminders ?? defaults.Notifications.ShowSleepReminders
            },
            Advanced = new AdvancedSettings
            {
                ExcludedApplications = loaded.Advanced?.ExcludedApplications ?? defaults.Advanced.ExcludedApplications,
                MultiMonitorSupport = loaded.Advanced?.MultiMonitorSupport ?? defaults.Advanced.MultiMonitorSupport,
                TransitionUpdateIntervalMs = loaded.Advanced?.TransitionUpdateIntervalMs ?? defaults.Advanced.TransitionUpdateIntervalMs,
                UseHardwareAcceleration = loaded.Advanced?.UseHardwareAcceleration ?? defaults.Advanced.UseHardwareAcceleration,
                LogLevel = loaded.Advanced?.LogLevel ?? defaults.Advanced.LogLevel
            },
            LastModified = loaded.LastModified
        };
    }

    /// <summary>
    /// Creates a deep clone of configuration
    /// </summary>
    private static AppConfiguration CloneConfiguration(AppConfiguration source)
    {
        var json = JsonSerializer.Serialize(source, GetJsonOptions());
        return JsonSerializer.Deserialize<AppConfiguration>(json, GetJsonOptions())!;
    }

    /// <summary>
    /// Gets a value from configuration using dot notation key path
    /// </summary>
    private static T? GetValueFromConfiguration<T>(AppConfiguration config, string key)
    {
        var parts = key.Split('.');
        object? current = config;

        foreach (var part in parts)
        {
            if (current == null) return default;

            var property = current.GetType().GetProperty(part);
            if (property == null) return default;

            current = property.GetValue(current);
        }

        if (current is T value)
            return value;

        if (current != null && typeof(T) != current.GetType())
        {
            try
            {
                return (T)Convert.ChangeType(current, typeof(T));
            }
            catch
            {
                return default;
            }
        }

        return default;
    }

    /// <summary>
    /// Sets a value in configuration using dot notation key path
    /// </summary>
    private static void SetValueInConfiguration<T>(AppConfiguration config, string key, T value)
    {
        var parts = key.Split('.');
        if (parts.Length == 0) return;

        object current = config;

        // Navigate to the parent object
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var property = current.GetType().GetProperty(parts[i]);
            if (property == null) return;

            var nextValue = property.GetValue(current);
            if (nextValue == null) return;

            current = nextValue;
        }

        // Set the final property
        var finalProperty = current.GetType().GetProperty(parts[^1]);
        if (finalProperty != null && finalProperty.CanWrite)
        {
            finalProperty.SetValue(current, value);
        }
    }

    /// <summary>
    /// Gets JSON serialization options
    /// </summary>
    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}

/// <summary>
/// JSON converter for enums to handle string serialization
/// </summary>
public class JsonStringEnumConverter : JsonConverter<Enum>
{
    public override Enum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var enumText = reader.GetString();
            if (Enum.TryParse(typeToConvert, enumText, true, out var result))
            {
                return (Enum)result;
            }
        }

        // Return default value if parsing fails
        return (Enum)Activator.CreateInstance(typeToConvert)!;
    }

    public override void Write(Utf8JsonWriter writer, Enum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
