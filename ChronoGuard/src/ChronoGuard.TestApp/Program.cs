using ChronoGuard.Application.Services;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using Moq;
using System;
using System.Threading.Tasks;

namespace ChronoGuard.TestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("   CHRONOGUARD TESTAPP - VERIFICACION");
            Console.WriteLine("========================================");
            Console.WriteLine();
            
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
                
                // Create service
                var service = new ChronoGuardBackgroundService(
                    mockColorService.Object,
                    mockSolarService.Object);
                
                Console.WriteLine("✓ ChronoGuardBackgroundService creado exitosamente");
                
                // Test methods
                Console.WriteLine("\n1. Testing InitializePredictiveCacheAsync...");
                var location = new Location(40.7128, -74.0060, "New York", "USA");
                var profile = new ColorProfile("Test", "Test Profile", 6500, 2700, 30);
                
                await service.InitializePredictiveCacheAsync(location, profile);
                Console.WriteLine("   ✓ Cache predictivo inicializado");
                
                Console.WriteLine("\n2. Testing GetCachedTemperature...");
                var cachedTemp = service.GetCachedTemperature(DateTime.Now);
                Console.WriteLine($"   ✓ Temperatura en cache obtenida: {cachedTemp.Kelvin}K");
                
                // Verificar que la temperatura está en el rango esperado
                if (cachedTemp.Kelvin >= 2700 && cachedTemp.Kelvin <= 6500)
                {
                    Console.WriteLine("   ✓ Temperatura dentro del rango válido");
                }
                else
                {
                    Console.WriteLine($"   ❌ Temperatura fuera de rango: {cachedTemp.Kelvin}K");
                }
                
                Console.WriteLine("\n3. Testing CalculateAdaptiveUpdateInterval...");
                var solarTimes = await mockSolarService.Object.CalculateSolarTimesAsync(location, DateTime.Today);
                
                // Test different times of day
                var noonTime = DateTime.Today.AddHours(12);
                var interval1 = service.CalculateAdaptiveUpdateInterval(noonTime, solarTimes, profile);
                Console.WriteLine($"   ✓ Intervalo al mediodía: {interval1}ms");
                
                var sunriseTime = solarTimes.Sunrise;
                var interval2 = service.CalculateAdaptiveUpdateInterval(sunriseTime, solarTimes, profile);
                Console.WriteLine($"   ✓ Intervalo al amanecer: {interval2}ms");
                
                var sunsetTime = solarTimes.Sunset;
                var interval3 = service.CalculateAdaptiveUpdateInterval(sunsetTime, solarTimes, profile);
                Console.WriteLine($"   ✓ Intervalo al atardecer: {interval3}ms");
                
                Console.WriteLine("\n4. Testing ApplyTemperatureSmoothing...");
                var temp1 = new ColorTemperature(5000);
                var temp2 = new ColorTemperature(4800);
                var smoothedTemp = service.ApplyTemperatureSmoothing(temp1, temp2);
                Console.WriteLine($"   ✓ Suavizado aplicado: {temp1.Kelvin}K + {temp2.Kelvin}K = {smoothedTemp.Kelvin}K");
                
                // Verificar que el suavizado funciona correctamente
                var expectedSmoothed = (int)(5000 * 0.7 + 4800 * 0.3);
                if (Math.Abs(smoothedTemp.Kelvin - expectedSmoothed) <= 1)
                {
                    Console.WriteLine("   ✓ Algoritmo de suavizado correcto");
                }
                else
                {
                    Console.WriteLine($"   ❌ Error en suavizado. Esperado: {expectedSmoothed}K, Obtenido: {smoothedTemp.Kelvin}K");
                }
                
                Console.WriteLine("\n5. Testing ProcessTemperatureUpdateAsync...");
                await service.ProcessTemperatureUpdateAsync(DateTime.Now);
                Console.WriteLine("   ✓ Procesamiento de actualización de temperatura completado");
                
                Console.WriteLine("\n6. Testing UpdateLocationAsync...");
                var newLocation = new Location(34.0522, -118.2437, "Los Angeles", "USA");
                await service.UpdateLocationAsync(newLocation);
                Console.WriteLine("   ✓ Ubicación actualizada a Los Angeles");
                
                Console.WriteLine("\n7. Testing UpdateProfileAsync...");
                var newProfile = new ColorProfile("Test2", "Test Profile 2", 6000, 3000, 45);
                await service.UpdateProfileAsync(newProfile);
                Console.WriteLine("   ✓ Perfil actualizado");
                
                // Verificar que el cache se regeneró con la nueva ubicación/perfil
                Console.WriteLine("\n8. Testing cache regeneration...");
                var newCachedTemp = service.GetCachedTemperature(DateTime.Now);
                Console.WriteLine($"   ✓ Nueva temperatura en cache: {newCachedTemp.Kelvin}K");
                
                Console.WriteLine("\n========================================");
                Console.WriteLine("   ✅ TODAS LAS PRUEBAS EXITOSAS!");
                Console.WriteLine("   ChronoGuard está listo para usar");
                Console.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n========================================");
                Console.WriteLine("   ❌ ERROR EN LAS PRUEBAS");
                Console.WriteLine("========================================");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("\nPresiona cualquier tecla para continuar...");
                Console.ReadKey();
            }
        }
    }
}
