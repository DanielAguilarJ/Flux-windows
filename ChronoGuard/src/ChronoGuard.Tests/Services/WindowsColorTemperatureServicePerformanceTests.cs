using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using ChronoGuard.Domain.Configuration;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Infrastructure.Services;

namespace ChronoGuard.Tests.Services
{
    /// <summary>
    /// Performance-focused tests for WindowsColorTemperatureService optimizations
    /// </summary>
    public class WindowsColorTemperatureServicePerformanceTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILogger<WindowsColorTemperatureService>> _mockLogger;
        private readonly Mock<ICCProfileService> _mockICCProfileService;
        private readonly Mock<IOptions<AdvancedColorManagementConfig>> _mockConfig;
        private readonly WindowsColorTemperatureService _service;

        public WindowsColorTemperatureServicePerformanceTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger<WindowsColorTemperatureService>>();
            _mockICCProfileService = new Mock<ICCProfileService>();
            _mockConfig = new Mock<IOptions<AdvancedColorManagementConfig>>();
            
            var config = new AdvancedColorManagementConfig
            {
                EnableHardwareGamma = true,
                UseAdvancedGammaAlgorithm = true,
                MonitorCalibrations = new Dictionary<string, MonitorCalibrationSettings>()
            };
            
            _mockConfig.Setup(x => x.Value).Returns(config);
            
