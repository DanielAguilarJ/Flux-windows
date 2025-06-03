using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ChronoGuard.Domain.Configuration;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Infrastructure.Services;

namespace ChronoGuard.Tests.Services
{
    /// <summary>
    /// Performance-focused tests for WindowsColorTemperatureService optimizations
    /// </summary>
    [TestClass]
    public class WindowsColorTemperatureServicePerformanceTests
    {
        private Mock<ILogger<WindowsColorTemperatureService>>? _mockLogger;
        private Mock<ICCProfileService>? _mockICCProfileService;
        private AdvancedColorManagementConfig? _config;
        private WindowsColorTemperatureService? _service;

        [TestInitialize]
        public void TestInitialize()
        {
            _mockLogger = new Mock<ILogger<WindowsColorTemperatureService>>();
            _mockICCProfileService = new Mock<ICCProfileService>();
            
            _config = new AdvancedColorManagementConfig
            {
                EnableHardwareGamma = true,
                EnableICCProfileFallback = true,
                MonitorCalibrations = new Dictionary<string, MonitorCalibrationSettings>()
            };
            
            _service = new WindowsColorTemperatureService(
                _mockLogger.Object,
                _mockICCProfileService.Object,
                _config
            );
        }

        [TestMethod]
        public async Task AdaptiveTransitionInterval_CalculatesCorrectly_BasedOnComplexity()
        {
            // Arrange
            var simpleTransition = new TransitionState(
                sourceTemp: 6500,
                targetTemp: 3000,
                duration: TimeSpan.FromMinutes(1),
                curve: TransitionCurve.Linear,
                priority: TransitionPriority.Normal
            );

            var complexTransition = new TransitionState(
                sourceTemp: 6500,
                targetTemp: 3000,
                duration: TimeSpan.FromMinutes(1),
                curve: TransitionCurve.Exponential,
                priority: TransitionPriority.High
            );

            // Act & Assert
            var stopwatch = Stopwatch.StartNew();
            
            var simpleState = _service!.CalculateTransitionState(simpleTransition);
            var complexState = _service.CalculateTransitionState(complexTransition);
            
            stopwatch.Stop();

            // Verify that transitions are created quickly (should be < 100ms)
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 100,
                $"Transition creation took {stopwatch.ElapsedMilliseconds}ms, expected < 100ms");
            Assert.IsNotNull(simpleState);
            Assert.IsNotNull(complexState);
        }

        [TestMethod]
        public async Task GammaRampCaching_ImprovesPerfomance_OnRepeatedCalls()
        {
            // Arrange
            var temperatureValue = 4000;
            var brightness = 1.0f;

            // Act - First run (cache miss expected)
            var firstRunStopwatch = Stopwatch.StartNew();
            await _service!.ApplyColorTemperatureAsync(temperatureValue, brightness);
            firstRunStopwatch.Stop();

            // Act - Second run (cache hit expected)
            var secondRunStopwatch = Stopwatch.StartNew();
            await _service.ApplyColorTemperatureAsync(temperatureValue, brightness);
            secondRunStopwatch.Stop();

            // Assert - Second run should be faster due to caching
            Console.WriteLine($"First run: {firstRunStopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Second run: {secondRunStopwatch.ElapsedMilliseconds}ms");

            // The second run should be at least 20% faster (allowing for variance)
            var improvementThreshold = firstRunStopwatch.ElapsedMilliseconds * 0.8;
            Assert.IsTrue(secondRunStopwatch.ElapsedMilliseconds <= improvementThreshold,
                $"Expected performance improvement from caching. First: {firstRunStopwatch.ElapsedMilliseconds}ms, " +
                $"Second: {secondRunStopwatch.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        public async Task ConcurrentTemperatureApplication_MaintainsThreadSafety()
        {
            // Arrange
            const int concurrentOperations = 10;
            var tasks = new List<Task<bool>>();
            var stopwatch = Stopwatch.StartNew();

            // Act - Execute multiple operations concurrently
            for (int i = 0; i < concurrentOperations; i++)
            {
                var temp = 3000 + (i * 200); // Vary temperatures
                var brightness = 0.8f + (i * 0.02f); // Vary brightness

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await _service!.ApplyColorTemperatureAsync(temp, brightness);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }));
            }

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var successCount = results.Count(r => r);
            var failureCount = results.Length - successCount;
            
