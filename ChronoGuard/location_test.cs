using ChronoGuard.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("=== ChronoGuard Location Detection Test ===");
Console.WriteLine();

// Set up services
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddHttpClient();

var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<LocationService>>();
var httpClient = serviceProvider.GetRequiredService<HttpClient>();

// Create location service
var locationService = new LocationService(httpClient, logger);

try
{
    Console.WriteLine("1. Testing IP-based location detection...");
    var ipLocation = await locationService.GetLocationFromIpAsync();
    
    if (ipLocation != null)
    {
        Console.WriteLine($"   ✓ IP Location detected:");
        Console.WriteLine($"     Coordinates: {ipLocation.Latitude}°, {ipLocation.Longitude}°");
        Console.WriteLine($"     City: {ipLocation.City ?? "Unknown"}");
        Console.WriteLine($"     Country: {ipLocation.Country ?? "Unknown"}");
        Console.WriteLine($"     Full Display: {ipLocation}");
    }
    else
    {
        Console.WriteLine("   ❌ IP location detection failed");
    }
    
    Console.WriteLine();
    Console.WriteLine("2. Testing Windows location detection...");
    var windowsLocation = await locationService.GetLocationFromWindowsApiAsync();
    
    if (windowsLocation != null)
    {
        Console.WriteLine($"   ✓ Windows Location detected:");
        Console.WriteLine($"     Coordinates: {windowsLocation.Latitude}°, {windowsLocation.Longitude}°");
        Console.WriteLine($"     City: {windowsLocation.City ?? "Unknown"}");
        Console.WriteLine($"     Country: {windowsLocation.Country ?? "Unknown"}");
        Console.WriteLine($"     Full Display: {windowsLocation}");
    }
    else
    {
        Console.WriteLine("   ⚠️ Windows location detection failed (may require permission)");
    }
    
    Console.WriteLine();
    Console.WriteLine("3. Testing combined location detection...");
    var combinedLocation = await locationService.GetCurrentLocationAsync();
    
    if (combinedLocation != null)
    {
        Console.WriteLine($"   ✓ Combined Location result:");
        Console.WriteLine($"     Coordinates: {combinedLocation.Latitude}°, {combinedLocation.Longitude}°");
        Console.WriteLine($"     City: {combinedLocation.City ?? "Unknown"}");
        Console.WriteLine($"     Country: {combinedLocation.Country ?? "Unknown"}");
        Console.WriteLine($"     Full Display: {combinedLocation}");
    }
    else
    {
        Console.WriteLine("   ❌ Combined location detection failed");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error during location testing: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("=== Location Detection Test Complete ===");
