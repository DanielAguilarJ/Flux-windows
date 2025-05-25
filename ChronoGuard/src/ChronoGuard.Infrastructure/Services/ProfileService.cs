using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.IO;

namespace ChronoGuard.Infrastructure.Services;

/// <summary>
/// Service for managing color profiles with persistence
/// </summary>
public class ProfileService : IProfileService
{
    private readonly ILogger<ProfileService> _logger;
    private readonly string _profilesDirectory;
    private readonly string _configFilePath;
    private readonly Dictionary<string, ColorProfile> _profiles = new();
    private string? _activeProfileId;
    private readonly object _profilesLock = new();

    public event EventHandler<ColorProfile>? ActiveProfileChanged;
    public event EventHandler? ProfilesChanged;

    public ProfileService(ILogger<ProfileService> logger)
    {
        _logger = logger;
        
        // Set up profile storage paths
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var chronoGuardPath = Path.Combine(appDataPath, "ChronoGuard");
        _profilesDirectory = Path.Combine(chronoGuardPath, "Profiles");
        _configFilePath = Path.Combine(chronoGuardPath, "profiles-config.json");

        // Ensure directories exist
        Directory.CreateDirectory(_profilesDirectory);
        Directory.CreateDirectory(chronoGuardPath);

        // Load profiles on startup
        _ = Task.Run(async () =>
        {
            await InitializeBuiltInProfilesAsync();
            await LoadProfilesAsync();
            await LoadConfigurationAsync();
        });
    }

    /// <summary>
    /// Gets all available profiles
    /// </summary>
    public async Task<IEnumerable<ColorProfile>> GetProfilesAsync()
    {
        await EnsureProfilesLoadedAsync();
        
        lock (_profilesLock)
        {
            return _profiles.Values.ToList();
        }
    }

    /// <summary>
    /// Gets a profile by ID
    /// </summary>
    public async Task<ColorProfile?> GetProfileByIdAsync(string id)
    {
        await EnsureProfilesLoadedAsync();
        
        lock (_profilesLock)
        {
            return _profiles.GetValueOrDefault(id);
        }
    }

    /// <summary>
    /// Gets the currently active profile
    /// </summary>
    public async Task<ColorProfile?> GetActiveProfileAsync()
    {
        await EnsureProfilesLoadedAsync();
        
        lock (_profilesLock)
        {
            if (_activeProfileId != null && _profiles.TryGetValue(_activeProfileId, out var profile))
            {
                return profile;
            }
            
            // Return default profile if no active profile set
            return _profiles.GetValueOrDefault("classic");
        }
    }

    /// <summary>
    /// Sets the active profile
    /// </summary>
    public async Task SetActiveProfileAsync(string profileId)
    {
        await EnsureProfilesLoadedAsync();
        
        ColorProfile? profile;
        lock (_profilesLock)
        {
            if (!_profiles.TryGetValue(profileId, out profile))
            {
                throw new ArgumentException($"Profile with ID '{profileId}' not found");
            }
            
            _activeProfileId = profileId;
        }

        await SaveConfigurationAsync();
        
        _logger.LogInformation("Active profile changed to: {ProfileName}", profile.Name);
        ActiveProfileChanged?.Invoke(this, profile);
    }

    /// <summary>
    /// Saves a profile (creates new or updates existing)
    /// </summary>
    public async Task<string> SaveProfileAsync(ColorProfile profile)
    {
        if (string.IsNullOrEmpty(profile.Id))
        {
            profile.Id = Guid.NewGuid().ToString();
        }

        profile.ModifiedAt = DateTime.UtcNow;

        // Cannot modify built-in profiles directly
        if (profile.IsBuiltIn && _profiles.ContainsKey(profile.Id))
        {
            throw new InvalidOperationException("Cannot modify built-in profiles");
        }

        lock (_profilesLock)
        {
            _profiles[profile.Id] = profile;
        }

        await SaveProfileToFileAsync(profile);
        
        _logger.LogInformation("Profile saved: {ProfileName} (ID: {ProfileId})", profile.Name, profile.Id);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);

