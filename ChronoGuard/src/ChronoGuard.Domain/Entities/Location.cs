namespace ChronoGuard.Domain.Entities;

/// <summary>
/// Represents a geographical location with latitude and longitude coordinates
/// </summary>
public record Location
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public DateTime LastUpdated { get; init; }

    public Location(double latitude, double longitude, string? city = null, string? country = null)
    {
        if (latitude < -90 || latitude > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90 degrees");
        
        if (longitude < -180 || longitude > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180 degrees");

        Latitude = Math.Round(latitude, 2); // Privacy: round to city-level precision
        Longitude = Math.Round(longitude, 2);
        City = city;
        Country = country;
        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates the distance to another location in kilometers
    /// </summary>
    public double DistanceTo(Location other)
    {
        const double earthRadius = 6371; // km
        
        var lat1Rad = Latitude * Math.PI / 180;
        var lat2Rad = other.Latitude * Math.PI / 180;
        var deltaLatRad = (other.Latitude - Latitude) * Math.PI / 180;
        var deltaLonRad = (other.Longitude - Longitude) * Math.PI / 180;

        var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLonRad / 2) * Math.Sin(deltaLonRad / 2);
        
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadius * c;
    }

    public override string ToString()
    {
        var cityInfo = !string.IsNullOrEmpty(City) ? $" ({City})" : "";
        return $"{Latitude:F2}°, {Longitude:F2}°{cityInfo}";
    }
}
