using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChronoGuard.Infrastructure.Services
{
    /// <summary>
    /// Background service that manages automatic updates and notifications
    /// </summary>
    public class UpdateNotificationService : BackgroundService
    {
        private readonly IUpdateService _updateService;
        private readonly INotificationService _notificationService;
        private readonly IConfigurationService _configurationService;
        private readonly ILogger<UpdateNotificationService> _logger;
        private readonly System.Threading.Timer _updateCheckTimer;
        private bool _updateInProgress = false;

        public UpdateNotificationService(
            IUpdateService updateService,
            INotificationService notificationService,
            IConfigurationService configurationService,
            ILogger<UpdateNotificationService> logger)
        {
            _updateService = updateService;
            _notificationService = notificationService;
            _configurationService = configurationService;
            _logger = logger;
            
            // Initialize timer for periodic update checks
            _updateCheckTimer = new System.Threading.Timer(CheckForUpdatesCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await StartUpdateScheduler(stoppingToken);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task StartUpdateScheduler(CancellationToken cancellationToken)
        {
            try
            {
                var config = await _configurationService.GetConfigurationAsync();
                var updateSettings = config.General;
                
                if (!updateSettings.AutoUpdateEnabled)
                {
                    _logger.LogInformation("Actualizaciones automáticas deshabilitadas");
                    return;
                }

                // Check immediately on startup
                _ = Task.Run(async () => await CheckForUpdatesAsync(), cancellationToken);
                
                // Schedule periodic checks
                var checkInterval = TimeSpan.FromHours(updateSettings.UpdateCheckInterval);
                _updateCheckTimer.Change(checkInterval, checkInterval);
                
                _logger.LogInformation("Programador de actualizaciones iniciado. Verificando cada {Hours} horas", 
                    updateSettings.UpdateCheckInterval);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al inicializar el programador de actualizaciones");
            }
        }

        private async void CheckForUpdatesCallback(object? state)
        {
            try
            {
                await CheckForUpdatesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en verificación programada de actualizaciones");
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            if (_updateInProgress)
            {
                _logger.LogDebug("Verificación de actualización ya en progreso, omitiendo");
                return;
            }

            try
            {
                _updateInProgress = true;
                _logger.LogDebug("Verificando actualizaciones disponibles...");

                var updateInfo = await _updateService.CheckForUpdatesAsync();
                
                if (updateInfo == null)
                {
                    _logger.LogDebug("No se encontraron actualizaciones");
                    return;
                }

                await HandleUpdateAvailable(updateInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar actualizaciones");
                await _notificationService.ShowErrorAsync(
                    "Error de Actualización",
                    "No se pudo verificar si hay actualizaciones disponibles");
            }
            finally
            {
                _updateInProgress = false;
            }
        }

        private async Task HandleUpdateAvailable(UpdateInfo updateInfo)
        {
            try
            {
                var currentVersion = _updateService.GetCurrentVersion();
                var isNewerVersion = Version.Parse(updateInfo.Version) > Version.Parse(currentVersion);
                
                if (!isNewerVersion)
                {
                    _logger.LogDebug("La versión actual {Current} está actualizada respecto a {Available}", 
                        currentVersion, updateInfo.Version);
                    return;
                }

                _logger.LogInformation("Nueva versión disponible: {Version}", updateInfo.Version);

                // Show notification about available update
                await _notificationService.ShowInfoAsync(
                    "Actualización Disponible",
                    $"ChronoGuard {updateInfo.Version} está disponible. {updateInfo.Description}");

                // Check if we should auto-install
                var config = await _configurationService.GetConfigurationAsync();
                if (ShouldAutoInstall(updateInfo, config))
                {
                    await PerformAutoUpdate(updateInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al manejar actualización disponible");
            }
        }

        private bool ShouldAutoInstall(UpdateInfo updateInfo, AppConfiguration config)
        {
            // Don't auto-install pre-releases unless explicitly enabled
            if (updateInfo.IsPreRelease && !config.General.CheckPreReleases)
            {
                return false;
            }

            // Only auto-install patch versions for security
            var currentVersion = Version.Parse(_updateService.GetCurrentVersion());
            var newVersion = Version.Parse(updateInfo.Version);
            
            // Auto-install only patch versions (x.x.PATCH) to minimize risk
            return currentVersion.Major == newVersion.Major && 
                   currentVersion.Minor == newVersion.Minor &&
                   newVersion.Build > currentVersion.Build;
        }

        private async Task PerformAutoUpdate(UpdateInfo updateInfo)
        {
            try
            {
                _logger.LogInformation("Iniciando actualización automática a versión {Version}", updateInfo.Version);

                await _notificationService.ShowInfoAsync(
                    "Actualizando ChronoGuard",
                    "Descargando e instalando actualización automáticamente...");

                // Subscribe to progress events
                _updateService.UpdateProgressChanged += OnUpdateProgressChanged;
                _updateService.UpdateCompleted += OnUpdateCompleted;
                _updateService.UpdateFailed += OnUpdateFailed;

                try
                {
                    await _updateService.DownloadAndInstallUpdateAsync(updateInfo);
                }
                finally
                {
                    // Unsubscribe from events
                    _updateService.UpdateProgressChanged -= OnUpdateProgressChanged;
                    _updateService.UpdateCompleted -= OnUpdateCompleted;
                    _updateService.UpdateFailed -= OnUpdateFailed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la actualización automática");
                await _notificationService.ShowErrorAsync(
                    "Error de Actualización",
                    "La actualización automática falló. Puede intentar actualizar manualmente.");
            }
        }

        private async void OnUpdateProgressChanged(object? sender, UpdateProgressEventArgs e)
        {
            try
            {
                _logger.LogDebug("Progreso de actualización: {Progress}% - {Status}", e.ProgressPercentage, e.Status);
                
                // Show progress notification every 25%
                if (e.ProgressPercentage % 25 == 0 && e.ProgressPercentage > 0)
                {
                    await _notificationService.ShowInfoAsync(
                        "Actualizando ChronoGuard",
                        $"Progreso: {e.ProgressPercentage}% - {e.Status}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al mostrar progreso de actualización");
            }
        }

        private async void OnUpdateCompleted(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("Actualización completada exitosamente");
                
                await _notificationService.ShowInfoAsync(
                    "Actualización Completada",
                    "ChronoGuard se ha actualizado exitosamente. Reinicie la aplicación para usar la nueva versión.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al notificar finalización de actualización");
            }
        }

        private async void OnUpdateFailed(object? sender, UpdateFailedEventArgs e)
        {
            try
            {
                _logger.LogError(e.Exception, "La actualización falló: {Message}", e.Message);
                
                await _notificationService.ShowErrorAsync(
                    "Error de Actualización",
                    $"La actualización falló: {e.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al notificar fallo de actualización");
            }
        }

        public override void Dispose()
        {
            _updateCheckTimer?.Dispose();
            base.Dispose();
        }
    }
}
