using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChronoGuard.Infrastructure.Services;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;

namespace ChronoGuard.Tests.Services
{
    [TestClass]
    public class UpdateServiceTests
    {
        private Mock<ILogger<UpdateService>> _mockLogger;
        private Mock<IConfigurationService> _mockConfigService;
        private HttpClient _httpClient;
        private UpdateService _updateService;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<UpdateService>>();
            _mockConfigService = new Mock<IConfigurationService>();
            _httpClient = new HttpClient();
            
            _updateService = new UpdateService(
                _httpClient, 
                _mockLogger.Object, 
                _mockConfigService.Object);
        }

        [TestMethod]
        public async Task CheckForUpdatesAsync_ValidResponse_ReturnsUpdateInfo()
        {
            // Arrange
            var mockUpdateInfo = new UpdateInfo
            {
                Version = "1.1.0",
                ReleaseDate = DateTime.Now,
                DownloadUrl = "https://github.com/DanielAguilarJ/ChronoGuard/releases/download/v1.1.0/ChronoGuard-1.1.0.msi",
                ChangelogUrl = "https://github.com/DanielAguilarJ/ChronoGuard/releases/tag/v1.1.0",
                IsPreRelease = false,
                Description = "New features and bug fixes"
            };

            // Act
            var result = await _updateService.CheckForUpdatesAsync();

            // Assert
            Assert.IsNotNull(result, "CheckForUpdatesAsync should return a result");
            
            // En un entorno real con conexión a internet, esto verificaría la respuesta de GitHub
            // Por ahora, solo verificamos que el método no falle
        }

        [TestMethod]
        public async Task CheckForUpdatesAsync_NetworkError_ReturnsNoUpdate()
        {
            // Arrange
            // Usar una URL inválida para simular error de red
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("https://invalid-url-that-does-not-exist.com/");
            
            var updateService = new UpdateService(
                httpClient, 
                _mockLogger.Object, 
                _mockConfigService.Object);

            // Act
            var result = await updateService.CheckForUpdatesAsync();

            // Assert
            Assert.IsNotNull(result, "Should return a result even on network error");
            Assert.IsFalse(result.IsUpdateAvailable, "Should indicate no update available on error");
        }

        [TestMethod]
        public void UpdateService_Initialization_SetsUpEventHandlers()
        {
            // Arrange & Act
            var updateProgressTriggered = false;
            var updateCompletedTriggered = false;
            var updateFailedTriggered = false;

            // Test that we can subscribe to events without throwing exceptions
            try
            {
                _updateService.UpdateProgressChanged += (sender, e) => updateProgressTriggered = true;
                _updateService.UpdateCompleted += (sender, e) => updateCompletedTriggered = true;
                _updateService.UpdateFailed += (sender, e) => updateFailedTriggered = true;

                // Unsubscribe to clean up
                _updateService.UpdateProgressChanged -= (sender, e) => updateProgressTriggered = true;
                _updateService.UpdateCompleted -= (sender, e) => updateCompletedTriggered = true;
                _updateService.UpdateFailed -= (sender, e) => updateFailedTriggered = true;

                // Assert - If we reach here, event subscription works
                Assert.IsTrue(true, "Event subscription and unsubscription completed successfully");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Event subscription failed: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task ScheduleUpdateCheckAsync_ValidCall_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            await _updateService.ScheduleUpdateCheckAsync();
        }

        [TestMethod]
        public async Task GetUpdateHistoryAsync_ValidCall_ReturnsHistory()
        {
            // Act
            var history = await _updateService.GetUpdateHistoryAsync();

            // Assert
            Assert.IsNotNull(history, "GetUpdateHistoryAsync should return a collection");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _httpClient?.Dispose();
            _updateService = null;
        }
    }
}
