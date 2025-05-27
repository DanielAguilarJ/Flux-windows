using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChronoGuard.Domain.Entities;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace ChronoGuard.Tests.Core
{
    [TestClass]
    public class LocationTests
    {
        [TestMethod]
        public void Location_ValidCoordinates_CreatesSuccessfully()
        {
            // Arrange & Act
            var location = new Location(40.7128, -74.0060, "New York", "USA");

            // Assert
            Assert.AreEqual(40.71, location.Latitude); // Rounded to 2 decimal places
            Assert.AreEqual(-74.01, location.Longitude); // Rounded to 2 decimal places  
            Assert.AreEqual("New York", location.City);
            Assert.AreEqual("USA", location.Country);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Location_InvalidLatitude_ThrowsException()
        {
            // Arrange & Act & Assert
            new Location(91.0, -74.0060, "Invalid", "Test");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Location_InvalidLongitude_ThrowsException()
        {
            // Arrange & Act & Assert
            new Location(40.7128, 181.0, "Invalid", "Test");
        }

        [TestMethod]
        public void Location_DistanceTo_CalculatesCorrectly()
        {
            // Arrange
            var nyc = new Location(40.7128, -74.0060, "New York", "USA");
            var la = new Location(34.0522, -118.2437, "Los Angeles", "USA");

            // Act
            var distance = nyc.DistanceTo(la);

            // Assert
            Assert.IsTrue(distance > 3900 && distance < 4000, "Distance should be approximately 3944 km");
        }
    }

    [TestClass]
    public class ColorProfileTests
    {
        [TestMethod]
        public void ColorProfile_Constructor_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var profile = new ColorProfile("Test Profile", "Test Description", 6500, 3000, 30);

            // Assert
            Assert.AreEqual("Test Profile", profile.Name);
            Assert.AreEqual("Test Description", profile.Description);
            Assert.AreEqual(6500, profile.DayTemperature);
            Assert.AreEqual(3000, profile.NightTemperature);
            Assert.AreEqual(30, profile.TransitionDurationMinutes);
            Assert.IsFalse(string.IsNullOrEmpty(profile.Id));
        }

        [TestMethod]
        public void ColorProfile_DefaultConstructor_UsesDefaults()
        {
            // Arrange & Act
            var profile = new ColorProfile();

            // Assert
            Assert.AreEqual(ColorTemperature.DefaultDayKelvin, profile.DayTemperature);
            Assert.AreEqual(ColorTemperature.DefaultNightKelvin, profile.NightTemperature);
            Assert.AreEqual(20, profile.TransitionDurationMinutes);
            Assert.IsTrue(profile.EnableSunsetTransition);
            Assert.IsTrue(profile.EnableSunriseTransition);
        }

        [TestMethod]
        public void ColorProfile_GetColorTemperatureForTime_ReturnsCorrectTemperature()
        {
            // Arrange
            var profile = new ColorProfile("Test", "Description", 6500, 3000, 30);
            var location = new Location(40.7128, -74.0060, "New York", "USA");
            var date = new DateTime(2025, 6, 21, 12, 0, 0);
            var sunrise = date.Date.AddHours(6);
            var sunset = date.Date.AddHours(20);
            var solarTimes = new SolarTimes(sunrise, sunset, date.Date, location);

            // Act - midday should be day temperature
            var middayTemp = profile.GetColorTemperatureForTime(date, solarTimes);

            // Assert
            Assert.AreEqual(6500, middayTemp.Kelvin);
        }
    }

    [TestClass]
    public class ColorTemperatureTests
    {
        [TestMethod]
        public void ColorTemperature_ValidKelvin_CreatesSuccessfully()
        {
            // Arrange & Act
            var temp = new ColorTemperature(6500);

            // Assert
            Assert.AreEqual(6500, temp.Kelvin);
            Assert.IsNotNull(temp.RGB);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void ColorTemperature_InvalidKelvin_ThrowsException()
        {
            // Arrange & Act & Assert
            new ColorTemperature(500); // Below minimum
        }

        [TestMethod]
        public void ColorTemperature_WarmLight_HasMoreRed()
        {
            // Arrange & Act
            var warmTemp = new ColorTemperature(2700);
            var coolTemp = new ColorTemperature(6500);

            // Assert
            Assert.IsTrue(warmTemp.RGB.R >= coolTemp.RGB.R, "Warm light should have more red");
        }

        [TestMethod]
        public void ColorTemperature_CoolLight_HasMoreBlue()
        {
            // Arrange & Act
            var warmTemp = new ColorTemperature(2700);
            var coolTemp = new ColorTemperature(6500);

            // Assert
            Assert.IsTrue(coolTemp.RGB.B >= warmTemp.RGB.B, "Cool light should have more blue");
        }
    }

    [TestClass]
    public class SolarTimesTests
    {
        [TestMethod]
        public void SolarTimes_ValidTimes_CreatesSuccessfully()
        {
            // Arrange
            var location = new Location(40.7128, -74.0060, "New York", "USA");
            var date = new DateTime(2025, 6, 21);
            var sunrise = date.AddHours(6);
            var sunset = date.AddHours(20);

            // Act
            var solarTimes = new SolarTimes(sunrise, sunset, date, location);

            // Assert
            Assert.AreEqual(sunrise, solarTimes.Sunrise);
            Assert.AreEqual(sunset, solarTimes.Sunset);
            Assert.AreEqual(date, solarTimes.Date);
            Assert.AreEqual(location, solarTimes.Location);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void SolarTimes_SunriseAfterSunset_ThrowsException()
        {
            // Arrange
            var location = new Location(40.7128, -74.0060, "New York", "USA");
            var date = new DateTime(2025, 6, 21);
            var sunrise = date.AddHours(20);
            var sunset = date.AddHours(6);

            // Act & Assert
            new SolarTimes(sunrise, sunset, date, location);
        }

        [TestMethod]
        public void SolarTimes_CalculatesProperties_Correctly()
        {
            // Arrange
            var location = new Location(40.7128, -74.0060, "New York", "USA");
            var date = new DateTime(2025, 6, 21);
            var sunrise = date.AddHours(6);
            var sunset = date.AddHours(20);

            // Act
            var solarTimes = new SolarTimes(sunrise, sunset, date, location);

            // Assert
            Assert.AreEqual(TimeSpan.FromHours(14), solarTimes.DayLength);
            Assert.AreEqual(TimeSpan.FromHours(10), solarTimes.NightLength);
            Assert.AreEqual(date.AddHours(13), solarTimes.SolarNoon);
        }
    }
}
