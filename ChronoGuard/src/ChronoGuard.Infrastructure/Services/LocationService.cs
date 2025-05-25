using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
using System.Runtime.InteropServices;
using Windows.Devices.Geolocation;
using Windows.Services.Maps;

namespace ChronoGuard.Infrastructure.Services;

/// <summary>
/// Service for detecting and managing geographical location using multiple sources
/// </summary>
public class LocationService : ILocationService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocationService> _logger;
    private Location? _lastKnownLocation;
    private readonly object _locationLock = new();

    public event EventHandler<Location>? LocationChanged;

    public LocationService(HttpClient httpClient, ILogger<LocationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current location using the preferred method (Windows API first, then IP)
    /// </summary>
    public async Task<Location?> GetCurrentLocationAsync()
    {
        try
        {
            // Try Windows Location API first for better accuracy
            var windowsLocation = await GetLocationFromWindowsApiAsync();
            if (windowsLocation != null)
            {
                _logger.LogInformation("Location obtained from Windows API: {Location}", windowsLocation);
                UpdateLastKnownLocation(windowsLocation);
                return windowsLocation;
            }

            // Fallback to IP-based location
            var ipLocation = await GetLocationFromIpAsync();
            if (ipLocation != null)
            {
                _logger.LogInformation("Location obtained from IP: {Location}", ipLocation);
                UpdateLastKnownLocation(ipLocation);
                return ipLocation;
            }

            _logger.LogWarning("Could not determine location from any source");
            return _lastKnownLocation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current location");
            return _lastKnownLocation;
        }
    }

    /// <summary>
    /// Gets location from Windows Location API
    /// </summary>
    public async Task<Location?> GetLocationFromWindowsApiAsync()
    {
        try
        {
            // Check if running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogWarning("Windows Location API not available on current platform");
                return null;
            }

            var accessStatus = await Geolocator.RequestAccessAsync();
            if (accessStatus != GeolocationAccessStatus.Allowed)
            {
                _logger.LogWarning("Location access not granted by user");
                return null;
            }

            var geolocator = new Geolocator()
            {
                DesiredAccuracy = PositionAccuracy.Default,
                DesiredAccuracyInMeters = 1000 // City-level accuracy for privacy
            };

            var position = await geolocator.GetGeopositionAsync(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
            
            if (position?.Coordinate?.Point != null)
            {
                var latitude = position.Coordinate.Point.Position.Latitude;
                var longitude = position.Coordinate.Point.Position.Longitude;

                // Try to get city name using reverse geocoding
                var cityName = await GetCityNameAsync(latitude, longitude);

                return new Location(latitude, longitude, cityName);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting location from Windows API");
            return null;
        }
    }

    /// <summary>
    /// Gets location from IP address using ipapi.co service
    /// </summary>
    public async Task<Location?> GetLocationFromIpAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync("https://ipapi.co/json/");
            
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("latitude", out var latElement) &&
                root.TryGetProperty("longitude", out var lonElement) &&
                latElement.ValueKind == JsonValueKind.Number &&
                lonElement.ValueKind == JsonValueKind.Number)
            {
                var latitude = latElement.GetDouble();
                var longitude = lonElement.GetDouble();
                
                var city = root.TryGetProperty("city", out var cityElement) ? cityElement.GetString() : null;
                var country = root.TryGetProperty("country_name", out var countryElement) ? countryElement.GetString() : null;

                return new Location(latitude, longitude, city, country);
            }

            _logger.LogWarning("Invalid response from IP location service");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error getting location from IP");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing IP location response");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting location from IP");
            return null;
        }
    }

    /// <summary>
    /// Searches for cities by name using a free geocoding service
    /// </summary>
    public async Task<IEnumerable<Location>> SearchCitiesAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Enumerable.Empty<Location>();

        try
        {
            var encodedQuery = Uri.EscapeDataString(query);
            var url = $"https://geocoding-api.open-meteo.com/v1/search?name={encodedQuery}&count=10&language=en&format=json";
            
            var response = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            
            if (!doc.RootElement.TryGetProperty("results", out var resultsElement))
                return Enumerable.Empty<Location>();

            var locations = new List<Location>();
            
            foreach (var result in resultsElement.EnumerateArray())
            {
                if (result.TryGetProperty("latitude", out var latElement) &&
                    result.TryGetProperty("longitude", out var lonElement) &&
                    latElement.ValueKind == JsonValueKind.Number &&
                    lonElement.ValueKind == JsonValueKind.Number)
                {
                    var latitude = latElement.GetDouble();
                    var longitude = lonElement.GetDouble();
                    
                    var name = result.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                    var country = result.TryGetProperty("country", out var countryElement) ? countryElement.GetString() : null;

                    if (!string.IsNullOrEmpty(name))
                    {
                        locations.Add(new Location(latitude, longitude, name, country));
                    }
                }
            }

            return locations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching cities for query: {Query}", query);
            return Enumerable.Empty<Location>();
        }
    }

    /// <summary>
    /// Validates if a location has valid coordinates
    /// </summary>
    public bool IsValidLocation(Location location)
    {
        return location.Latitude >= -90 && location.Latitude <= 90 &&
               location.Longitude >= -180 && location.Longitude <= 180;
    }

    /// <summary>
    /// Gets city name using reverse geocoding
    /// </summary>
    private async Task<string?> GetCityNameAsync(double latitude, double longitude)
    {
        try
        {
            // Try using Windows Maps API for reverse geocoding
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var location = new Geopoint(new BasicGeoposition { Latitude = latitude, Longitude = longitude });
                var result = await MapLocationFinder.FindLocationsAtAsync(location);
                
                if (result.Status == MapLocationFinderStatus.Success && result.Locations.Any())
                {
                    var address = result.Locations.First().Address;
                    return address.Town ?? address.District ?? address.Region;
                }
            }

            // Fallback to OpenStreetMap Nominatim
            var url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={latitude:F6}&lon={longitude:F6}&zoom=10&addressdetails=1";
            var response = await _httpClient.GetStringAsync(url);
            
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("address", out var addressElement))
            {
                // Try to get city, town, or village
                foreach (var cityProperty in new[] { "city", "town", "village", "municipality" })
                {
                    if (addressElement.TryGetProperty(cityProperty, out var cityElement))
                    {
                        return cityElement.GetString();
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not get city name for coordinates {Lat}, {Lon}", latitude, longitude);
            return null;
        }
    }

    /// <summary>
    /// Updates the last known location and raises event if changed significantly
    /// </summary>
    private void UpdateLastKnownLocation(Location newLocation)
    {
        lock (_locationLock)
        {
            var shouldRaiseEvent = false;
            
            if (_lastKnownLocation == null)
            {
                shouldRaiseEvent = true;
            }
            else
            {
                // Raise event if location changed by more than 50km
                var distance = _lastKnownLocation.DistanceTo(newLocation);
                if (distance > 50)
                {
                    shouldRaiseEvent = true;
                    _logger.LogInformation("Location changed by {Distance:F1} km", distance);
                }
            }

            _lastKnownLocation = newLocation;

            if (shouldRaiseEvent)
            {
                LocationChanged?.Invoke(this, newLocation);
            }
        }
    }

    public void Dispose()
    {
        // HttpClient is managed by DI container, don't dispose it here
        GC.SuppressFinalize(this);
    }
}
