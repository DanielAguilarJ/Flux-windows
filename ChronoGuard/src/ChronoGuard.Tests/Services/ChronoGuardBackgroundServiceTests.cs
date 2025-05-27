using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChronoGuard.Application.Services;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ChronoGuard.Tests.Services
{
    [TestClass]
    public class ChronoGuardBackgroundServiceTests
    {
        private Mock<IColorTemperatureService> _mockColorService;
        private Mock<ISolarCalculatorService> _mockSolarService;
        private ChronoGuardBackgroundService _backgroundService;

        [TestInitialize]
        public void Setup()
        {
            _mockColorService = new Mock<IColorTemperatureService>();
            _mockSolarService = new Mock<ISolarCalculatorService>();
            _backgroundService = new ChronoGuardBackgroundService(_mockColorService.Object, _mockSolarService.Object);
        }

        [TestMethod]
        public async Task PredictiveTemperatureCache_PrecomputesAccurately()
        {
            // Arrange
            var location = new Location(40.7128, -74.0060, "New York", "USA");
            var profile = new ColorProfile("Test", "Test", 6500, 2700, 30);
            var baseTime = new DateTime(2024, 6, 21, 12, 0, 0);
            
            var mockSolarTimes = new SolarTimes(
                baseTime.Date.AddHours(6),  // sunrise
                baseTime.Date.AddHours(20), // sunset
                baseTime.Date,
                location
            );

            _mockSolarService
                .Setup(s => s.CalculateSolarTimesAsync(It.IsAny<Location>(), It.IsAny<DateTime>()))
                .ReturnsAsync(mockSolarTimes);

            // Act
            await _backgroundService.InitializePredictiveCacheAsync(location, profile);
            var cachedTemp = _backgroundService.GetCachedTemperature(baseTime);

            // Assert
            Assert.IsNotNull(cachedTemp, "Temperature should be cached for the specified time");
            Assert.IsTrue(cachedTemp.Kelvin >= 2700 && cachedTemp.Kelvin <= 6500,
                $"Cached temperature should be within profile range, got {cachedTemp.Kelvin}K");
        }

        [TestMethod]
        public async Task AdaptiveUpdateInterval_AdjustsBasedOnTransitionRate()
        {
            // Arrange
            var location = new Location(40.7128, -74.0060, "New York", "USA");
            var profile = new ColorProfile("Test", "Test", 6500, 2700, 30);
            
            // Setup times during rapid transition (sunset)
            var sunsetTime = new DateTime(2024, 6, 21, 20, 0, 0);
            var mockSolarTimes = new SolarTimes(
                sunsetTime.Date.AddHours(6),
                sunsetTime,
                sunsetTime.Date,
                location
            );

            _mockSolarService
                .Setup(s => s.CalculateSolarTimesAsync(It.IsAny<Location>(), It.IsAny<DateTime>()))
                .ReturnsAsync(mockSolarTimes);

            // Act
            var rapidTransitionInterval = _backgroundService.CalculateAdaptiveUpdateInterval(
                sunsetTime, mockSolarTimes, profile);
            
            var stableInterval = _backgroundService.CalculateAdaptiveUpdateInterval(
                sunsetTime.AddHours(-6), mockSolarTimes, profile); // Midday

            // Assert
            Assert.IsTrue(rapidTransitionInterval < stableInterval,
                $"Rapid transition interval ({rapidTransitionInterval}ms) should be shorter than stable interval ({stableInterval}ms)");
            Assert.IsTrue(rapidTransitionInterval >= 100 && rapidTransitionInterval <= 1000,
                $"Rapid transition interval should be 100-1000ms, got {rapidTransitionInterval}ms");
        }

        [TestMethod]
        public async Task HighFrequencyTransitionUpdates_MaintainsSmoothness()
        {
            // Arrange
            var location = new Location(40.7128, -74.0060, "New York", "USA");
            var profile = new ColorProfile("Test", "Test", 6500, 2700, 30);
            var transitionStart = new DateTime(2024, 6, 21, 19, 30, 0);
            var transitionEnd = transitionStart.AddMinutes(30);

            var mockSolarTimes = new SolarTimes(
                transitionStart.Date.AddHours(6),
                transitionStart.Date.AddHours(20),
                transitionStart.Date,
                location
            );

            _mockSolarService
                .Setup(s => s.CalculateSolarTimesAsync(It.IsAny<Location>(), It.IsAny<DateTime>()))
                .ReturnsAsync(mockSolarTimes);

            var appliedTemperatures = new List<ColorTemperature>();
            _mockColorService
                .Setup(c => c.ApplyTemperatureAsync(It.IsAny<ColorTemperature>()))
                .Callback<ColorTemperature>(temp => appliedTemperatures.Add(temp))
                .Returns(Task.CompletedTask);

            // Act - Simulate high-frequency updates during transition
            await _backgroundService.InitializePredictiveCacheAsync(location, profile);
            
            for (var time = transitionStart; time <= transitionEnd; time = time.AddMinutes(1))
            {
                await _backgroundService.ProcessTemperatureUpdateAsync(time);
            }

            // Assert
            Assert.IsTrue(appliedTemperatures.Count >= 15, // At least every 2 minutes
                $"Should have multiple updates during transition, got {appliedTemperatures.Count}");

            // Verify smooth progression
            for (int i = 1; i < appliedTemperatures.Count; i++)
            {
                var tempDiff = Math.Abs(appliedTemperatures[i].Kelvin - appliedTemperatures[i-1].Kelvin);
                Assert.IsTrue(tempDiff <= 200, // No sudden jumps > 200K
                    $"Temperature change too abrupt: {tempDiff}K between updates {i-1} and {i}");
            }
        }

        [TestMethod]
        public async Task EnergyEfficientCaching_ReducesCalculationLoad()
        {
            // Arrange
            var location = new Location(40.7128, -74.0060, "New York", "USA");
            var profile = new ColorProfile("Test", "Test", 6500, 2700, 30);
            var testTime = new DateTime(2024, 6, 21, 12, 0, 0);

            var mockSolarTimes = new SolarTimes(
                testTime.Date.AddHours(6),
                testTime.Date.AddHours(20),
                testTime.Date,
                location
            );

            _mockSolarService
                .Setup(s => s.CalculateSolarTimesAsync(It.IsAny<Location>(), It.IsAny<DateTime>()))
                .ReturnsAsync(mockSolarTimes);

            // Act
            await _backgroundService.InitializePredictiveCacheAsync(location, profile);
            
            // Request same time multiple times
            var temp1 = _backgroundService.GetCachedTemperature(testTime);
            var temp2 = _backgroundService.GetCachedTemperature(testTime);
            var temp3 = _backgroundService.GetCachedTemperature(testTime.AddSeconds(30)); // Close time

            // Assert
            Assert.AreEqual(temp1.Kelvin, temp2.Kelvin, 
                "Identical times should return identical cached values");
            Assert.IsTrue(Math.Abs(temp1.Kelvin - temp3.Kelvin) <= 50,
                "Close times should return similar cached values");

            // Verify solar calculation was called minimal times (setup + cache initialization)
            _mockSolarService.Verify(s => s.CalculateSolarTimesAsync(It.IsAny<Location>(), It.IsAny<DateTime>()), 
                Times.AtMost(3), "Solar calculations should be minimized through caching");
        }

        [TestMethod]
        public async Task BackgroundService_HandlesLocationChanges()
        {
            // Arrange
            var nyLocation = new Location(40.7128, -74.0060, "New York", "USA");
            var laLocation = new Location(34.0522, -118.2437, "Los Angeles", "USA");
            var profile = new ColorProfile("Test", "Test", 6500, 2700, 30);
            var testTime = new DateTime(2024, 6, 21, 20, 0, 0); // 8 PM

            var nySolarTimes = new SolarTimes(
                testTime.Date.AddHours(6), testTime.Date.AddHours(20),
                testTime.Date, nyLocation);
            var laSolarTimes = new SolarTimes(
                testTime.Date.AddHours(6.5), testTime.Date.AddHours(20.5),
                testTime.Date, laLocation);

            _mockSolarService
                .Setup(s => s.CalculateSolarTimesAsync(nyLocation, It.IsAny<DateTime>()))
                .ReturnsAsync(nySolarTimes);
            _mockSolarService
                .Setup(s => s.CalculateSolarTimesAsync(laLocation, It.IsAny<DateTime>()))
                .ReturnsAsync(laSolarTimes);

            // Act
            await _backgroundService.InitializePredictiveCacheAsync(nyLocation, profile);
            var nyTemp = _backgroundService.GetCachedTemperature(testTime);

            await _backgroundService.UpdateLocationAsync(laLocation);
            var laTemp = _backgroundService.GetCachedTemperature(testTime);

            // Assert
            Assert.AreNotEqual(nyTemp.Kelvin, laTemp.Kelvin, 
                "Different locations should produce different temperatures at same time");
            
            // LA is further west, so at 8 PM it should be earlier in sunset transition
            Assert.IsTrue(laTemp.Kelvin >= nyTemp.Kelvin,
                $"LA should have higher temperature (less sunset) than NY at same clock time. " +
                $"NY: {nyTemp.Kelvin}K, LA: {laTemp.Kelvin}K");
        }

        [TestMethod]
        public async Task ProfileSwitching_UpdatesTemperatureImmediately()
        {
            // Arrange
            var location = new Location(40.7128, -74.0060, "New York", "USA");
            var profile1 = new ColorProfile("Mild", "Mild reduction", 6000, 4000, 30);
            var profile2 = new ColorProfile("Strong", "Strong reduction", 6500, 2500, 60);
            var testTime = new DateTime(2024, 6, 21, 22, 0, 0); // 10 PM (night)

            var mockSolarTimes = new SolarTimes(
                testTime.Date.AddHours(6), testTime.Date.AddHours(20),
                testTime.Date, location);

            _mockSolarService
                .Setup(s => s.CalculateSolarTimesAsync(It.IsAny<Location>(), It.IsAny<DateTime>()))
                .ReturnsAsync(mockSolarTimes);

            var appliedTemperature = new ColorTemperature(6500);
            _mockColorService
                .Setup(c => c.ApplyTemperatureAsync(It.IsAny<ColorTemperature>()))
                .Callback<ColorTemperature>(temp => appliedTemperature = temp)
                .Returns(Task.CompletedTask);

            // Act
            await _backgroundService.InitializePredictiveCacheAsync(location, profile1);
            await _backgroundService.ProcessTemperatureUpdateAsync(testTime);
            var temp1 = appliedTemperature.Kelvin;

            await _backgroundService.UpdateProfileAsync(profile2);
            await _backgroundService.ProcessTemperatureUpdateAsync(testTime);
            var temp2 = appliedTemperature.Kelvin;

            // Assert
            Assert.IsTrue(temp2 < temp1,
                $"Stronger profile should produce lower temperature at night. " +
                $"Mild: {temp1}K, Strong: {temp2}K");
            Assert.IsTrue(temp2 >= 2500 && temp2 <= 4000,
                $"Night temperature with strong profile should be in expected range, got {temp2}K");
        }

        [TestMethod]
        public void TemperatureSmoothing_ReducesFlicker()
        {
            // Arrange
            var temperatures = new[]
            {
                new ColorTemperature(5000),
                new ColorTemperature(4950),
                new ColorTemperature(5020), // Small spike
                new ColorTemperature(4900),
                new ColorTemperature(4850)
            };

            // Act
            var smoothedTemperatures = new List<ColorTemperature>();
            foreach (var temp in temperatures)
            {
                var smoothed = _backgroundService.ApplyTemperatureSmoothing(temp, 
                    smoothedTemperatures.LastOrDefault() ?? temp);
                smoothedTemperatures.Add(smoothed);
            }

            // Assert
            // Check that smoothing reduces the spike
            var originalSpike = Math.Abs(temperatures[2].Kelvin - temperatures[1].Kelvin);
            var smoothedSpike = Math.Abs(smoothedTemperatures[2].Kelvin - smoothedTemperatures[1].Kelvin);
            
            Assert.IsTrue(smoothedSpike < originalSpike,
                $"Smoothing should reduce spikes. Original: {originalSpike}K, Smoothed: {smoothedSpike}K");
            
            // Verify overall trend is preserved
            Assert.IsTrue(smoothedTemperatures.Last().Kelvin < smoothedTemperatures.First().Kelvin,
                "Overall decreasing trend should be preserved");
        }

        [TestMethod]
        public async Task ErrorRecovery_ContinuesOperationAfterFailures()
        {
            // Arrange
            var location = new Location(40.7128, -74.0060, "New York", "USA");
            var profile = new ColorProfile("Test", "Test", 6500, 2700, 30);
            var testTime = new DateTime(2024, 6, 21, 12, 0, 0);

            // Setup solar service to fail first call, succeed second
            _mockSolarService
                .SetupSequence(s => s.CalculateSolarTimesAsync(It.IsAny<Location>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException("Temporary failure"))
                .ReturnsAsync(new SolarTimes(
                    testTime.Date.AddHours(6), testTime.Date.AddHours(20),
                    testTime.Date, location));

            // Act & Assert - Should not throw, should retry and succeed
            await _backgroundService.InitializePredictiveCacheAsync(location, profile);
            
            var cachedTemp = _backgroundService.GetCachedTemperature(testTime);
            Assert.IsNotNull(cachedTemp, "Service should recover from temporary failures");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _backgroundService?.Dispose();
        }
    }
}
