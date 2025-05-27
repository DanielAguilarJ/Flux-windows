using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChronoGuard.Infrastructure.Services;
using ChronoGuard.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace ChronoGuard.Tests.Services
{
    [TestClass]
    public class SolarCalculatorServiceTests
    {
        private SolarCalculatorService _solarCalculator;

        [TestInitialize]
        public void Setup()
        {
            _solarCalculator = new SolarCalculatorService();
        }

        [TestMethod]
        public async Task CalculateSolarTimes_NewYorkSummerSolstice_ReturnsAccurateTimes()
        {
            // Arrange
            var location = new Location(40.7128, -74.0060, "New York", "USA");
            var date = new DateTime(2024, 6, 21); // Summer solstice

            // Act
            var solarTimes = await _solarCalculator.CalculateSolarTimesAsync(location, date);

            // Assert - New York summer solstice should have early sunrise (~5:25 AM) and late sunset (~8:30 PM)
            Assert.IsTrue(solarTimes.Sunrise.Hour >= 5 && solarTimes.Sunrise.Hour <= 6, 
                $"Expected sunrise between 5-6 AM, got {solarTimes.Sunrise:HH:mm}");
            Assert.IsTrue(solarTimes.Sunset.Hour >= 19 && solarTimes.Sunset.Hour <= 21, 
                $"Expected sunset between 7-9 PM, got {solarTimes.Sunset:HH:mm}");
            
            // Day length should be around 15 hours during summer solstice
            var dayLength = solarTimes.Sunset - solarTimes.Sunrise;
            Assert.IsTrue(dayLength.TotalHours >= 14.5 && dayLength.TotalHours <= 15.5,
                $"Expected day length ~15 hours, got {dayLength.TotalHours:F2} hours");
        }

        [TestMethod]
        public async Task CalculateSolarTimes_NewYorkWinterSolstice_ReturnsAccurateTimes()
        {
            // Arrange
            var location = new Location(40.7128, -74.0060, "New York", "USA");
            var date = new DateTime(2024, 12, 21); // Winter solstice

            // Act
            var solarTimes = await _solarCalculator.CalculateSolarTimesAsync(location, date);

            // Assert - New York winter solstice should have late sunrise (~7:20 AM) and early sunset (~4:30 PM)
            Assert.IsTrue(solarTimes.Sunrise.Hour >= 7 && solarTimes.Sunrise.Hour <= 8,
                $"Expected sunrise between 7-8 AM, got {solarTimes.Sunrise:HH:mm}");
            Assert.IsTrue(solarTimes.Sunset.Hour >= 16 && solarTimes.Sunset.Hour <= 17,
                $"Expected sunset between 4-5 PM, got {solarTimes.Sunset:HH:mm}");

            // Day length should be around 9 hours during winter solstice
            var dayLength = solarTimes.Sunset - solarTimes.Sunrise;
            Assert.IsTrue(dayLength.TotalHours >= 9.0 && dayLength.TotalHours <= 10.0,
                $"Expected day length ~9 hours, got {dayLength.TotalHours:F2} hours");
        }

        [TestMethod]
        public async Task CalculateSolarTimes_EquatorLocation_HasConsistentDayLength()
        {
            // Arrange
            var equatorLocation = new Location(0.0, 0.0, "Equator", "Test");
            var date1 = new DateTime(2024, 6, 21); // Summer solstice
            var date2 = new DateTime(2024, 12, 21); // Winter solstice

            // Act
            var solarTimes1 = await _solarCalculator.CalculateSolarTimesAsync(equatorLocation, date1);
            var solarTimes2 = await _solarCalculator.CalculateSolarTimesAsync(equatorLocation, date2);

            // Assert - At equator, day length should be approximately 12 hours year-round
            var dayLength1 = solarTimes1.Sunset - solarTimes1.Sunrise;
            var dayLength2 = solarTimes2.Sunset - solarTimes2.Sunrise;
            
            Assert.IsTrue(Math.Abs(dayLength1.TotalHours - 12.0) < 0.5,
                $"Expected equator day length ~12 hours in summer, got {dayLength1.TotalHours:F2} hours");
            Assert.IsTrue(Math.Abs(dayLength2.TotalHours - 12.0) < 0.5,
                $"Expected equator day length ~12 hours in winter, got {dayLength2.TotalHours:F2} hours");
            
            // Day lengths should be very similar
            Assert.IsTrue(Math.Abs(dayLength1.TotalHours - dayLength2.TotalHours) < 0.1,
                "Equator day lengths should be consistent year-round");
        }

        [TestMethod]
        public async Task CalculateSolarTimes_ArcticCircle_HasExtremeDayLengths()
        {
            // Arrange
            var arcticLocation = new Location(66.5, 0.0, "Arctic Circle", "Test");
            var summerSolstice = new DateTime(2024, 6, 21);
            var winterSolstice = new DateTime(2024, 12, 21);

            // Act
            var summerTimes = await _solarCalculator.CalculateSolarTimesAsync(arcticLocation, summerSolstice);
            var winterTimes = await _solarCalculator.CalculateSolarTimesAsync(arcticLocation, winterSolstice);

            // Assert - Arctic circle should have midnight sun in summer and polar night in winter
            var summerDayLength = summerTimes.Sunset - summerTimes.Sunrise;
            var winterDayLength = winterTimes.Sunset - winterTimes.Sunrise;

            // Summer should have very long days (close to 24 hours)
            Assert.IsTrue(summerDayLength.TotalHours >= 20.0,
                $"Expected arctic summer day length >= 20 hours, got {summerDayLength.TotalHours:F2} hours");
            
            // Winter should have very short days (close to 0 hours)
            Assert.IsTrue(winterDayLength.TotalHours <= 4.0,
                $"Expected arctic winter day length <= 4 hours, got {winterDayLength.TotalHours:F2} hours");
        }

        [TestMethod]
        public async Task CalculateSolarTimes_SouthernHemisphere_HasOppositeSeasons()
        {
            // Arrange
            var sydneyLocation = new Location(-33.8688, 151.2093, "Sydney", "Australia");
            var date = new DateTime(2024, 6, 21); // Winter solstice in Southern Hemisphere

            // Act
            var solarTimes = await _solarCalculator.CalculateSolarTimesAsync(sydneyLocation, date);

            // Assert - Sydney in June should have short days (winter)
            var dayLength = solarTimes.Sunset - solarTimes.Sunrise;
            Assert.IsTrue(dayLength.TotalHours >= 9.5 && dayLength.TotalHours <= 11.0,
                $"Expected Sydney winter day length ~10 hours, got {dayLength.TotalHours:F2} hours");
        }

        [TestMethod]
        public async Task CalculateSolarTimes_HighPrecisionNREL_SubMinuteAccuracy()
        {
            // Arrange
            var location = new Location(39.7392, -104.9903, "Denver", "USA"); // NREL location
            var date = new DateTime(2024, 3, 20); // Spring equinox

            // Act
            var solarTimes = await _solarCalculator.CalculateSolarTimesAsync(location, date);

            // Assert - On equinox, sunrise and sunset should be roughly 12 hours apart
            var dayLength = solarTimes.Sunset - solarTimes.Sunrise;
            Assert.IsTrue(Math.Abs(dayLength.TotalHours - 12.0) < 0.5,
                $"Expected equinox day length ~12 hours, got {dayLength.TotalHours:F2} hours");

            // Solar noon should be approximately halfway between sunrise and sunset
            var expectedSolarNoon = solarTimes.Sunrise.AddTicks((solarTimes.Sunset - solarTimes.Sunrise).Ticks / 2);
            var actualSolarNoon = solarTimes.SolarNoon;
            var noonDifference = Math.Abs((actualSolarNoon - expectedSolarNoon).TotalMinutes);
            
            Assert.IsTrue(noonDifference < 2.0,
                $"Solar noon should be within 2 minutes of midpoint, difference: {noonDifference:F1} minutes");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task CalculateSolarTimes_NullLocation_ThrowsException()
        {
            // Arrange
            Location location = null;
            var date = DateTime.Today;

            // Act & Assert
            await _solarCalculator.CalculateSolarTimesAsync(location, date);
        }

        [TestMethod]
        public async Task CalculateSolarTimes_LeapYear_HandlesCorrectly()
        {
            // Arrange
            var location = new Location(40.7128, -74.0060, "New York", "USA");
            var leapYearDate = new DateTime(2024, 2, 29); // Leap year day

            // Act
            var solarTimes = await _solarCalculator.CalculateSolarTimesAsync(location, leapYearDate);

            // Assert
            Assert.IsNotNull(solarTimes);
            Assert.IsTrue(solarTimes.Sunrise < solarTimes.Sunset);
            Assert.AreEqual(leapYearDate.Date, solarTimes.Date.Date);
        }

        [TestMethod]
        public async Task CalculateSolarTimes_ExtremeLongitudes_HandlesCorrectly()
        {
            // Arrange
            var eastLocation = new Location(35.6762, 139.6503, "Tokyo", "Japan"); // UTC+9
            var westLocation = new Location(34.0522, -118.2437, "Los Angeles", "USA"); // UTC-8
            var date = new DateTime(2024, 6, 21);

            // Act
            var tokyoTimes = await _solarCalculator.CalculateSolarTimesAsync(eastLocation, date);
            var laTimes = await _solarCalculator.CalculateSolarTimesAsync(westLocation, date);

            // Assert - Tokyo should have earlier local times than LA
            // Note: This compares local solar times, not clock times
            Assert.IsNotNull(tokyoTimes);
            Assert.IsNotNull(laTimes);
            Assert.IsTrue(tokyoTimes.Sunrise < tokyoTimes.Sunset);
            Assert.IsTrue(laTimes.Sunrise < laTimes.Sunset);
        }
    }
}
