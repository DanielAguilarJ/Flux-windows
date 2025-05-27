using ChronoGuard.Domain.Configuration;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChronoGuard.Infrastructure.Services;

/// <summary>
/// Service for persisting and loading ChronoGuard configuration settings
/// Provides secure storage, backup/restore, and configuration validation
/// </summary>
public class ConfigurationPersistenceService : IConfigurationPersistenceService
{
    private readonly ILogger<ConfigurationPersistenceService> _logger;
    private readonly string _configurationDirectory;
    private readonly string _backupDirectory;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    // Configuration file names
    private const string MAIN_CONFIG_FILE = "chronoguard_config.json";
    private const string ADVANCED_CONFIG_FILE = "advanced_color_config.json";
    private const string MONITOR_PROFILES_FILE = "monitor_profiles.json";
    private const string USER_PREFERENCES_FILE = "user_preferences.json";
    private const string PERFORMANCE_CONFIG_FILE = "performance_config.json";
    private const string LOCATION_CONFIG_FILE = "location_config.json";

    public ConfigurationPersistenceService(ILogger<ConfigurationPersistenceService> logger)
    {
        _logger = logger;
        
        // Use AppData\Local for configuration storage
        var appDataLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _configurationDirectory = Path.Combine(appDataLocal, "ChronoGuard", "Configuration");
        _backupDirectory = Path.Combine(_configurationDirectory, "Backups");
        
        EnsureDirectoriesExist();
    }

