using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ChronoGuard.Infrastructure.Services;

/// <summary>
/// Service for calculating solar times using astronomical algorithms
/// </summary>
public class SolarCalculatorService : ISolarCalculatorService
{
    private readonly ILogger<SolarCalculatorService> _logger;
    private readonly ILocationService _locationService;

    public SolarCalculatorService(ILogger<SolarCalculatorService> logger, ILocationService locationService)
    {
        _logger = logger;
        _locationService = locationService;
    }

    public async Task<SolarTimes> CalculateSolarTimesAsync(Location location, DateTime date)
    {
        try
        {
            var (sunrise, sunset) = CalculateSunriseSunset(location.Latitude, location.Longitude, date);
            return new SolarTimes(sunrise, sunset, date, location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating solar times for {Location} on {Date}", location, date);
            throw;
        }
    }

    public async Task<SolarTimes?> GetTodaySolarTimesAsync()
    {
        var location = await _locationService.GetCurrentLocationAsync();
        if (location == null)
        {
            _logger.LogWarning("Cannot calculate solar times: no location available");
            return null;
        }

        return await CalculateSolarTimesAsync(location, DateTime.Today);
    }

    public async Task<IEnumerable<SolarTimes>> CalculateSolarTimesRangeAsync(Location location, DateTime startDate, DateTime endDate)
    {
        var results = new List<SolarTimes>();
        var currentDate = startDate.Date;

        while (currentDate <= endDate.Date)
        {
            try
            {
                var solarTimes = await CalculateSolarTimesAsync(location, currentDate);
                results.Add(solarTimes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate solar times for {Date}", currentDate);
            }

            currentDate = currentDate.AddDays(1);
        }

        return results;
    }

    public bool IsPolarRegion(Location location, DateTime date)
    {
        // Simplified polar region detection
        var absLatitude = Math.Abs(location.Latitude);
        
        // Arctic/Antarctic circles are approximately at 66.5 degrees
        if (absLatitude < 66.5) return false;

        // Check if sun doesn't rise or set (polar day/night)
        try
        {
            var (sunrise, sunset) = CalculateSunriseSunset(location.Latitude, location.Longitude, date);
            return sunrise == sunset; // If they're equal, sun doesn't rise/set
        }
        catch
        {
            return true; // If calculation fails, assume polar region
        }
    }

    /// <summary>
    /// Calculates sunrise and sunset times using simplified astronomical formulas
    /// Based on NOAA Solar Calculator algorithms
    /// </summary>
    private (DateTime sunrise, DateTime sunset) CalculateSunriseSunset(double latitude, double longitude, DateTime date)
    {
        const double zenith = 90.833; // Civil twilight zenith angle

        var dayOfYear = date.DayOfYear;
        var lngHour = longitude / 15.0;

        // Approximate times
        var tSunrise = dayOfYear + ((6 - lngHour) / 24);
        var tSunset = dayOfYear + ((18 - lngHour) / 24);

        // Sun's mean anomaly
        var mSunrise = (0.9856 * tSunrise) - 3.289;
        var mSunset = (0.9856 * tSunset) - 3.289;

        // Sun's true longitude
        var lSunrise = mSunrise + (1.916 * Math.Sin(ToRadians(mSunrise))) + (0.020 * Math.Sin(ToRadians(2 * mSunrise))) + 282.634;
        var lSunset = mSunset + (1.916 * Math.Sin(ToRadians(mSunset))) + (0.020 * Math.Sin(ToRadians(2 * mSunset))) + 282.634;

        // Normalize longitude
        lSunrise = NormalizeDegrees(lSunrise);
        lSunset = NormalizeDegrees(lSunset);

        // Sun's right ascension
        var raSunrise = ToDegrees(Math.Atan(0.91764 * Math.Tan(ToRadians(lSunrise))));
        var raSunset = ToDegrees(Math.Atan(0.91764 * Math.Tan(ToRadians(lSunset))));

        raSunrise = NormalizeDegrees(raSunrise);
        raSunset = NormalizeDegrees(raSunset);

        // Right ascension value needs to be in the same quadrant as L
        var lQuadSunrise = (Math.Floor(lSunrise / 90)) * 90;
        var raQuadSunrise = (Math.Floor(raSunrise / 90)) * 90;
        raSunrise = raSunrise + (lQuadSunrise - raQuadSunrise);

        var lQuadSunset = (Math.Floor(lSunset / 90)) * 90;
        var raQuadSunset = (Math.Floor(raSunset / 90)) * 90;
        raSunset = raSunset + (lQuadSunset - raQuadSunset);

        // Right ascension value needs to be converted into hours
        raSunrise = raSunrise / 15;
        raSunset = raSunset / 15;

        // Sun's declination
        var sinDecSunrise = 0.39782 * Math.Sin(ToRadians(lSunrise));
        var cosDecSunrise = Math.Cos(Math.Asin(sinDecSunrise));

        var sinDecSunset = 0.39782 * Math.Sin(ToRadians(lSunset));
        var cosDecSunset = Math.Cos(Math.Asin(sinDecSunset));

        // Sun's local hour angle
        var cosHSunrise = (Math.Cos(ToRadians(zenith)) - (sinDecSunrise * Math.Sin(ToRadians(latitude)))) / (cosDecSunrise * Math.Cos(ToRadians(latitude)));
        var cosHSunset = (Math.Cos(ToRadians(zenith)) - (sinDecSunset * Math.Sin(ToRadians(latitude)))) / (cosDecSunset * Math.Cos(ToRadians(latitude)));

        // Check for polar day/night
        if (cosHSunrise > 1)
        {
            // Polar night
            var polarTime = date.Date.AddHours(12);
            return (polarTime, polarTime);
        }
        if (cosHSunrise < -1)
        {
            // Polar day
            var polarTime = date.Date.AddHours(12);
            return (polarTime, polarTime);
        }

        // Finalize the calculations
        var hSunrise = 360 - ToDegrees(Math.Acos(cosHSunrise));
        var hSunset = ToDegrees(Math.Acos(cosHSunset));

        hSunrise = hSunrise / 15;
        hSunset = hSunset / 15;

        // Calculate local mean time of rising/setting
        var tSunriseLocal = hSunrise + raSunrise - (0.06571 * tSunrise) - 6.622;
        var tSunsetLocal = hSunset + raSunset - (0.06571 * tSunset) - 6.622;

        // Adjust back to UTC
        var utcSunrise = tSunriseLocal - lngHour;
        var utcSunset = tSunsetLocal - lngHour;

        // Normalize to 0-24 hours
        utcSunrise = NormalizeHours(utcSunrise);
        utcSunset = NormalizeHours(utcSunset);

        // Convert to DateTime
        var sunrise = date.Date.AddHours(utcSunrise).ToLocalTime();
        var sunset = date.Date.AddHours(utcSunset).ToLocalTime();

        // Ensure sunrise is before sunset
        if (sunrise >= sunset)
        {
            sunset = sunset.AddDays(1);
        }

        return (sunrise, sunset);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;

    private static double NormalizeDegrees(double degrees)
    {
        while (degrees < 0) degrees += 360;
        while (degrees >= 360) degrees -= 360;
        return degrees;
    }

    private static double NormalizeHours(double hours)
    {
        while (hours < 0) hours += 24;
        while (hours >= 24) hours -= 24;
        return hours;
    }
}
