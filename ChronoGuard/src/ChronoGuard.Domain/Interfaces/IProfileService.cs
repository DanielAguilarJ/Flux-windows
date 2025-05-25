using ChronoGuard.Domain.Entities;
namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Service for managing color profiles
/// </summary>
public interface IProfileService
{
    /// <summary>
    /// Gets all available profiles
    /// </summary>
    Task<IEnumerable<ColorProfile>> GetProfilesAsync();

    /// <summary>
    /// Gets a profile by ID
    /// </summary>
    Task<ColorProfile?> GetProfileByIdAsync(string id);

    /// <summary>
    /// Gets the currently active profile
    /// </summary>
    Task<ColorProfile?> GetActiveProfileAsync();

    /// <summary>
    /// Sets the active profile
    /// </summary>
    Task SetActiveProfileAsync(string profileId);

    /// <summary>
    /// Saves a profile (creates new or updates existing)
    /// </summary>
    Task<string> SaveProfileAsync(ColorProfile profile);

    /// <summary>
    /// Deletes a profile (cannot delete built-in profiles)
    /// </summary>
    Task<bool> DeleteProfileAsync(string id);

    /// <summary>
    /// Creates built-in profiles if they don't exist
    /// </summary>
    Task InitializeBuiltInProfilesAsync();

    /// <summary>
    /// Exports a profile to a file
    /// </summary>
    Task<string> ExportProfileAsync(string profileId, string filePath);

    /// <summary>
    /// Imports a profile from a file
    /// </summary>
    Task<ColorProfile> ImportProfileAsync(string filePath);

    /// <summary>
    /// Event raised when the active profile changes
    /// </summary>
    event EventHandler<ColorProfile>? ActiveProfileChanged;

    /// <summary>
    /// Event raised when profiles are modified
    /// </summary>
    event EventHandler? ProfilesChanged;
}
