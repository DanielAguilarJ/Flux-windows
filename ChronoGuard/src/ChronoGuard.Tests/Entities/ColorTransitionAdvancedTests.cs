using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChronoGuard.Domain.Entities;
using System;
using System.Drawing;

namespace ChronoGuard.Tests.Entities
{
    [TestClass]
    public class ColorTransitionAdvancedTests
    {
        [TestMethod]
        public void InterpolateTemperature_Linear_ProducesCorrectMidpoint()
        {
            // Arrange
            var startTemp = new ColorTemperature(6500); // Cool
            var endTemp = new ColorTemperature(2700);   // Warm
            var progress = 0.5; // Midpoint

            // Act
            var result = ColorTransition.InterpolateTemperature(startTemp, endTemp, progress, EasingType.Linear);

            // Assert
            var expectedKelvin = (6500 + 2700) / 2; // 4600K
            Assert.AreEqual(expectedKelvin, result.Kelvin, 10, 
                $"Expected ~{expectedKelvin}K at midpoint, got {result.Kelvin}K");
        }

        [TestMethod]
        public void InterpolateTemperature_EaseInOut_ProducesSmootherTransition()
        {
            // Arrange
            var startTemp = new ColorTemperature(6500);
            var endTemp = new ColorTemperature(2700);

            // Act - Test multiple points along the curve
            var linear25 = ColorTransition.InterpolateTemperature(startTemp, endTemp, 0.25, EasingType.Linear);
            var easeInOut25 = ColorTransition.InterpolateTemperature(startTemp, endTemp, 0.25, EasingType.EaseInOut);
            
            var linear75 = ColorTransition.InterpolateTemperature(startTemp, endTemp, 0.75, EasingType.Linear);
            var easeInOut75 = ColorTransition.InterpolateTemperature(startTemp, endTemp, 0.75, EasingType.EaseInOut);

            // Assert - EaseInOut should be closer to start at 0.25 and closer to end at 0.75
            Assert.IsTrue(Math.Abs(easeInOut25.Kelvin - startTemp.Kelvin) < Math.Abs(linear25.Kelvin - startTemp.Kelvin),
                "EaseInOut at 0.25 should be closer to start than linear");
            Assert.IsTrue(Math.Abs(easeInOut75.Kelvin - endTemp.Kelvin) < Math.Abs(linear75.Kelvin - endTemp.Kelvin),
                "EaseInOut at 0.75 should be closer to end than linear");
        }

        [TestMethod]
        public void InterpolateTemperature_ExponentialEasing_ProducesCorrectCurve()
        {
            // Arrange
            var startTemp = new ColorTemperature(6500);
            var endTemp = new ColorTemperature(2700);

            // Act
            var expo25 = ColorTransition.InterpolateTemperature(startTemp, endTemp, 0.25, EasingType.Exponential);
            var expo50 = ColorTransition.InterpolateTemperature(startTemp, endTemp, 0.5, EasingType.Exponential);
            var expo75 = ColorTransition.InterpolateTemperature(startTemp, endTemp, 0.75, EasingType.Exponential);

            // Assert - Exponential should change slowly at start, rapidly at end
            var change25 = Math.Abs(expo25.Kelvin - startTemp.Kelvin);
            var change50 = Math.Abs(expo50.Kelvin - startTemp.Kelvin);
            var change75 = Math.Abs(expo75.Kelvin - startTemp.Kelvin);

            Assert.IsTrue(change25 < change50, "Exponential should change slowly early");
            Assert.IsTrue(change50 < change75, "Exponential should accelerate later");
        }

        [TestMethod]
        public void InterpolateTemperature_CircadianRhythm_FollowsNaturalPattern()
        {
            // Arrange
            var startTemp = new ColorTemperature(6500); // Day
            var endTemp = new ColorTemperature(2700);   // Night

            // Act - Test transition that mimics natural circadian rhythm
            var results = new ColorTemperature[11];
            for (int i = 0; i <= 10; i++)
            {
                var progress = i / 10.0;
                results[i] = ColorTransition.InterpolateTemperature(startTemp, endTemp, progress, EasingType.CircadianRhythm);
            }

            // Assert - Should start slow, accelerate in middle, then slow down again
            var earlyChange = Math.Abs(results[2].Kelvin - results[0].Kelvin);
            var midChange = Math.Abs(results[6].Kelvin - results[4].Kelvin);
            var lateChange = Math.Abs(results[10].Kelvin - results[8].Kelvin);

            Assert.IsTrue(midChange > earlyChange, "Circadian rhythm should have faster change in middle");
            Assert.IsTrue(midChange > lateChange, "Circadian rhythm should slow down at end");
        }

        [TestMethod]
        public void InterpolateRGB_PerceptualColorSpace_ProducesNaturalColors()
        {
            // Arrange
            var warmColor = Color.FromArgb(255, 180, 100); // Warm orange
            var coolColor = Color.FromArgb(255, 255, 255); // Cool white

            // Act
            var midpoint = ColorTransition.InterpolateRGB(warmColor, coolColor, 0.5, InterpolationMode.PerceptualLab);

            // Assert - Perceptual interpolation should produce more natural intermediate colors
            Assert.IsNotNull(midpoint);
            Assert.IsTrue(midpoint.R >= warmColor.R && midpoint.R <= coolColor.R, "Red should be between bounds");
            Assert.IsTrue(midpoint.G >= warmColor.G && midpoint.G <= coolColor.G, "Green should be between bounds");
            Assert.IsTrue(midpoint.B >= warmColor.B && midpoint.B <= coolColor.B, "Blue should be between bounds");
        }

