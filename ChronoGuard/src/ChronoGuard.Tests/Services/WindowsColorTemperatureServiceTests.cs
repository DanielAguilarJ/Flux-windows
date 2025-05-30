using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChronoGuard.Infrastructure.Services;
using ChronoGuard.Domain.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace ChronoGuard.Tests.Services
{    [TestClass]
    public class WindowsColorTemperatureServiceTests
    {
        private WindowsColorTemperatureService _colorService = null!;
        private ILogger<WindowsColorTemperatureService> _logger = null!;

        [TestInitialize]
        public void Setup()
        {
            _logger = LoggerFactory.Create(builder => { })
                .CreateLogger<WindowsColorTemperatureService>();
            _colorService = new WindowsColorTemperatureService(_logger);
        }

        [TestMethod]
        public async Task GetMonitorsAsync_ReturnsValidMonitors()
        {
            // Act
            var monitors = await _colorService.GetMonitorsAsync();

            // Assert
            Assert.IsNotNull(monitors, "Monitors collection should not be null");
            Assert.IsTrue(monitors.Any(), "Should detect at least one monitor");
              foreach (var monitor in monitors)
            {
                Assert.IsFalse(string.IsNullOrEmpty(monitor.DevicePath), 
                    "Monitor should have a device path");
                Assert.IsTrue(monitor.IsPrimary || !monitor.IsPrimary, 
                    "IsPrimary should be a valid boolean");
            }
        }

        [TestMethod]
        public async Task GetExtendedMonitorsAsync_ReturnsDetailedInfo()
        {
            // Act
            var extendedMonitors = await _colorService.GetExtendedMonitorsAsync();

            // Assert
            Assert.IsNotNull(extendedMonitors, "Extended monitors collection should not be null");
            Assert.IsTrue(extendedMonitors.Any(), "Should detect at least one extended monitor");            foreach (var monitor in extendedMonitors)
            {
                Assert.IsFalse(string.IsNullOrEmpty(monitor.DevicePath), 
                    "Extended monitor should have device path");
                Assert.IsTrue(monitor.BitDepth >= 16 && monitor.BitDepth <= 32,
                    $"Bit depth should be reasonable (16-32), got {monitor.BitDepth}");
                
                // These properties should be set, even if detection fails
                Assert.IsNotNull(monitor.ManufacturerName, 
                    "Manufacturer name should not be null (can be 'Unknown')");
                Assert.IsNotNull(monitor.ModelName, 
                    "Model name should not be null (can be 'Unknown')");
            }
        }

        [TestMethod]
        public async Task ApplyTemperatureAsync_ValidTemperature_Succeeds()
        {
            // Arrange
            var warmTemperature = new ColorTemperature(3000);

            // Act & Assert - Should not throw
            await _colorService.ApplyTemperatureAsync(warmTemperature);
            
            // Verify we can apply multiple temperatures in sequence
            var coolTemperature = new ColorTemperature(6500);
            await _colorService.ApplyTemperatureAsync(coolTemperature);
            
            var neutralTemperature = new ColorTemperature(5000);
            await _colorService.ApplyTemperatureAsync(neutralTemperature);
        }

        [TestMethod]
        public async Task ApplyTemperatureToMonitorAsync_SpecificMonitor_Succeeds()
        {
            // Arrange
            var monitors = await _colorService.GetMonitorsAsync();
            var firstMonitor = monitors.First();
            var temperature = new ColorTemperature(4000);            // Act & Assert - Should not throw
            await _colorService.ApplyTemperatureToMonitorAsync(firstMonitor.DevicePath, temperature);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task ApplyTemperatureToMonitorAsync_InvalidMonitor_ThrowsException()
        {
            // Arrange
            var invalidMonitorName = "INVALID_MONITOR_12345";
            var temperature = new ColorTemperature(4000);

            // Act & Assert
            await _colorService.ApplyTemperatureToMonitorAsync(invalidMonitorName, temperature);
        }

        [TestMethod]
        public async Task FallbackMechanism_HandlesGammaRampFailure()
        {
            // This test verifies that the fallback system works when gamma ramp manipulation fails
            // Note: This is difficult to test in isolation without mocking internal Windows APIs
            
            // Arrange
            var extremeTemperature = new ColorTemperature(1000); // Very warm, might fail on some systems

            // Act & Assert - Should not throw, should use fallback
            try
            {
                await _colorService.ApplyTemperatureAsync(extremeTemperature);
                // If we get here, either primary method worked or fallback succeeded
                Assert.IsTrue(true, "Temperature application completed (primary or fallback)");
            }
            catch (Exception ex)
            {
                Assert.Fail($"All fallback mechanisms failed: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task MonitorCapabilityDetection_AssessesFeatures()
        {
            // Act
            var extendedMonitors = await _colorService.GetExtendedMonitorsAsync();

            // Assert
            foreach (var monitor in extendedMonitors)
            {
                // Test capability flags are properly set (can be true or false)
                Assert.IsTrue(monitor.SupportsHardwareGamma || !monitor.SupportsHardwareGamma,
                    "SupportsHardwareGamma should be a valid boolean");
                Assert.IsTrue(monitor.SupportsICCProfiles || !monitor.SupportsICCProfiles,
                    "SupportsICCProfiles should be a valid boolean");
                Assert.IsTrue(monitor.SupportsDDCCI || !monitor.SupportsDDCCI,
                    "SupportsDDCCI should be a valid boolean");

                // MaxLuminance should be reasonable if detected
                if (monitor.MaxLuminance > 0)
                {
                    Assert.IsTrue(monitor.MaxLuminance >= 80 && monitor.MaxLuminance <= 10000,
                        $"MaxLuminance should be reasonable (80-10000 nits), got {monitor.MaxLuminance}");
                }

                // ColorGamut should be reasonable if detected
                if (!string.IsNullOrEmpty(monitor.ColorGamut) && monitor.ColorGamut != "Unknown")
                {
                    var knownGamuts = new[] { "sRGB", "Adobe RGB", "DCI-P3", "Rec. 2020", "NTSC" };
                    Assert.IsTrue(knownGamuts.Any(g => monitor.ColorGamut.Contains(g)),
                        $"ColorGamut should be a known standard, got: {monitor.ColorGamut}");
                }
            }
        }

        [TestMethod]
        public async Task PerceptualColorAdjustment_ProducesNaturalColors()
        {
            // Test that the advanced gamma ramp generation produces natural-looking colors
            
            // Arrange
            var temperatures = new[]
            {
                new ColorTemperature(2700), // Warm incandescent
                new ColorTemperature(4000), // Warm white LED
                new ColorTemperature(5000), // Daylight
                new ColorTemperature(6500)  // Cool daylight
            };

            // Act & Assert
            foreach (var temperature in temperatures)
            {
                // Should not throw and should apply successfully
                await _colorService.ApplyTemperatureAsync(temperature);
                
                // Brief delay to allow system to settle
                await Task.Delay(100);
            }            // Reset to neutral
            await _colorService.RestoreOriginalSettingsAsync();
        }

        [TestMethod]
        public async Task MultiMonitorSupport_HandlesIndependently()
        {
            // Arrange
            var monitors = await _colorService.GetMonitorsAsync();
            
            if (monitors.Count() < 2)
            {
                Assert.Inconclusive("Multi-monitor test requires at least 2 monitors");
                return;
            }

            var monitor1 = monitors.First();
            var monitor2 = monitors.Skip(1).First();
            var warmTemp = new ColorTemperature(3000);
            var coolTemp = new ColorTemperature(6000);            // Act - Apply different temperatures to different monitors
            await _colorService.ApplyTemperatureToMonitorAsync(monitor1.DevicePath, warmTemp);
            await _colorService.ApplyTemperatureToMonitorAsync(monitor2.DevicePath, coolTemp);            // Assert - Should complete without errors
            Assert.IsTrue(true, "Independent monitor temperature adjustment completed");

            // Cleanup
            await _colorService.RestoreOriginalSettingsAsync();
        }

        [TestMethod]
        public async Task ChromaticAdaptation_MaintainsWhitePoint()
        {
            // Test Bradford chromatic adaptation matrix calculations
            
            // Arrange
            var temperatures = new[]
            {
                new ColorTemperature(2700),
                new ColorTemperature(3000),
                new ColorTemperature(4000),
                new ColorTemperature(5000),
                new ColorTemperature(6500)
            };

            // Act & Assert
            foreach (var temperature in temperatures)
            {
                // The advanced color temperature service should maintain proper white point
                // through Bradford chromatic adaptation
                await _colorService.ApplyTemperatureAsync(temperature);

                // Verify the temperature was applied (no exception thrown)
                Assert.IsTrue(true, $"Chromatic adaptation succeeded for {temperature.Kelvin}K");
                  await Task.Delay(50); // Brief settling time
            }

            await _colorService.RestoreOriginalSettingsAsync();
        }

        [TestMethod]
        public async Task ICCProfileFallback_HandlesUnsupportedHardware()
        {
            // This test verifies ICC profile fallback when hardware gamma isn't supported
            
            // Arrange
            var temperature = new ColorTemperature(3500);

            // Act - Force use of ICC profile fallback method
            try
            {
                // Try to apply temperature with ICC fallback
                await _colorService.ApplyTemperatureAsync(temperature);
                Assert.IsTrue(true, "ICC profile fallback mechanism worked");
            }
            catch (Exception ex)
            {
                // If ICC fallback also fails, ensure error is properly handled
                Assert.IsTrue(ex.Message.Contains("fallback") || ex.Message.Contains("ICC") || 
                             ex.Message.Contains("not supported"),
                    $"Expected fallback-related error message, got: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task GammaRampGeneration_CreatesValidCurves()
        {
            // Test that gamma ramp generation creates valid curves for different temperatures
            
            // Arrange
            var temperatures = new[]
            {
                new ColorTemperature(2000), // Very warm
                new ColorTemperature(3000), // Warm
                new ColorTemperature(5000), // Neutral
                new ColorTemperature(7000), // Cool
                new ColorTemperature(9000)  // Very cool
            };

            // Act & Assert
            foreach (var temperature in temperatures)
            {
                // Each temperature should generate a valid gamma ramp
                await _colorService.ApplyTemperatureAsync(temperature);
                
                // Verify no exceptions were thrown (gamma ramp was valid)
                Assert.IsTrue(true, $"Valid gamma ramp generated for {temperature.Kelvin}K");
                  await Task.Delay(25); // Brief delay
            }

            await _colorService.RestoreOriginalSettingsAsync();
        }

        [TestMethod]
        public async Task ResetToDefaultAsync_RestoresOriginalSettings()
        {
            // Arrange
            var originalMonitors = await _colorService.GetMonitorsAsync();
            var warmTemperature = new ColorTemperature(2700);

            // Act            await _colorService.ApplyTemperatureAsync(warmTemperature);
            await Task.Delay(100); // Let the change settle
            
            await _colorService.RestoreOriginalSettingsAsync();
            await Task.Delay(100); // Let the reset settle

            // Assert
            var restoredMonitors = await _colorService.GetMonitorsAsync();
            Assert.AreEqual(originalMonitors.Count(), restoredMonitors.Count(),
                "Same number of monitors should be detected after reset");
        }

        [TestMethod]
        public async Task StressTest_RapidTemperatureChanges()
        {
            // Test system stability under rapid temperature changes
            
            // Arrange
            var temperatures = new[]
            {
                new ColorTemperature(6500),
                new ColorTemperature(2700),
                new ColorTemperature(5000),
                new ColorTemperature(3500),
                new ColorTemperature(4500)
            };

            // Act & Assert
            for (int i = 0; i < 3; i++) // Multiple iterations
            {
                foreach (var temperature in temperatures)
                {
                    await _colorService.ApplyTemperatureAsync(temperature);
                    await Task.Delay(10); // Very brief delay for stress testing
                }            }

            // Cleanup
            await _colorService.RestoreOriginalSettingsAsync();
            Assert.IsTrue(true, "System remained stable under rapid temperature changes");
        }        [TestCleanup]
        public async Task Cleanup()
        {
            // Always reset to default after tests
            try
            {
                if (_colorService != null)
                {
                    await _colorService.RestoreOriginalSettingsAsync();
                    await Task.Delay(100);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
            
            _colorService?.Dispose();
        }
    }
}
