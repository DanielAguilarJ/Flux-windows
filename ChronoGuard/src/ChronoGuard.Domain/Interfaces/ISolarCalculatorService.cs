using ChronoGuard.Domain.Entities;

namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Service for calculating solar times (sunrise/sunset)
/// </summary>
public interface ISolarCalculatorService
{
    /// <summary>
    /// Calculates sunrise and sunset times for a given location and date
    /// </summary>
    Task<SolarTimes> CalculateSolarTimesAsync(Location location, DateTime date);

    /// <summary>
    /// Gets solar times for today at the current location
    /// </summary>
    Task<SolarTimes?> GetTodaySolarTimesAsync();

    /// <summary>
    /// Calculates solar times for multiple days
    /// </summary>
    Task<IEnumerable<SolarTimes>> CalculateSolarTimesRangeAsync(Location location, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Checks if the location is in a polar region where normal solar calculations don't apply
    /// </summary>
    bool IsPolarRegion(Location location, DateTime date);
}