    /// <summary>
    /// Save main ChronoGuard configuration
    /// </summary>
    public async Task SaveMainConfigurationAsync(ChronoGuardConfiguration configuration)
    {
        await _fileLock.WaitAsync();
        try
        {
            _logger.LogInformation("Saving main configuration");
            
            var configPath = Path.Combine(_configurationDirectory, MAIN_CONFIG_FILE);
            await SaveConfigurationAsync(configuration, configPath);
            
            // Create backup
            await CreateBackupAsync(MAIN_CONFIG_FILE);
            
            _logger.LogInformation("Main configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save main configuration");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Load main ChronoGuard configuration
    /// </summary>
    public async Task<ChronoGuardConfiguration?> LoadMainConfigurationAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var configPath = Path.Combine(_configurationDirectory, MAIN_CONFIG_FILE);
            
            if (!File.Exists(configPath))
            {
                _logger.LogInformation("Main configuration file not found, creating default configuration");
                return CreateDefaultMainConfiguration();
            }

            var configuration = await LoadConfigurationAsync<ChronoGuardConfiguration>(configPath);
            
            if (configuration != null)
            {
                ValidateMainConfiguration(configuration);
                _logger.LogInformation("Main configuration loaded successfully");
            }
            
            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load main configuration, attempting to restore from backup");
            return await RestoreFromBackupAsync<ChronoGuardConfiguration>(MAIN_CONFIG_FILE) ?? CreateDefaultMainConfiguration();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Save advanced color management configuration
    /// </summary>
    public async Task SaveAdvancedColorConfigurationAsync(AdvancedColorManagementConfig configuration)
    {
        await _fileLock.WaitAsync();
        try
        {
            _logger.LogInformation("Saving advanced color configuration");
            
            var configPath = Path.Combine(_configurationDirectory, ADVANCED_CONFIG_FILE);
            await SaveConfigurationAsync(configuration, configPath);
            await CreateBackupAsync(ADVANCED_CONFIG_FILE);
            
            _logger.LogInformation("Advanced color configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save advanced color configuration");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Load advanced color management configuration
    /// </summary>
    public async Task<AdvancedColorManagementConfig?> LoadAdvancedColorConfigurationAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var configPath = Path.Combine(_configurationDirectory, ADVANCED_CONFIG_FILE);
            
            if (!File.Exists(configPath))
            {
                _logger.LogInformation("Advanced color configuration file not found, creating default");
                return new AdvancedColorManagementConfig();
            }

            var configuration = await LoadConfigurationAsync<AdvancedColorManagementConfig>(configPath);
            _logger.LogInformation("Advanced color configuration loaded successfully");
            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load advanced color configuration");
            return await RestoreFromBackupAsync<AdvancedColorManagementConfig>(ADVANCED_CONFIG_FILE) ?? new AdvancedColorManagementConfig();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Save monitor profiles
    /// </summary>
    public async Task SaveMonitorProfilesAsync(Dictionary<string, MonitorColorProfile> profiles)
    {
        await _fileLock.WaitAsync();
        try
        {
            _logger.LogInformation("Saving monitor profiles for {Count} monitors", profiles.Count);
            
            var configPath = Path.Combine(_configurationDirectory, MONITOR_PROFILES_FILE);
            await SaveConfigurationAsync(profiles, configPath);
            await CreateBackupAsync(MONITOR_PROFILES_FILE);
            
            _logger.LogInformation("Monitor profiles saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save monitor profiles");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Load monitor profiles
    /// </summary>
    public async Task<Dictionary<string, MonitorColorProfile>?> LoadMonitorProfilesAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var configPath = Path.Combine(_configurationDirectory, MONITOR_PROFILES_FILE);
            
            if (!File.Exists(configPath))
            {
                _logger.LogInformation("Monitor profiles file not found, returning empty dictionary");
                return new Dictionary<string, MonitorColorProfile>();
            }

            var profiles = await LoadConfigurationAsync<Dictionary<string, MonitorColorProfile>>(configPath);
            _logger.LogInformation("Monitor profiles loaded successfully: {Count} profiles", profiles?.Count ?? 0);
            return profiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load monitor profiles");
            return await RestoreFromBackupAsync<Dictionary<string, MonitorColorProfile>>(MONITOR_PROFILES_FILE) ?? new Dictionary<string, MonitorColorProfile>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Save user preferences
    /// </summary>
    public async Task SaveUserPreferencesAsync(UserPreferences preferences)
    {
        await _fileLock.WaitAsync();
        try
        {
            _logger.LogInformation("Saving user preferences");
            
            var configPath = Path.Combine(_configurationDirectory, USER_PREFERENCES_FILE);
            await SaveConfigurationAsync(preferences, configPath);
            await CreateBackupAsync(USER_PREFERENCES_FILE);
            
            _logger.LogInformation("User preferences saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save user preferences");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Load user preferences
    /// </summary>
    public async Task<UserPreferences?> LoadUserPreferencesAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var configPath = Path.Combine(_configurationDirectory, USER_PREFERENCES_FILE);
            
            if (!File.Exists(configPath))
            {
                _logger.LogInformation("User preferences file not found, creating default");
                return CreateDefaultUserPreferences();
            }

            var preferences = await LoadConfigurationAsync<UserPreferences>(configPath);
            _logger.LogInformation("User preferences loaded successfully");
            return preferences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load user preferences");
            return await RestoreFromBackupAsync<UserPreferences>(USER_PREFERENCES_FILE) ?? CreateDefaultUserPreferences();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Save performance configuration
    /// </summary>
    public async Task SavePerformanceConfigurationAsync(PerformanceConfig configuration)
    {
        await _fileLock.WaitAsync();
        try
        {
            _logger.LogInformation("Saving performance configuration");
            
            var configPath = Path.Combine(_configurationDirectory, PERFORMANCE_CONFIG_FILE);
            await SaveConfigurationAsync(configuration, configPath);
            await CreateBackupAsync(PERFORMANCE_CONFIG_FILE);
            
            _logger.LogInformation("Performance configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save performance configuration");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Load performance configuration
    /// </summary>
    public async Task<PerformanceConfig?> LoadPerformanceConfigurationAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var configPath = Path.Combine(_configurationDirectory, PERFORMANCE_CONFIG_FILE);
            
            if (!File.Exists(configPath))
            {
                _logger.LogInformation("Performance configuration file not found, creating default");
                return new PerformanceConfig();
            }

            var configuration = await LoadConfigurationAsync<PerformanceConfig>(configPath);
            _logger.LogInformation("Performance configuration loaded successfully");
            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load performance configuration");
            return await RestoreFromBackupAsync<PerformanceConfig>(PERFORMANCE_CONFIG_FILE) ?? new PerformanceConfig();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Save location configuration
    /// </summary>
    public async Task SaveLocationConfigurationAsync(GeographicLocation location)
    {
        await _fileLock.WaitAsync();
        try
        {
            _logger.LogInformation("Saving location configuration");
            
            var configPath = Path.Combine(_configurationDirectory, LOCATION_CONFIG_FILE);
            await SaveConfigurationAsync(location, configPath);
            await CreateBackupAsync(LOCATION_CONFIG_FILE);
            
            _logger.LogInformation("Location configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save location configuration");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Load location configuration
    /// </summary>
    public async Task<GeographicLocation?> LoadLocationConfigurationAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var configPath = Path.Combine(_configurationDirectory, LOCATION_CONFIG_FILE);
            
            if (!File.Exists(configPath))
            {
                _logger.LogInformation("Location configuration file not found");
                return null;
            }

            var location = await LoadConfigurationAsync<GeographicLocation>(configPath);
            _logger.LogInformation("Location configuration loaded successfully");
            return location;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load location configuration");
            return await RestoreFromBackupAsync<GeographicLocation>(LOCATION_CONFIG_FILE);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Export all configurations to a backup file
    /// </summary>
    public async Task<string> ExportConfigurationAsync(string? filePath = null)
    {
        await _fileLock.WaitAsync();
        try
        {
            filePath ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"ChronoGuard_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");

            _logger.LogInformation("Exporting configuration to {FilePath}", filePath);

            var exportData = new ConfigurationExport
            {
                ExportDate = DateTime.Now,
                Version = GetApplicationVersion(),
                MainConfiguration = await LoadMainConfigurationAsync(),
                AdvancedColorConfiguration = await LoadAdvancedColorConfigurationAsync(),
                MonitorProfiles = await LoadMonitorProfilesAsync(),
                UserPreferences = await LoadUserPreferencesAsync(),
                PerformanceConfiguration = await LoadPerformanceConfigurationAsync(),
                LocationConfiguration = await LoadLocationConfigurationAsync()
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(filePath, json);
            
            _logger.LogInformation("Configuration exported successfully to {FilePath}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export configuration");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Import configurations from a backup file
    /// </summary>
    public async Task ImportConfigurationAsync(string filePath)
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Configuration file not found: {filePath}");

            _logger.LogInformation("Importing configuration from {FilePath}", filePath);

            var json = await File.ReadAllTextAsync(filePath);
            var importData = JsonSerializer.Deserialize<ConfigurationExport>(json, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            if (importData == null)
                throw new InvalidDataException("Invalid configuration file format");

            // Validate version compatibility
            if (!IsVersionCompatible(importData.Version))
            {
                _logger.LogWarning("Configuration version {Version} may not be compatible with current version", importData.Version);
            }

            // Import each configuration section
            if (importData.MainConfiguration != null)
                await SaveMainConfigurationAsync(importData.MainConfiguration);

            if (importData.AdvancedColorConfiguration != null)
                await SaveAdvancedColorConfigurationAsync(importData.AdvancedColorConfiguration);

            if (importData.MonitorProfiles != null)
                await SaveMonitorProfilesAsync(importData.MonitorProfiles);

            if (importData.UserPreferences != null)
                await SaveUserPreferencesAsync(importData.UserPreferences);

            if (importData.PerformanceConfiguration != null)
                await SavePerformanceConfigurationAsync(importData.PerformanceConfiguration);

            if (importData.LocationConfiguration != null)
                await SaveLocationConfigurationAsync(importData.LocationConfiguration);

            _logger.LogInformation("Configuration imported successfully from {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import configuration from {FilePath}", filePath);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Reset all configurations to defaults
    /// </summary>
    public async Task ResetToDefaultsAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            _logger.LogInformation("Resetting all configurations to defaults");

            // Create backup before reset
            await ExportConfigurationAsync(Path.Combine(_backupDirectory, $"PreReset_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json"));

            // Reset to defaults
            await SaveMainConfigurationAsync(CreateDefaultMainConfiguration());
            await SaveAdvancedColorConfigurationAsync(new AdvancedColorManagementConfig());
            await SaveMonitorProfilesAsync(new Dictionary<string, MonitorColorProfile>());
            await SaveUserPreferencesAsync(CreateDefaultUserPreferences());
            await SavePerformanceConfigurationAsync(new PerformanceConfig());

            // Remove location configuration
            var locationPath = Path.Combine(_configurationDirectory, LOCATION_CONFIG_FILE);
            if (File.Exists(locationPath))
                File.Delete(locationPath);

            _logger.LogInformation("All configurations reset to defaults");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset configurations to defaults");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Get configuration file paths for debugging
    /// </summary>
    public Dictionary<string, string> GetConfigurationPaths()
    {
        return new Dictionary<string, string>
        {
            ["Main Configuration"] = Path.Combine(_configurationDirectory, MAIN_CONFIG_FILE),
            ["Advanced Color"] = Path.Combine(_configurationDirectory, ADVANCED_CONFIG_FILE),
            ["Monitor Profiles"] = Path.Combine(_configurationDirectory, MONITOR_PROFILES_FILE),
            ["User Preferences"] = Path.Combine(_configurationDirectory, USER_PREFERENCES_FILE),
            ["Performance"] = Path.Combine(_configurationDirectory, PERFORMANCE_CONFIG_FILE),
            ["Location"] = Path.Combine(_configurationDirectory, LOCATION_CONFIG_FILE),
            ["Configuration Directory"] = _configurationDirectory,
            ["Backup Directory"] = _backupDirectory
        };
    }

    #region Private Methods

    /// <summary>
    /// Ensure configuration directories exist
    /// </summary>
    private void EnsureDirectoriesExist()
    {
        try
        {
            Directory.CreateDirectory(_configurationDirectory);
            Directory.CreateDirectory(_backupDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create configuration directories");
            throw;
        }
    }

    /// <summary>
    /// Generic method to save configuration
    /// </summary>
    private async Task SaveConfigurationAsync<T>(T configuration, string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(configuration, options);
        
        // Write to temporary file first, then rename (atomic operation)
        var tempFile = filePath + ".tmp";
        await File.WriteAllTextAsync(tempFile, json);
        
        if (File.Exists(filePath))
            File.Delete(filePath);
            
        File.Move(tempFile, filePath);
    }

    /// <summary>
    /// Generic method to load configuration
    /// </summary>
    private async Task<T?> LoadConfigurationAsync<T>(string filePath) where T : class
    {
        var json = await File.ReadAllTextAsync(filePath);
        
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Deserialize<T>(json, options);
    }

    /// <summary>
    /// Create backup of configuration file
    /// </summary>
    private async Task CreateBackupAsync(string configFileName)
    {
        try
        {
            var sourceFile = Path.Combine(_configurationDirectory, configFileName);
            if (!File.Exists(sourceFile)) return;

            var backupFileName = $"{Path.GetFileNameWithoutExtension(configFileName)}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var backupFile = Path.Combine(_backupDirectory, backupFileName);

            await Task.Run(() => File.Copy(sourceFile, backupFile, true));

            // Clean up old backups (keep last 10)
            await CleanupOldBackupsAsync(Path.GetFileNameWithoutExtension(configFileName));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create backup for {ConfigFileName}", configFileName);
        }
    }

    /// <summary>
    /// Restore configuration from backup
    /// </summary>
    private async Task<T?> RestoreFromBackupAsync<T>(string configFileName) where T : class
    {
        try
        {
            var configBaseName = Path.GetFileNameWithoutExtension(configFileName);
            var backupFiles = Directory.GetFiles(_backupDirectory, $"{configBaseName}_*.json")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();

            foreach (var backupFile in backupFiles)
            {
                try
                {
                    var configuration = await LoadConfigurationAsync<T>(backupFile);
                    if (configuration != null)
                    {
                        _logger.LogInformation("Successfully restored {ConfigFileName} from backup {BackupFile}", configFileName, backupFile);
                        return configuration;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to restore from backup file {BackupFile}", backupFile);
                }
            }

            _logger.LogWarning("No valid backup found for {ConfigFileName}", configFileName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore {ConfigFileName} from backup", configFileName);
            return null;
        }
    }

    /// <summary>
    /// Clean up old backup files
    /// </summary>
    private async Task CleanupOldBackupsAsync(string configBaseName)
    {
        await Task.Run(() =>
        {
            try
            {
                var backupFiles = Directory.GetFiles(_backupDirectory, $"{configBaseName}_*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .Skip(10) // Keep latest 10 backups
                    .ToList();

                foreach (var file in backupFiles)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to delete old backup file {FileName}", file.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup old backups for {ConfigBaseName}", configBaseName);
            }
        });
    }

    /// <summary>
    /// Create default main configuration
    /// </summary>
    private ChronoGuardConfiguration CreateDefaultMainConfiguration()
    {
        return new ChronoGuardConfiguration
        {
            IsEnabled = true,
            DayColorTemperature = 6500,
            NightColorTemperature = 3000,
            TransitionDurationMinutes = 60,
            SunsetOffset = TimeSpan.Zero,
            SunriseOffset = TimeSpan.Zero,
            EnableAutomaticLocation = true,
            EnableTransitions = true,
            EnableAtStartup = false,
            MinimizeToTray = true,
            ShowNotifications = true,
            EnableHotkeys = true,
            LastModified = DateTime.Now
        };
    }

    /// <summary>
    /// Create default user preferences
    /// </summary>
    private UserPreferences CreateDefaultUserPreferences()
    {
        return new UserPreferences
        {
            Language = "en-US",
            Theme = "System",
            EnableAnimations = true,
            ShowAdvancedOptions = false,
            AutoCheckUpdates = true,
            TelemetryEnabled = false,
            FirstRun = true,
            LastVersion = GetApplicationVersion(),
            UsageStatistics = new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Validate main configuration
    /// </summary>
    private void ValidateMainConfiguration(ChronoGuardConfiguration configuration)
    {
        if (configuration.DayColorTemperature < 1000 || configuration.DayColorTemperature > 10000)
            configuration.DayColorTemperature = 6500;

        if (configuration.NightColorTemperature < 1000 || configuration.NightColorTemperature > 10000)
            configuration.NightColorTemperature = 3000;

        if (configuration.TransitionDurationMinutes < 1 || configuration.TransitionDurationMinutes > 480)
            configuration.TransitionDurationMinutes = 60;
    }

    /// <summary>
    /// Get application version
    /// </summary>
    private string GetApplicationVersion()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0.0";
        }
        catch
        {
            return "1.0.0.0";
        }
    }

    /// <summary>
    /// Check if configuration version is compatible
    /// </summary>
    private bool IsVersionCompatible(string? version)
    {
        if (string.IsNullOrEmpty(version)) return false;

        try
        {
            var importVersion = new Version(version);
            var currentVersion = new Version(GetApplicationVersion());
            
            // Compatible if major version matches
            return importVersion.Major == currentVersion.Major;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region IConfigurationPersistenceService Implementation

    /// <summary>
    /// Generic method to load configuration of any type
    /// </summary>
    public async Task<T?> LoadConfigurationAsync<T>() where T : class
    {
        var typeName = typeof(T).Name;
        string fileName = typeName switch
        {
            nameof(ChronoGuardConfiguration) => MAIN_CONFIG_FILE,
            nameof(AdvancedColorManagementConfig) => ADVANCED_CONFIG_FILE,
            nameof(UserPreferences) => USER_PREFERENCES_FILE,
            nameof(PerformanceConfig) => PERFORMANCE_CONFIG_FILE,
            nameof(GeographicLocation) => LOCATION_CONFIG_FILE,
            _ => $"{typeName.ToLowerInvariant()}_config.json"
        };
        
        var filePath = Path.Combine(_configurationDirectory, fileName);
        if (!File.Exists(filePath))
        {
            return null;
        }

        return await LoadConfigurationAsync<T>(filePath);
    }

    /// <summary>
    /// Generic method to save configuration of any type
    /// </summary>
    public async Task SaveConfigurationAsync<T>(T configuration) where T : class
    {
        var typeName = typeof(T).Name;
        string fileName = typeName switch
        {
            nameof(ChronoGuardConfiguration) => MAIN_CONFIG_FILE,
            nameof(AdvancedColorManagementConfig) => ADVANCED_CONFIG_FILE,
            nameof(UserPreferences) => USER_PREFERENCES_FILE,
            nameof(PerformanceConfig) => PERFORMANCE_CONFIG_FILE,
            nameof(GeographicLocation) => LOCATION_CONFIG_FILE,
            _ => $"{typeName.ToLowerInvariant()}_config.json"
        };
        
        var filePath = Path.Combine(_configurationDirectory, fileName);
        await SaveConfigurationAsync(configuration, filePath);
    }

    /// <summary>
    /// Check if configuration exists for the specified type
    /// </summary>
    public async Task<bool> ConfigurationExistsAsync<T>() where T : class
    {
        var typeName = typeof(T).Name;
        string fileName = typeName switch
        {
            nameof(ChronoGuardConfiguration) => MAIN_CONFIG_FILE,
            nameof(AdvancedColorManagementConfig) => ADVANCED_CONFIG_FILE,
            nameof(UserPreferences) => USER_PREFERENCES_FILE,
            nameof(PerformanceConfig) => PERFORMANCE_CONFIG_FILE,
            nameof(GeographicLocation) => LOCATION_CONFIG_FILE,
            _ => $"{typeName.ToLowerInvariant()}_config.json"
        };
        
        var filePath = Path.Combine(_configurationDirectory, fileName);
        return await Task.FromResult(File.Exists(filePath));
    }

    /// <summary>
    /// Delete configuration for the specified type
    /// </summary>
    public async Task DeleteConfigurationAsync<T>() where T : class
    {
        var typeName = typeof(T).Name;
        string fileName = typeName switch
        {
            nameof(ChronoGuardConfiguration) => MAIN_CONFIG_FILE,
            nameof(AdvancedColorManagementConfig) => ADVANCED_CONFIG_FILE,
            nameof(UserPreferences) => USER_PREFERENCES_FILE,
            nameof(PerformanceConfig) => PERFORMANCE_CONFIG_FILE,
            nameof(GeographicLocation) => LOCATION_CONFIG_FILE,
            _ => $"{typeName.ToLowerInvariant()}_config.json"
        };
        
        var filePath = Path.Combine(_configurationDirectory, fileName);
        if (File.Exists(filePath))
        {
            await Task.Run(() => File.Delete(filePath));
        }
    }

    /// <summary>
    /// Create backup of all configurations
    /// </summary>
    public async Task CreateBackupAsync()
    {
        await ExportConfigurationAsync(Path.Combine(_backupDirectory, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.json"));
    }

    /// <summary>
    /// Restore configuration from backup
    /// </summary>
    public async Task RestoreFromBackupAsync(string backupPath)
    {
        await ImportConfigurationAsync(backupPath);
    }

    /// <summary>
    /// Get available backup files
    /// </summary>
    public async Task<IEnumerable<string>> GetAvailableBackupsAsync()
    {
        return await Task.FromResult(
            Directory.Exists(_backupDirectory) 
                ? Directory.GetFiles(_backupDirectory, "*.json").OrderByDescending(f => f)
                : Enumerable.Empty<string>()
        );
    }

    #endregion
}

/// <summary>
/// Configuration export/import data structure
/// </summary>
public class ConfigurationExport
{
    public DateTime ExportDate { get; set; }
    public string Version { get; set; } = string.Empty;
    public ChronoGuardConfiguration? MainConfiguration { get; set; }
    public AdvancedColorManagementConfig? AdvancedColorConfiguration { get; set; }
    public Dictionary<string, MonitorColorProfile>? MonitorProfiles { get; set; }
    public UserPreferences? UserPreferences { get; set; }
    public PerformanceConfig? PerformanceConfiguration { get; set; }
    public GeographicLocation? LocationConfiguration { get; set; }
}

/// <summary>
/// Main ChronoGuard configuration
/// </summary>
public class ChronoGuardConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public int DayColorTemperature { get; set; } = 6500;
    public int NightColorTemperature { get; set; } = 3000;
    public int TransitionDurationMinutes { get; set; } = 60;
    public TimeSpan SunsetOffset { get; set; } = TimeSpan.Zero;
    public TimeSpan SunriseOffset { get; set; } = TimeSpan.Zero;
    public bool EnableAutomaticLocation { get; set; } = true;
    public bool EnableTransitions { get; set; } = true;
    public bool EnableAtStartup { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool EnableHotkeys { get; set; } = true;
    public DateTime LastModified { get; set; } = DateTime.Now;
}

/// <summary>
/// User preferences configuration
/// </summary>
public class UserPreferences
{
    public string Language { get; set; } = "en-US";
    public string Theme { get; set; } = "System";
    public bool EnableAnimations { get; set; } = true;
    public bool ShowAdvancedOptions { get; set; } = false;
    public bool AutoCheckUpdates { get; set; } = true;
    public bool TelemetryEnabled { get; set; } = false;
    public bool FirstRun { get; set; } = true;
    public string LastVersion { get; set; } = string.Empty;
    public Dictionary<string, object> UsageStatistics { get; set; } = new();
}
