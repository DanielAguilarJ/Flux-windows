using System;
using System.Threading.Tasks;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChronoGuard.TestApp
{
    /// <summary>
    /// Diagnostic program to test and troubleshoot color temperature application
    /// </summary>
    class ColorTemperatureDiagnosticProgram
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🔍 ChronoGuard Color Temperature Diagnostic Tool");
            Console.WriteLine("================================================");
            Console.WriteLine();

            // Setup dependency injection
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
                    services.AddSingleton<WindowsColorTemperatureService>();
                })
                .Build();

            var logger = host.Services.GetRequiredService<ILogger<ColorTemperatureDiagnosticProgram>>();
            var colorService = host.Services.GetRequiredService<WindowsColorTemperatureService>();

            try
            {
                await RunDiagnosticsAsync(logger, colorService);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Critical error during diagnostics: {ex.Message}");
                logger.LogError(ex, "Critical error during diagnostics");
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task RunDiagnosticsAsync(ILogger logger, WindowsColorTemperatureService colorService)
        {
            Console.WriteLine("🔍 Step 1: Testing Monitor Detection");
            Console.WriteLine("───────────────────────────────────");
            
            try
            {
                var monitors = await colorService.GetMonitorsAsync();
                Console.WriteLine($"✅ Found {monitors.Count()} monitor(s):");
                
                foreach (var monitor in monitors)
                {
                    Console.WriteLine($"   📺 {monitor.DeviceName} - {monitor.Width}x{monitor.Height}");
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Monitor detection failed: {ex.Message}");
                return;
            }

            Console.WriteLine("🔍 Step 2: Testing Current Temperature Retrieval");
            Console.WriteLine("───────────────────────────────────────────────");
            
            try
            {
                var currentTemp = colorService.GetCurrentTemperature();
                if (currentTemp != null)
                {
                    Console.WriteLine($"✅ Current temperature: {currentTemp.Kelvin}K");
                }
                else
                {
                    Console.WriteLine("⚠️ No current temperature available");
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Temperature retrieval failed: {ex.Message}");
            }

            Console.WriteLine("🔍 Step 3: Testing Temperature Application");
            Console.WriteLine("────────────────────────────────────────");
            
            var testTemperatures = new[] { 4000, 3000, 5000, 6500 };
            
            foreach (var tempK in testTemperatures)
            {
                Console.WriteLine($"Testing {tempK}K...");
                
                try
                {
                    var colorTemp = new ColorTemperature(tempK);
                    var success = await colorService.ApplyTemperatureAsync(colorTemp);
                    
                    if (success)
                    {
                        Console.WriteLine($"✅ {tempK}K applied successfully");
                        
                        // Show the change for 2 seconds
                        Console.WriteLine("   (You should see a color change on your screen)");
                        await Task.Delay(2000);
                    }
                    else
                    {
                        Console.WriteLine($"❌ {tempK}K failed to apply");
                        Console.WriteLine("   Possible causes:");
                        Console.WriteLine("   • Monitor doesn't support gamma manipulation");
                        Console.WriteLine("   • Graphics driver is incompatible");
                        Console.WriteLine("   • Another application is controlling colors");
                        Console.WriteLine("   • Administrator permissions needed");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ {tempK}K error: {ex.Message}");
                }
                
                Console.WriteLine();
            }

            Console.WriteLine("🔍 Step 4: System Information");
            Console.WriteLine("────────────────────────────");
            
            try
            {
                var osVersion = Environment.OSVersion;
                var isAdmin = IsRunningAsAdministrator();
                var bitness = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
                
                Console.WriteLine($"OS: {osVersion.VersionString} ({bitness})");
                Console.WriteLine($"Running as Administrator: {(isAdmin ? "Yes" : "No")}");
                Console.WriteLine($".NET Runtime: {Environment.Version}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ System info error: {ex.Message}");
            }

            Console.WriteLine("🔍 Step 5: Restoring Original Settings");
            Console.WriteLine("─────────────────────────────────────");
            
            try
            {
                await colorService.RestoreOriginalSettingsAsync();
                Console.WriteLine("✅ Original settings restored");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Restore failed: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("🎯 Recommendations:");
            Console.WriteLine("──────────────────");
            
            // Generate recommendations based on test results
            await GenerateRecommendationsAsync(colorService);
        }

        static async Task GenerateRecommendationsAsync(WindowsColorTemperatureService colorService)
        {
            var recommendations = new List<string>();
            
            try
            {
                // Test basic functionality
                var testTemp = new ColorTemperature(4000);
                var success = await colorService.ApplyTemperatureAsync(testTemp);
                
                if (!success)
                {
                    recommendations.Add("❌ Primary gamma ramp method failed");
                    recommendations.Add("💡 Try running as Administrator");
                    recommendations.Add("💡 Update your graphics drivers");
                    recommendations.Add("💡 Close other color management software (f.lux, etc.)");
                    recommendations.Add("💡 Use Windows Night Light as an alternative");
                }
                else
                {
                    recommendations.Add("✅ Color temperature control is working correctly!");
                    recommendations.Add("💡 If ChronoGuard still doesn't work in the main app, try:");
                    recommendations.Add("   • Restarting the application");
                    recommendations.Add("   • Checking for conflicting software");
                    recommendations.Add("   • Verifying monitor settings");
                }

                // Check admin permissions
                if (!IsRunningAsAdministrator())
                {
                    recommendations.Add("⚠️ Consider running as Administrator for better hardware access");
                }

                // Check OS version
                var osVersion = Environment.OSVersion.Version;
                if (osVersion.Major < 10)
                {
                    recommendations.Add("⚠️ Windows 10 or newer recommended for best compatibility");
                }

                await colorService.RestoreOriginalSettingsAsync();
            }
            catch (Exception ex)
            {
                recommendations.Add($"❌ Diagnostic test failed: {ex.Message}");
                recommendations.Add("💡 This indicates a serious compatibility issue");
            }

            foreach (var recommendation in recommendations)
            {
                Console.WriteLine(recommendation);
            }
        }

        static bool IsRunningAsAdministrator()
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
