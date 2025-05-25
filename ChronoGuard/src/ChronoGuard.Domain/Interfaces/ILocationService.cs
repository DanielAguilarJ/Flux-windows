using ChronoGuard.Domain.Entities;

namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Service for detecting and managing geographical location
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Gets the current location using the preferred method
    /// </summary>
    Task<Location?> GetCurrentLocationAsync();

    /// <summary>
    /// Gets location from Windows Location API
    /// </summary>
    Task<Location?> GetLocationFromWindowsApiAsync();

    /// <summary>
    /// Gets location from IP address
    /// </summary>
    Task<Location?> GetLocationFromIpAsync();

    /// <summary>
    /// Searches for a city by name
    /// </summary>
    Task<IEnumerable<Location>> SearchCitiesAsync(string query);

    /// <summary>
    /// Validates if a location has valid coordinates
    /// </summary>
    bool IsValidLocation(Location location);

    /// <summary>
    /// Event raised when location changes
    /// </summary>
    event EventHandler<Location>? LocationChanged;
}
