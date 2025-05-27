namespace ChronoGuard.Domain.Entities;

/// <summary>
/// Represents a geographic location with coordinates and metadata
/// </summary>
public class GeographicLocation
{
    /// <summary>
    /// Latitude in decimal degrees (-90 to +90)
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude in decimal degrees (-180 to +180)
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Altitude in meters above sea level
    /// </summary>
    public double? Altitude { get; set; }

    /// <summary>
    /// Accuracy of the location measurement in meters
    /// </summary>
    public double? Accuracy { get; set; }

    /// <summary>
    /// City name if available
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// State/province name if available
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Country name if available
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Country code (ISO 3166-1 alpha-2)
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    /// Timezone identifier (e.g., "America/New_York")
    /// </summary>
    public string? TimeZone { get; set; }

    /// <summary>
    /// Source of the location data
    /// </summary>
    public LocationSource Source { get; set; } = LocationSource.Unknown;

    /// <summary>
    /// When this location was obtained
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this location was manually set by the user
    /// </summary>
    public bool IsManuallySet { get; set; }

    /// <summary>
    /// Creates a new geographic location
    /// </summary>
    public GeographicLocation() { }

    /// <summary>
    /// Creates a new geographic location with coordinates
    /// </summary>
    public GeographicLocation(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// Creates a new geographic location with full details
    /// </summary>
    public GeographicLocation(double latitude, double longitude, string? city, string? country, LocationSource source = LocationSource.Unknown)
    {
        Latitude = latitude;
        Longitude = longitude;
        City = city;
        Country = country;
        Source = source;
    }

    /// <summary>
    /// Calculates distance to another location in kilometers
    /// </summary>
    public double DistanceTo(GeographicLocation other)
    {
        const double earthRadiusKm = 6371.0;

        var lat1Rad = ToRadians(Latitude);
        var lat2Rad = ToRadians(other.Latitude);
        var deltaLatRad = ToRadians(other.Latitude - Latitude);
        var deltaLonRad = ToRadians(other.Longitude - Longitude);

        var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLonRad / 2) * Math.Sin(deltaLonRad / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    /// <summary>
    /// Checks if this location is approximately equal to another
    /// </summary>
    public bool IsApproximatelyEqual(GeographicLocation other, double toleranceKm = 1.0)
    {
        return DistanceTo(other) <= toleranceKm;
    }

    /// <summary>
    /// Gets a display-friendly description of the location
    /// </summary>
    public string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(City) && !string.IsNullOrEmpty(Country))
        {
            return $"{City}, {Country}";
        }

        if (!string.IsNullOrEmpty(City))
        {
            return City;
        }

        if (!string.IsNullOrEmpty(Country))
        {
            return Country;
        }

        return $"{Latitude:F4}°, {Longitude:F4}°";
    }

    /// <summary>
    /// Validates that the coordinates are within valid ranges
    /// </summary>
    public bool IsValid()
    {
        return Latitude >= -90 && Latitude <= 90 &&
               Longitude >= -180 && Longitude <= 180;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    public override string ToString() => GetDisplayName();

    public override bool Equals(object? obj)
    {
        if (obj is not GeographicLocation other) return false;
        return Math.Abs(Latitude - other.Latitude) < 0.0001 &&
               Math.Abs(Longitude - other.Longitude) < 0.0001;
    }

    public override int GetHashCode() => HashCode.Combine(Latitude, Longitude);
}

/// <summary>
/// Source of location data
/// </summary>
public enum LocationSource
{
    Unknown,
    GPS,
    Network,
    IP,
    Manual,
    WindowsLocationService,
    TimeZone
}