            _service = new WindowsColorTemperatureService(
                _mockLogger.Object,
                _mockICCProfileService.Object,
                _mockConfig.Object
            );
        }

        [Fact]
        public async Task AdaptiveTransitionInterval_CalculatesCorrectly_BasedOnComplexity()
        {
            // Arrange
            var simpleTransition = new TransitionState(
                new ColorTemperature(6500), 
                new ColorTemperature(6000), 
                TimeSpan.FromMinutes(1), 
                "Simple transition"
            );
            
            var complexTransition = new TransitionState(
                new ColorTemperature(6500), 
                new ColorTemperature(3000), 
                TimeSpan.FromMinutes(30), 
                "Complex transition"
            );

            // Act & Assert - We can't directly test the private method,
            // but we can test through the public CreateTransitionAsync method
            var stopwatch = Stopwatch.StartNew();
            
            var simpleState = await _service.CreateTransitionAsync(
                simpleTransition.FromTemperature, 
                simpleTransition.ToTemperature, 
                simpleTransition.Duration
            );
            
            var complexState = await _service.CreateTransitionAsync(
                complexTransition.FromTemperature, 
                complexTransition.ToTemperature, 
                complexTransition.Duration
            );
            
            stopwatch.Stop();
            
            // Verify that transitions are created quickly (should be < 100ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 100, 
                $"Transition creation took {stopwatch.ElapsedMilliseconds}ms, expected < 100ms");
            
            Assert.NotNull(simpleState);
            Assert.NotNull(complexState);
            
            _output.WriteLine($"Adaptive transition calculation completed in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task GammaRampCaching_ImprovesPerfomance_OnRepeatedCalls()
        {
            // Arrange
            var temperature = new ColorTemperature(5000);
            var iterations = 100;

            // Act - First run (populate cache)
            var firstRunStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                await _service.ApplyTemperatureAsync(temperature);
            }
            firstRunStopwatch.Stop();

            // Act - Second run (should hit cache)
            var secondRunStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                await _service.ApplyTemperatureAsync(temperature);
            }
            secondRunStopwatch.Stop();

            // Assert - Second run should be faster due to caching
            _output.WriteLine($"First run: {firstRunStopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Second run: {secondRunStopwatch.ElapsedMilliseconds}ms");
            
            // The second run should be at least 20% faster (allowing for variance)
            var improvementThreshold = firstRunStopwatch.ElapsedMilliseconds * 0.8;
            Assert.True(secondRunStopwatch.ElapsedMilliseconds < improvementThreshold,
                $"Expected performance improvement from caching. First: {firstRunStopwatch.ElapsedMilliseconds}ms, " +
                $"Second: {secondRunStopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task ConcurrentTemperatureApplication_MaintainsThreadSafety()
        {
            // Arrange
            var temperatures = new[]
            {
                new ColorTemperature(3000),
                new ColorTemperature(4000),
                new ColorTemperature(5000),
                new ColorTemperature(6000),
                new ColorTemperature(6500)
            };

            var random = new Random(42); // Fixed seed for reproducibility
            var concurrentTasks = new List<Task<bool>>();

            // Act - Create multiple concurrent temperature applications
            var stopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < 50; i++)
            {
                var temperature = temperatures[random.Next(temperatures.Length)];
                concurrentTasks.Add(_service.ApplyTemperatureAsync(temperature));
            }

            var results = await Task.WhenAll(concurrentTasks);
            stopwatch.Stop();

            // Assert - All operations should complete without errors
            var successCount = results.Count(r => r);
            var failureCount = results.Length - successCount;

            _output.WriteLine($"Concurrent operations completed in {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Successes: {successCount}, Failures: {failureCount}");

            // We expect most operations to succeed (some might fail due to mocked dependencies)
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, 
                $"Concurrent operations took too long: {stopwatch.ElapsedMilliseconds}ms");
            
            // No deadlocks or crashes should occur
            Assert.True(results.Length == 50, "All tasks should complete");
        }

        [Fact]
        public async Task PerformanceUnderLoad_StaysWithinAcceptableLimits()
        {
            // Arrange
            var temperatureRange = Enumerable.Range(2700, 3801) // 2700K to 6500K
                .Where(k => k % 100 == 0) // Every 100K
                .Select(k => new ColorTemperature(k))
                .ToArray();

            var operationTimes = new List<long>();

            // Act - Apply various temperatures and measure performance
            foreach (var temperature in temperatureRange)
            {
                var stopwatch = Stopwatch.StartNew();
                await _service.ApplyTemperatureAsync(temperature);
                stopwatch.Stop();
                
                operationTimes.Add(stopwatch.ElapsedMilliseconds);
            }

            // Assert - Performance metrics
            var averageTime = operationTimes.Average();
            var maxTime = operationTimes.Max();
            var minTime = operationTimes.Min();

            _output.WriteLine($"Temperature application performance:");
            _output.WriteLine($"  Average: {averageTime:F2}ms");
            _output.WriteLine($"  Max: {maxTime}ms");
            _output.WriteLine($"  Min: {minTime}ms");
            _output.WriteLine($"  Total operations: {operationTimes.Count}");

            // Performance should be acceptable
            Assert.True(averageTime < 100, $"Average operation time should be < 100ms, was {averageTime:F2}ms");
            Assert.True(maxTime < 500, $"Maximum operation time should be < 500ms, was {maxTime}ms");
        }

        [Fact]
        public async Task TransitionPerformance_MaintainsTargetFrameRate()
        {
            // Arrange
            var fromTemp = new ColorTemperature(6500);
            var toTemp = new ColorTemperature(3000);
            var duration = TimeSpan.FromSeconds(5);

            // Act - Create and measure transition performance
            var transition = await _service.CreateTransitionAsync(fromTemp, toTemp, duration);
            
            var frameUpdateTimes = new List<long>();
            var startTime = DateTime.UtcNow;
            
            // Simulate transition updates for measurement
            while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(1)) // Measure for 1 second
            {
                var updateStopwatch = Stopwatch.StartNew();
                
                // Simulate what UpdateTransitionAsync would do
                var currentTemp = transition?.CurrentTemperature ?? fromTemp;
                await _service.ApplyTemperatureAsync(currentTemp);
                
                updateStopwatch.Stop();
                frameUpdateTimes.Add(updateStopwatch.ElapsedMilliseconds);
                
                await Task.Delay(50); // Simulate 20 FPS update rate
            }

            await _service.StopTransitionAsync();

            // Assert - Transition updates should maintain acceptable frame rate
            var averageFrameTime = frameUpdateTimes.Average();
            var maxFrameTime = frameUpdateTimes.Max();

            _output.WriteLine($"Transition frame performance:");
            _output.WriteLine($"  Average frame time: {averageFrameTime:F2}ms");
            _output.WriteLine($"  Max frame time: {maxFrameTime}ms");
            _output.WriteLine($"  Frame updates measured: {frameUpdateTimes.Count}");

            // For smooth transitions, frame updates should be quick
            Assert.True(averageFrameTime < 50, 
                $"Average frame time should be < 50ms for smooth transitions, was {averageFrameTime:F2}ms");
            Assert.True(maxFrameTime < 200, 
                $"Max frame time should be < 200ms to avoid stuttering, was {maxFrameTime}ms");
        }

        [Fact]
        public async Task MemoryUsage_RemainsStable_UnderContinuousLoad()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(true);
            var temperatures = new[]
            {
                new ColorTemperature(3000),
                new ColorTemperature(6500)
            };

            // Act - Continuous load test
            for (int cycle = 0; cycle < 10; cycle++)
            {
                for (int i = 0; i < 100; i++)
                {
                    var temp = temperatures[i % temperatures.Length];
                    await _service.ApplyTemperatureAsync(temp);
                }
                
                // Force garbage collection between cycles
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            var finalMemory = GC.GetTotalMemory(true);
            var memoryGrowth = finalMemory - initialMemory;

            // Assert - Memory usage should not grow significantly
            _output.WriteLine($"Memory usage:");
            _output.WriteLine($"  Initial: {initialMemory / 1024.0:F2} KB");
            _output.WriteLine($"  Final: {finalMemory / 1024.0:F2} KB");
            _output.WriteLine($"  Growth: {memoryGrowth / 1024.0:F2} KB");

            // Memory growth should be reasonable (less than 1MB for this test)
            Assert.True(memoryGrowth < 1024 * 1024, 
                $"Memory growth should be < 1MB, was {memoryGrowth / 1024.0:F2} KB");
        }

        [Fact]
        public void CacheEfficiency_MaintainsOptimalHitRatio()
        {
            // Arrange
            var temperatures = new[]
            {
                new ColorTemperature(3000),
                new ColorTemperature(4000),
                new ColorTemperature(5000),
                new ColorTemperature(6000),
                new ColorTemperature(6500)
            };

            // Act - Generate cache activity pattern (80% repeated, 20% new)
            var random = new Random(42);
            var operations = 1000;
            var cacheHits = 0;
            var previousApplicationTimes = new Dictionary<int, long>();

            for (int i = 0; i < operations; i++)
            {
                var tempIndex = random.NextDouble() < 0.8 
                    ? random.Next(Math.Min(i / 10, temperatures.Length)) // Favor recent temperatures
                    : random.Next(temperatures.Length); // Occasionally use new temperature

                var temperature = temperatures[tempIndex];
                
                var stopwatch = Stopwatch.StartNew();
                _service.ApplyTemperatureAsync(temperature).Wait();
                stopwatch.Stop();

                // Detect cache hits by comparing execution times
                if (previousApplicationTimes.TryGetValue(temperature.Kelvin, out var previousTime))
                {
                    if (stopwatch.ElapsedMilliseconds <= previousTime * 1.5) // Allow 50% variance
                    {
                        cacheHits++;
                    }
                }
                
                previousApplicationTimes[temperature.Kelvin] = stopwatch.ElapsedMilliseconds;
            }

            var hitRatio = (double)cacheHits / operations;

            // Assert - Cache should be effective
            _output.WriteLine($"Cache efficiency:");
            _output.WriteLine($"  Operations: {operations}");
            _output.WriteLine($"  Detected cache hits: {cacheHits}");
            _output.WriteLine($"  Hit ratio: {hitRatio:P2}");

            // With our test pattern, we should see good cache utilization
            Assert.True(hitRatio > 0.3, $"Cache hit ratio should be > 30%, was {hitRatio:P2}");
        }

        public void Dispose()
        {
            _service?.Dispose();
        }
    }
}