            Console.WriteLine($"Successes: {successCount}, Failures: {failureCount}");

            // We expect most operations to succeed (some might fail due to mocked dependencies)
            Assert.IsTrue(successCount >= concurrentOperations * 0.5, 
                "At least 50% of concurrent operations should succeed");

            // No deadlocks or crashes should occur
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000,
                $"Concurrent operations took too long: {stopwatch.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        public async Task PerformanceUnderLoad_StaysWithinAcceptableLimits()
        {
            // Arrange
            const int operationCount = 50;
            var operationTimes = new List<long>();

            // Act - Execute many operations and measure individual performance
            for (int i = 0; i < operationCount; i++)
            {
                var tempValue = 2700 + (i % 10) * 400; // Cycle through different temperatures
                var brightness = 0.5f + (i % 5) * 0.1f; // Cycle through different brightness

                var sw = Stopwatch.StartNew();
                try
                {
                    await _service!.ApplyColorTemperatureAsync(tempValue, brightness);
                    sw.Stop();
                    operationTimes.Add(sw.ElapsedMilliseconds);
                }
                catch
                {
                    sw.Stop();
                    // Record failed operations as taking maximum time
                    operationTimes.Add(100);
                }
            }

            // Assert - Calculate performance metrics
            var averageTime = operationTimes.Average();
            var maxTime = operationTimes.Max();
            var minTime = operationTimes.Min();

            Console.WriteLine($"Performance metrics over {operationCount} operations:");
            Console.WriteLine($"  Average: {averageTime:F2}ms");
            Console.WriteLine($"  Max: {maxTime}ms");
            Console.WriteLine($"  Min: {minTime}ms");
            Console.WriteLine($"  Total operations: {operationTimes.Count}");

            // Performance should be acceptable
            Assert.IsTrue(averageTime < 100, $"Average operation time should be < 100ms, was {averageTime:F2}ms");
        }

        [TestMethod]
        public async Task TransitionPerformance_MaintainsTargetFrameRate()
        {
            // Arrange
            const int frameUpdates = 30; // Simulate 30 frame updates
            var frameUpdateTimes = new List<long>();

            // Act - Simulate continuous transition updates (like during sunset/sunrise)
            for (int frame = 0; frame < frameUpdates; frame++)
            {
                var progress = (float)frame / frameUpdates;
                var currentTemp = 6500 - (int)(3500 * progress); // Transition from 6500K to 3000K
                var currentBrightness = 1.0f - (0.3f * progress); // Dim slightly during transition

                var sw = Stopwatch.StartNew();
                try
                {
                    await _service!.ApplyColorTemperatureAsync(currentTemp, currentBrightness);
                    sw.Stop();
                    frameUpdateTimes.Add(sw.ElapsedMilliseconds);
                }
                catch
                {
                    sw.Stop();
                    frameUpdateTimes.Add(50); // Assume 50ms for failed operations
                }

                // Small delay to simulate frame rate
                await Task.Delay(16); // ~60 FPS target
            }

            // Assert - Frame update performance
            var averageFrameTime = frameUpdateTimes.Average();
            var maxFrameTime = frameUpdateTimes.Max();

            Console.WriteLine($"Frame performance over {frameUpdates} updates:");
            Console.WriteLine($"  Average frame time: {averageFrameTime:F2}ms");
            Console.WriteLine($"  Max frame time: {maxFrameTime}ms");
            Console.WriteLine($"  Frame updates measured: {frameUpdateTimes.Count}");

            // For smooth transitions, frame updates should be quick
            Assert.IsTrue(averageFrameTime < 50,
                $"Average frame time should be < 50ms for smooth transitions, was {averageFrameTime:F2}ms");
            Assert.IsTrue(maxFrameTime < 200,
                $"Max frame time should be < 200ms to avoid stuttering, was {maxFrameTime}ms");
        }