        return profile.Id;
    }

    /// <summary>
    /// Deletes a profile (cannot delete built-in profiles)
    /// </summary>
    public async Task<bool> DeleteProfileAsync(string id)
    {
        ColorProfile? profile;
        lock (_profilesLock)
        {
            if (!_profiles.TryGetValue(id, out profile))
            {
                return false;
            }

            if (profile.IsBuiltIn)
            {
                throw new InvalidOperationException("Cannot delete built-in profiles");
            }

            _profiles.Remove(id);
        }

        // Delete profile file
        var filePath = GetProfileFilePath(id);
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete profile file: {FilePath}", filePath);
            }
        }

        // If this was the active profile, switch to default
        if (_activeProfileId == id)
        {
            await SetActiveProfileAsync("classic");
        }

        _logger.LogInformation("Profile deleted: {ProfileName} (ID: {ProfileId})", profile.Name, id);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>
    /// Creates built-in profiles if they don't exist
    /// </summary>
    public async Task InitializeBuiltInProfilesAsync()
    {
        var builtInProfiles = new[]
        {
            ColorProfile.CreateClassic(),
            ColorProfile.CreateWorkNight(),
            ColorProfile.CreateMultimedia()
        };

        lock (_profilesLock)
        {
            foreach (var profile in builtInProfiles)
            {
                if (!_profiles.ContainsKey(profile.Id))
                {
                    _profiles[profile.Id] = profile;
                    _logger.LogDebug("Initialized built-in profile: {ProfileName}", profile.Name);
                }
            }
        }

        // Set default active profile if none is set
        if (_activeProfileId == null)
        {
            _activeProfileId = "classic";
        }
    }

    /// <summary>
    /// Exports a profile to a file
    /// </summary>
    public async Task<string> ExportProfileAsync(string profileId, string filePath)
    {
        var profile = await GetProfileByIdAsync(profileId);
        if (profile == null)
        {
            throw new ArgumentException($"Profile with ID '{profileId}' not found");
        }

        var exportData = new
        {
            Profile = profile,
            ExportedAt = DateTime.UtcNow,
            ExportedBy = "ChronoGuard",
            Version = "1.0"
        };

        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(filePath, json);
        
        _logger.LogInformation("Profile exported: {ProfileName} to {FilePath}", profile.Name, filePath);
        return filePath;
    }

    /// <summary>
    /// Imports a profile from a file
    /// </summary>
    public async Task<ColorProfile> ImportProfileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Profile file not found: {filePath}");
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            using var doc = JsonDocument.Parse(json);
            
            if (!doc.RootElement.TryGetProperty("profile", out var profileElement))
            {
                throw new InvalidDataException("Invalid profile file format");
            }

            var profile = JsonSerializer.Deserialize<ColorProfile>(profileElement.GetRawText(), new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (profile == null)
            {
                throw new InvalidDataException("Could not deserialize profile data");
            }

            // Ensure it's not marked as built-in and has a new ID
            profile.Id = Guid.NewGuid().ToString();
            profile.IsBuiltIn = false;
            profile.CreatedAt = DateTime.UtcNow;
            profile.ModifiedAt = DateTime.UtcNow;
            profile.Name = $"{profile.Name} (Imported)";

            await SaveProfileAsync(profile);
            
            _logger.LogInformation("Profile imported: {ProfileName} from {FilePath}", profile.Name, filePath);
            return profile;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Invalid JSON format in profile file", ex);
        }
    }

    /// <summary>
    /// Loads all profiles from disk
    /// </summary>
    private async Task LoadProfilesAsync()
    {
        try
        {
            if (!Directory.Exists(_profilesDirectory))
                return;

            var profileFiles = Directory.GetFiles(_profilesDirectory, "*.json");
            
            foreach (var filePath in profileFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    var profile = JsonSerializer.Deserialize<ColorProfile>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    if (profile != null && !profile.IsBuiltIn) // Don't load built-in profiles from files
                    {
                        lock (_profilesLock)
                        {
                            _profiles[profile.Id] = profile;
                        }
                        _logger.LogDebug("Loaded profile: {ProfileName}", profile.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load profile from file: {FilePath}", filePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading profiles from directory: {Directory}", _profilesDirectory);
        }
    }

    /// <summary>
    /// Saves a profile to disk
    /// </summary>
    private async Task SaveProfileToFileAsync(ColorProfile profile)
    {
        if (profile.IsBuiltIn)
            return; // Don't save built-in profiles to disk

        try
        {
            var filePath = GetProfileFilePath(profile.Id);
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving profile to file: {ProfileName}", profile.Name);
            throw;
        }
    }

    /// <summary>
    /// Loads configuration (active profile, etc.)
    /// </summary>
    private async Task LoadConfigurationAsync()
    {
        try
        {
            if (!File.Exists(_configFilePath))
                return;

            var json = await File.ReadAllTextAsync(_configFilePath);
            using var doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.TryGetProperty("activeProfileId", out var activeProfileElement))
            {
                _activeProfileId = activeProfileElement.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load profile configuration");
        }
    }

    /// <summary>
    /// Saves configuration to disk
    /// </summary>
    private async Task SaveConfigurationAsync()
    {
        try
        {
            var config = new
            {
                ActiveProfileId = _activeProfileId,
                LastUpdated = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(_configFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving profile configuration");
        }
    }

    /// <summary>
    /// Gets the file path for a profile
    /// </summary>
    private string GetProfileFilePath(string profileId)
    {
        return Path.Combine(_profilesDirectory, $"{profileId}.json");
    }

    /// <summary>
    /// Ensures profiles are loaded (idempotent)
    /// </summary>
    private async Task EnsureProfilesLoadedAsync()
    {
        // This is a simple check - in a real implementation you might want
        // to track loading state more precisely
        if (_profiles.Count == 0)
        {
            await InitializeBuiltInProfilesAsync();
            await LoadProfilesAsync();
        }
    }
}