        [TestMethod]
        public void InterpolateRGB_LinearRGB_ProducesDirectInterpolation()
        {
            // Arrange
            var color1 = Color.FromArgb(255, 0, 0);   // Pure red
            var color2 = Color.FromArgb(0, 255, 0);   // Pure green

            // Act
            var midpoint = ColorTransition.InterpolateRGB(color1, color2, 0.5, InterpolationMode.LinearRGB);

            // Assert - Linear RGB should produce mathematical midpoint
            Assert.AreEqual(128, midpoint.R, 5, "Red should be ~128 at midpoint");
            Assert.AreEqual(128, midpoint.G, 5, "Green should be ~128 at midpoint");
            Assert.AreEqual(0, midpoint.B, 5, "Blue should remain 0");
        }

        [TestMethod]
        public void CalculateAdaptiveEasing_HighBlueLight_UsesSlowerTransition()
        {
            // Arrange
            var highBlueTemp = new ColorTemperature(6500);
            var lowBlueTemp = new ColorTemperature(2700);
            var currentTime = new DateTime(2024, 6, 21, 20, 0, 0); // 8 PM
            var sunrise = new DateTime(2024, 6, 21, 6, 0, 0);
            var sunset = new DateTime(2024, 6, 21, 20, 0, 0);

            // Act
            var adaptiveEasing = ColorTransition.CalculateAdaptiveEasing(
                highBlueTemp, lowBlueTemp, currentTime, sunrise, sunset);

            // Assert - Should use gentler easing for high blue light reduction
            Assert.IsTrue(adaptiveEasing == EasingType.EaseInOut || 
                         adaptiveEasing == EasingType.CircadianRhythm ||
                         adaptiveEasing == EasingType.Smooth,
                $"Expected gentle easing for high blue light, got {adaptiveEasing}");
        }

        [TestMethod]
        public void CalculateAdaptiveEasing_SmallTemperatureDelta_UsesLinearTransition()
        {
            // Arrange
            var temp1 = new ColorTemperature(5000);
            var temp2 = new ColorTemperature(4500); // Small difference
            var currentTime = new DateTime(2024, 6, 21, 19, 0, 0);
            var sunrise = new DateTime(2024, 6, 21, 6, 0, 0);
            var sunset = new DateTime(2024, 6, 21, 20, 0, 0);

            // Act
            var adaptiveEasing = ColorTransition.CalculateAdaptiveEasing(
                temp1, temp2, currentTime, sunrise, sunset);

            // Assert - Small changes can use linear easing
            Assert.IsTrue(adaptiveEasing == EasingType.Linear || 
                         adaptiveEasing == EasingType.EaseInOut,
                $"Expected simple easing for small delta, got {adaptiveEasing}");
        }

        [TestMethod]
        public void EasingFunctions_BoundaryValues_HandleCorrectly()
        {
            // Test all easing functions with boundary values (0.0 and 1.0)
            var easingTypes = Enum.GetValues<EasingType>();
            
            foreach (var easingType in easingTypes)
            {
                // Act
                var resultAt0 = ColorTransition.ApplyEasing(0.0, easingType);
                var resultAt1 = ColorTransition.ApplyEasing(1.0, easingType);

                // Assert
                Assert.AreEqual(0.0, resultAt0, 0.001, 
                    $"Easing {easingType} should return 0.0 for input 0.0");
                Assert.AreEqual(1.0, resultAt1, 0.001, 
                    $"Easing {easingType} should return 1.0 for input 1.0");
            }
        }

        [TestMethod]
        public void EasingFunctions_MonotonicIncreasing_AlwaysIncreases()
        {
            // Test that all easing functions are monotonically increasing
            var easingTypes = Enum.GetValues<EasingType>();
            
            foreach (var easingType in easingTypes)
            {
                var previousValue = 0.0;
                
                for (double t = 0.0; t <= 1.0; t += 0.1)
                {
                    var currentValue = ColorTransition.ApplyEasing(t, easingType);
                    
                    Assert.IsTrue(currentValue >= previousValue, 
                        $"Easing {easingType} should be monotonically increasing. " +
                        $"At t={t:F1}: current={currentValue:F3}, previous={previousValue:F3}");
                    
                    previousValue = currentValue;
                }
            }
        }

        [TestMethod]
        public void BlueReductionCalculation_HighColorTemperature_CalculatesCorrectReduction()
        {
            // Arrange
            var coolTemp = new ColorTemperature(6500);
            var warmTemp = new ColorTemperature(2700);

            // Act
            var blueReduction = ColorTransition.CalculateBlueReduction(coolTemp, warmTemp);

            // Assert - Should show significant blue light reduction
            Assert.IsTrue(blueReduction >= 0.3 && blueReduction <= 1.0,
                $"Expected blue reduction 30-100%, got {blueReduction * 100:F1}%");
        }

        [TestMethod]
        public void BlueReductionCalculation_SimilarTemperatures_ShowsMinimalReduction()
        {
            // Arrange
            var temp1 = new ColorTemperature(5000);
            var temp2 = new ColorTemperature(4800);

            // Act
            var blueReduction = ColorTransition.CalculateBlueReduction(temp1, temp2);

            // Assert - Should show minimal blue light reduction
            Assert.IsTrue(blueReduction >= 0.0 && blueReduction <= 0.2,
                $"Expected minimal blue reduction <20%, got {blueReduction * 100:F1}%");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void ApplyEasing_InvalidProgress_ThrowsException()
        {
            // Act & Assert
            ColorTransition.ApplyEasing(-0.1, EasingType.Linear);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void ApplyEasing_ProgressAboveOne_ThrowsException()
        {
            // Act & Assert
            ColorTransition.ApplyEasing(1.1, EasingType.Linear);
        }
    }
}
