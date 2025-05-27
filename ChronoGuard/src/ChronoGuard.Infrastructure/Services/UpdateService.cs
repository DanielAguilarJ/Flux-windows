using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;

namespace ChronoGuard.Infrastructure.Services
{
    /// <summary>
    /// Servicio de actualizaciones automáticas para ChronoGuard
    /// </summary>
    public class UpdateService : IUpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UpdateService> _logger;
        private readonly IConfigurationService _configurationService;
        private readonly string _updateUrl = "https://api.github.com/repos/DanielAguilarJ/ChronoGuard/releases/latest";
        private readonly string _appDataPath;
        private System.Timers.Timer? _updateTimer;

        public event EventHandler<UpdateProgressEventArgs>? UpdateProgressChanged;
        public event EventHandler? UpdateCompleted;
        public event EventHandler<UpdateFailedEventArgs>? UpdateFailed;

        public UpdateService(
            HttpClient httpClient,
            ILogger<UpdateService> logger,
            IConfigurationService configurationService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configurationService = configurationService;
            _appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ChronoGuard", "Updates");
            
            Directory.CreateDirectory(_appDataPath);
            
            // Configurar User-Agent para GitHub API
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ChronoGuard/1.0");
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                OnUpdateProgress(UpdateStatus.Checking, 0, "Verificando actualizaciones...");
                
                var response = await _httpClient.GetAsync(_updateUrl);
                response.EnsureSuccessStatusCode();
                
                var jsonContent = await response.Content.ReadAsStringAsync();
                var releaseInfo = JsonSerializer.Deserialize<GitHubRelease>(jsonContent);
                
                if (releaseInfo == null)
                {
                    OnUpdateProgress(UpdateStatus.UpToDate, 100, "No se pudo obtener información de actualizaciones");
                    return null;
                }

                var currentVersion = GetCurrentVersion();
                if (IsNewerVersion(releaseInfo.TagName, currentVersion))
                {
                    var asset = GetSuitableAsset(releaseInfo.Assets);
                    if (asset != null)
                    {
                        var updateInfo = new UpdateInfo
                        {
                            IsUpdateAvailable = true,
                            Version = releaseInfo.TagName,
                            DownloadUrl = asset.BrowserDownloadUrl,
                            ReleaseNotes = releaseInfo.Body,
                            ReleaseDate = releaseInfo.PublishedAt,
                            FileSize = asset.Size,
                            Type = DetermineUpdateType(currentVersion, releaseInfo.TagName),
                            IsSecurityUpdate = releaseInfo.Body.Contains("security", StringComparison.OrdinalIgnoreCase),
                            IsMandatory = releaseInfo.Body.Contains("mandatory", StringComparison.OrdinalIgnoreCase)
                        };

                        OnUpdateProgress(UpdateStatus.Available, 100, $"Actualización disponible: {updateInfo.Version}");
                        return updateInfo;
                    }
                }

                OnUpdateProgress(UpdateStatus.UpToDate, 100, "La aplicación está actualizada");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar actualizaciones");
                OnUpdateProgress(UpdateStatus.Failed, 0, $"Error al verificar actualizaciones: {ex.Message}", ex);
                return null;
            }
        }

        public async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo)
        {
            try
            {
                OnUpdateProgress(UpdateStatus.Downloading, 0, "Descargando actualización...");
                
                var fileName = Path.GetFileName(new Uri(updateInfo.DownloadUrl).LocalPath);
                var downloadPath = Path.Combine(_appDataPath, fileName);
                
                // Descargar el archivo de actualización
                using (var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = File.Create(downloadPath))
                    {
                        var buffer = new byte[8192];
                        var totalBytesRead = 0L;
                        int bytesRead;
                        
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                            
                            if (totalBytes > 0)
                            {
                                var progress = (int)((totalBytesRead * 100) / totalBytes);
                                OnUpdateProgress(UpdateStatus.Downloading, progress, 
                                    $"Descargando... {progress}% ({totalBytesRead:N0}/{totalBytes:N0} bytes)");
                            }
                        }
                    }
                }

                // Verificar integridad del archivo
                if (await VerifyFileIntegrityAsync(downloadPath, updateInfo))
                {
                    OnUpdateProgress(UpdateStatus.Installing, 90, "Preparando instalación...");
                    
                    // Crear script de instalación
                    var scriptPath = CreateInstallationScript(downloadPath);
                    
                    OnUpdateProgress(UpdateStatus.Installing, 95, "Iniciando instalación...");
                    
                    // Ejecutar instalación
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = scriptPath,
                        UseShellExecute = true,
                        Verb = "runas" // Solicitar permisos de administrador
                    };
                    
                    Process.Start(startInfo);
                    
                    OnUpdateProgress(UpdateStatus.Completed, 100, "Actualización iniciada. La aplicación se reiniciará.");
                    
                    // Guardar historial de actualización
                    await SaveUpdateHistoryAsync(updateInfo);
                    
                    // Disparar evento de actualización completada
                    UpdateCompleted?.Invoke(this, EventArgs.Empty);
                    
                    // Cerrar la aplicación para permitir la actualización
                    Environment.Exit(0);
                    
                    return true;
                }
                else
                {
                    OnUpdateProgress(UpdateStatus.Failed, 0, "Error de integridad del archivo de actualización");
                    File.Delete(downloadPath);
                    
                    // Disparar evento de fallo de actualización
                    UpdateFailed?.Invoke(this, new UpdateFailedEventArgs 
                    { 
                        Message = "Error de integridad del archivo de actualización"
                    });
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar/instalar actualización");
                OnUpdateProgress(UpdateStatus.Failed, 0, $"Error durante la actualización: {ex.Message}", ex);
                
                // Disparar evento de fallo de actualización
                UpdateFailed?.Invoke(this, new UpdateFailedEventArgs 
                { 
                    Message = $"Error durante la actualización: {ex.Message}",
                    Exception = ex 
                });
                
                return false;
            }
        }

        public string GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0.0";
        }

        public async Task ConfigureAutoUpdateAsync(bool enabled, bool includePreReleases = false, int checkInterval = 24)
        {
            var config = await _configurationService.GetConfigurationAsync();
            config.General.AutoUpdateEnabled = enabled;
            config.General.CheckPreReleases = includePreReleases;
            config.General.UpdateCheckInterval = checkInterval;
            await _configurationService.SaveConfigurationAsync(config);

            if (enabled)
            {
                await ScheduleUpdateCheckAsync();
            }
            else
            {
                _updateTimer?.Stop();
                _updateTimer?.Dispose();
                _updateTimer = null;
            }
        }

        public async Task ScheduleUpdateCheckAsync()
        {
            var config = await _configurationService.GetConfigurationAsync();
            if (!config.General.AutoUpdateEnabled) return;

            _updateTimer?.Stop();
            _updateTimer?.Dispose();

            _updateTimer = new System.Timers.Timer(TimeSpan.FromHours(config.General.UpdateCheckInterval).TotalMilliseconds);
            _updateTimer.Elapsed += async (_, _) =>
            {
                try
                {
                    var updateInfo = await CheckForUpdatesAsync();
                    if (updateInfo != null && updateInfo.IsUpdateAvailable)
                    {
                        // Mostrar notificación de actualización disponible
                        // Esto debería integrarse con el servicio de notificaciones
                        _logger.LogInformation($"Actualización disponible: {updateInfo.Version}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error durante verificación automática de actualizaciones");
                }
            };
            
            _updateTimer.Start();
        }

        public async Task<IEnumerable<UpdateInfo>> GetUpdateHistoryAsync()
        {
            var historyPath = Path.Combine(_appDataPath, "update_history.json");
            if (!File.Exists(historyPath))
                return new List<UpdateInfo>();

            try
            {
                var json = await File.ReadAllTextAsync(historyPath);
                return JsonSerializer.Deserialize<List<UpdateInfo>>(json) ?? new List<UpdateInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al leer historial de actualizaciones");
                return new List<UpdateInfo>();
            }
        }

        private void OnUpdateProgress(UpdateStatus status, int progress, string message, Exception? error = null)
        {
            UpdateProgressChanged?.Invoke(this, new UpdateProgressEventArgs
            {
                Status = status,
                ProgressPercentage = progress,
                Message = message,
                Error = error
            });
        }

        private bool IsNewerVersion(string remoteVersion, string currentVersion)
        {
            // Normalizar versiones (remover prefijos como 'v')
            var cleanRemote = remoteVersion.TrimStart('v');
            var cleanCurrent = currentVersion.TrimStart('v');

            try
            {
                var remote = new Version(cleanRemote);
                var current = new Version(cleanCurrent);
                return remote > current;
            }
            catch
            {
                // Si hay error en el parsing, comparar como strings
                return string.Compare(cleanRemote, cleanCurrent, StringComparison.OrdinalIgnoreCase) > 0;
            }
        }

        private UpdateType DetermineUpdateType(string currentVersion, string newVersion)
        {
            try
            {
                var current = new Version(currentVersion.TrimStart('v'));
                var next = new Version(newVersion.TrimStart('v'));

                if (next.Major > current.Major)
                    return UpdateType.Major;
                if (next.Minor > current.Minor)
                    return UpdateType.Minor;
                return UpdateType.Patch;
            }
            catch
            {
                return UpdateType.Minor;
            }
        }

        private GitHubAsset? GetSuitableAsset(List<GitHubAsset> assets)
        {
            // Preferir instalador MSI para Windows
            var msiAsset = assets.FirstOrDefault(a => 
                a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) &&
                a.Name.Contains("x64", StringComparison.OrdinalIgnoreCase));
            
            if (msiAsset != null) return msiAsset;

            // Alternativamente, buscar archivo portable
            return assets.FirstOrDefault(a => 
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                a.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase));
        }

        private async Task<bool> VerifyFileIntegrityAsync(string filePath, UpdateInfo updateInfo)
        {
            if (string.IsNullOrEmpty(updateInfo.Hash))
                return true; // No hay hash para verificar

            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hash = await Task.Run(() => sha256.ComputeHash(stream));
                var fileHash = Convert.ToHexString(hash);
                
                return string.Equals(fileHash, updateInfo.Hash, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar integridad del archivo");
                return false;
            }
        }

        private string CreateInstallationScript(string installerPath)
        {
            var scriptPath = Path.Combine(_appDataPath, "install_update.bat");
            var scriptContent = $@"
@echo off
echo Instalando ChronoGuard - Actualización Automática
echo.
timeout /t 3 /nobreak >nul
echo Cerrando procesos de ChronoGuard...
taskkill /f /im ChronoGuard.App.exe 2>nul
timeout /t 2 /nobreak >nul
echo.
echo Ejecutando instalador...
""{installerPath}"" /quiet /norestart
if %errorlevel% equ 0 (
    echo.
    echo Actualización completada exitosamente.
    echo Iniciando ChronoGuard...
    start """" ""%ProgramFiles%\ChronoGuard\ChronoGuard.App.exe""
) else (
    echo.
    echo Error durante la instalación. Código: %errorlevel%
    pause
)
del ""{installerPath}""
del ""%~f0""
";

            File.WriteAllText(scriptPath, scriptContent);
            return scriptPath;
        }

        private async Task SaveUpdateHistoryAsync(UpdateInfo updateInfo)
        {
            try
            {
                var history = (await GetUpdateHistoryAsync()).ToList();
                history.Insert(0, updateInfo);
                
                // Mantener solo los últimos 10 registros
                if (history.Count > 10)
                    history = history.Take(10).ToList();

                var historyPath = Path.Combine(_appDataPath, "update_history.json");
                var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(historyPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar historial de actualizaciones");
            }
        }

        public void Dispose()
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            _httpClient?.Dispose();
        }
    }

    // Clases para deserializar respuesta de GitHub API
    internal class GitHubRelease
    {
        public string TagName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public bool Draft { get; set; }
        public bool Prerelease { get; set; }
        public List<GitHubAsset> Assets { get; set; } = new();
    }

    internal class GitHubAsset
    {
        public string Name { get; set; } = string.Empty;
        public string BrowserDownloadUrl { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
    }
}
