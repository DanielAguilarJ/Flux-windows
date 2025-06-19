using System;
using System.Threading.Tasks;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace ChronoGuard.TestApp
{
    /// <summary>
    /// Simple diagnostic tool to test color temperature functionality
    /// </summary>
    class SimpleColorTest
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("🎨 ChronoGuard - Prueba Rápida de Temperatura de Color");
            Console.WriteLine("====================================================");
            Console.WriteLine();

            // Create a simple logger that outputs to console
            using var loggerFactory = LoggerFactory.Create(builder => 
                builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            
            var logger = loggerFactory.CreateLogger<WindowsColorTemperatureService>();
            
            // Create the color temperature service
            var colorService = new WindowsColorTemperatureService(logger, null, null);

            Console.WriteLine("📊 Información del Sistema:");
            Console.WriteLine($"   • OS: {Environment.OSVersion.VersionString}");
            Console.WriteLine($"   • Administrador: {(IsRunningAsAdmin() ? "Sí" : "No")}");
            Console.WriteLine($"   • .NET: {Environment.Version}");
            Console.WriteLine();

            Console.WriteLine("🔍 Detectando monitores...");
            try
            {
                var monitors = await colorService.GetMonitorsAsync();
                Console.WriteLine($"✅ Encontrados {monitors.Count()} monitor(es)");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error detectando monitores: {ex.Message}");
                Console.WriteLine("Presiona cualquier tecla para salir...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("🎯 Probando aplicación de temperatura de color...");
            Console.WriteLine("(Deberías ver cambios de color en tu pantalla)");
            Console.WriteLine();

            var testResults = new List<(int temp, bool success)>();

            // Test different temperatures
            var testTemperatures = new[] { 
                (3000, "Cálido (3000K)"),
                (4000, "Intermedio (4000K)"), 
                (6500, "Frío (6500K)")
            };

            foreach (var (kelvin, description) in testTemperatures)
            {
                Console.Write($"Probando {description}... ");
                
                try
                {
                    var colorTemp = new ColorTemperature(kelvin);
                    var success = await colorService.ApplyTemperatureAsync(colorTemp);
                    
                    if (success)
                    {
                        Console.WriteLine("✅ Éxito");
                        testResults.Add((kelvin, true));
                        
                        // Let user see the change
                        await Task.Delay(1500);
                    }
                    else
                    {
                        Console.WriteLine("❌ Falló");
                        testResults.Add((kelvin, false));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                    testResults.Add((kelvin, false));
                }
            }

            Console.WriteLine();
            Console.WriteLine("🔄 Restaurando configuración original...");
            
            try
            {
                await colorService.RestoreOriginalSettingsAsync();
                Console.WriteLine("✅ Configuración restaurada");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ No se pudo restaurar: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("📋 Resultados:");
            Console.WriteLine("─────────────");

            var successCount = testResults.Count(r => r.success);
            var totalTests = testResults.Count;

            if (successCount == totalTests)
            {
                Console.WriteLine("🎉 ¡Perfecto! Tu sistema es totalmente compatible con ChronoGuard.");
                Console.WriteLine("   Si tienes problemas en la aplicación principal, intenta:");
                Console.WriteLine("   • Reiniciar ChronoGuard");
                Console.WriteLine("   • Cerrar otros software de gestión de color");
                Console.WriteLine("   • Ejecutar como administrador");
            }
            else if (successCount == 0)
            {
                Console.WriteLine("❌ Tu sistema no es compatible con control de temperatura de color.");
                Console.WriteLine();
                Console.WriteLine("🔧 Posibles soluciones:");
                Console.WriteLine("   1. Ejecutar como Administrador");
                Console.WriteLine("   2. Actualizar drivers de gráficos");
                Console.WriteLine("   3. Cerrar f.lux, Night Light u otro software similar");
                Console.WriteLine("   4. Verificar que tu monitor soporte ajustes de gamma");
                Console.WriteLine("   5. Usar 'Luz Nocturna' de Windows como alternativa");
                Console.WriteLine();
                Console.WriteLine("💡 Nota: Algunos monitores y drivers no soportan esta funcionalidad.");
            }
            else
            {
                Console.WriteLine($"⚠️ Compatibilidad parcial: {successCount}/{totalTests} pruebas exitosas.");
                Console.WriteLine("   Prueba las soluciones mencionadas arriba.");
            }

            Console.WriteLine();
            Console.WriteLine("Presiona cualquier tecla para salir...");
            Console.ReadKey();
        }

        static bool IsRunningAsAdmin()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