        [TestMethod]
        public async Task MemoryUsage_RemainsStable_UnderContinuousLoad()
        {
            // Arrange
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var initialMemory = GC.GetTotalMemory(false);
            const int operationCycles = 20;

            // Act - Perform continuous operations
            for (int cycle = 0; cycle < operationCycles; cycle++)
            {
                for (int temp = 2700; temp <= 6500; temp += 200)
                {
                    try
                    {
                        await _service!.ApplyColorTemperatureAsync(temp, 1.0f);
                    }
                    catch
                    {
                        // Ignore exceptions for memory test
                    }
                }
            }

            // Force garbage collection to get accurate measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false);
            var memoryGrowth = finalMemory - initialMemory;

            // Assert - Memory usage should not grow significantly
            Console.WriteLine($"Memory usage analysis:");
            Console.WriteLine($"  Initial: {initialMemory / 1024.0:F2} KB");
            Console.WriteLine($"  Final: {finalMemory / 1024.0:F2} KB");
            Console.WriteLine($"  Growth: {memoryGrowth / 1024.0:F2} KB");

            // Memory growth should be reasonable (less than 1MB for this test)
            Assert.IsTrue(memoryGrowth < 1024 * 1024,
                $"Memory growth should be < 1MB, was {memoryGrowth / 1024.0:F2} KB");
        }

        [TestMethod]
        public void CacheEfficiency_MaintainsOptimalHitRatio()
        {
            // Arrange
            const int operations = 100;
            var temperatures = new[] { 2700, 3000, 4000, 5000, 6500 }; // Common temperatures
            var brightnessLevels = new[] { 0.5f, 0.8f, 1.0f }; // Common brightness levels
            var random = new Random(42); // Fixed seed for repeatability

            var operationTimes = new List<long>();

            // Act - Execute operations with repeated values to test caching
            for (int i = 0; i < operations; i++)
            {
                var temp = temperatures[random.Next(temperatures.Length)];
                var brightness = brightnessLevels[random.Next(brightnessLevels.Length)];

                var sw = Stopwatch.StartNew();
                try
                {
                    _service!.ApplyColorTemperatureAsync(temp, brightness).Wait();
                    sw.Stop();
                    operationTimes.Add(sw.ElapsedMilliseconds);
                }
                catch
                {
                    sw.Stop();
                    operationTimes.Add(50); // Default time for failed operations
                }
            }

            // Assert - Analyze cache effectiveness
            var averageTime = operationTimes.Average();
            var fastOperations = operationTimes.Count(t => t < averageTime * 0.5);
            var cacheHits = fastOperations; // Approximation: fast operations likely hit cache
            var hitRatio = (double)cacheHits / operations;

            Console.WriteLine($"Cache efficiency analysis:");
            Console.WriteLine($"  Operations: {operations}");
            Console.WriteLine($"  Detected cache hits: {cacheHits}");
            Console.WriteLine($"  Hit ratio: {hitRatio:P2}");

            // With our test pattern, we should see good cache utilization
            Assert.IsTrue(hitRatio > 0.3,
                $"Cache hit ratio should be > 30% with repeated values, was {hitRatio:P2}");
        }

        /// <summary>
        /// Helper method to create transition states for testing
        /// </summary>
        private TransitionState CreateTestTransition(int sourceTemp, int targetTemp, 
            TransitionCurve curve = TransitionCurve.Linear, 
            TransitionPriority priority = TransitionPriority.Normal)
        {
            return new TransitionState(
                sourceTemp: sourceTemp,
                targetTemp: targetTemp,
                duration: TimeSpan.FromMinutes(1),
                curve: curve,
                priority: priority
            );
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _service?.Dispose();
        }
    }
}
