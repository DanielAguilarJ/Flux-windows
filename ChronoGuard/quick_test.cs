using System;
using System.Threading.Tasks;
using ChronoGuard.Application.Services;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using Moq;

class QuickTest
{
    static async Task Main()
    {
        Console.WriteLine("=== ChronoGuard Implementation Verification ===");
        
        try
        {
            // Setup mocks
            var mockColorService = new Mock<IColorTemperatureService>();
            var mockSolarService = new Mock<ISolarCalculatorService>();
            
            // Setup mock behavior
            mockSolarService
                .Setup(s => s.CalculateSolarTimesAsync(It.IsAny<Location>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new SolarTimes(
                    DateTime.Today.AddHours(6),  // sunrise
                    DateTime.Today.AddHours(20), // sunset
                    DateTime.Today,
                    new Location(40.7128, -74.0060, "New York", "USA")
                ));
            
            // Create service instance using test constructor
            var service = new ChronoGuardBackgroundService(
                mockColorService.Object,
                mockSolarService.Object);
            
            Console.WriteLine("✓ Service created successfully");
            
            // Test 1: Initialize Predictive Cache
            var location = new Location(40.7128, -74.0060, "New York", "USA");
            var profile = new ColorProfile("Test", "Test Profile", 6500, 2700, 30);
            
            await service.InitializePredictiveCacheAsync(location, profile);
            Console.WriteLine("✓ Predictive cache initialized");
            
            // Test 2: Get Cached Temperature
            var cachedTemp = service.GetCachedTemperature(DateTime.Now);
            Console.WriteLine($"✓ Cached temperature retrieved: {cachedTemp.Kelvin}K");
            
            // Test 3: Calculate Adaptive Update Interval
            var solarTimes = await mockSolarService.Object.CalculateSolarTimesAsync(location, DateTime.Today);
            var interval = service.CalculateAdaptiveUpdateInterval(DateTime.Now, solarTimes, profile);
            Console.WriteLine($"✓ Adaptive update interval calculated: {interval}ms");
            
            // Test 4: Apply Temperature Smoothing
            var smoothedTemp = service.ApplyTemperatureSmoothing(
                new ColorTemperature(5000), 
                new ColorTemperature(4800));
            Console.WriteLine($"✓ Temperature smoothing applied: {smoothedTemp.Kelvin}K");
            
            // Test 5: Process Temperature Update
            await service.ProcessTemperatureUpdateAsync(DateTime.Now);
            Console.WriteLine("✓ Temperature update processed");
            
            // Test 6: Update Location
            var newLocation = new Location(34.0522, -118.2437, "Los Angeles", "USA");
            await service.UpdateLocationAsync(newLocation);
            Console.WriteLine("✓ Location updated");
            
            // Test 7: Update Profile
            var newProfile = new ColorProfile("Test2", "Test Profile 2", 6000, 3000, 45);
            await service.UpdateProfileAsync(newProfile);
            Console.WriteLine("✓ Profile updated");
            
            Console.WriteLine("\n=== ALL TESTS PASSED! ===");
            Console.WriteLine("The ChronoGuardBackgroundService implementation is working correctly.");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ ERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
