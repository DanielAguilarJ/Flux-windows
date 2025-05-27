using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChronoGuard.Domain.Entities;

namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Estados del proceso de actualización
/// </summary>
public enum UpdateStatus
{
    Checking,
    Available,
    Downloading,
    Installing,
    Completed,
    Failed,
    UpToDate
}

/// <summary>
/// Argumentos para eventos de progreso de actualización
/// </summary>
public class UpdateProgressEventArgs : EventArgs
{
    public UpdateStatus Status { get; set; }
    public int ProgressPercentage { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Error { get; set; }
}

/// <summary>
/// Argumentos para eventos de fallo de actualización
/// </summary>
public class UpdateFailedEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}

/// <summary>
/// Servicio para gestionar actualizaciones automáticas de la aplicación
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Evento que se dispara cuando hay progreso en el proceso de actualización
    /// </summary>
    event EventHandler<UpdateProgressEventArgs>? UpdateProgressChanged;

    /// <summary>
    /// Evento que se dispara cuando la actualización se completa exitosamente
    /// </summary>
    event EventHandler? UpdateCompleted;

    /// <summary>
    /// Evento que se dispara cuando la actualización falla
    /// </summary>
    event EventHandler<UpdateFailedEventArgs>? UpdateFailed;

    /// <summary>
    /// Verifica si hay actualizaciones disponibles
    /// </summary>
    /// <returns>Información de la actualización disponible o null si no hay actualizaciones</returns>
    Task<UpdateInfo?> CheckForUpdatesAsync();

    /// <summary>
    /// Descarga e instala la actualización especificada
    /// </summary>
    /// <param name="updateInfo">Información de la actualización a instalar</param>
    Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo);

    /// <summary>
    /// Obtiene la versión actual de la aplicación
    /// </summary>
    string GetCurrentVersion();

    /// <summary>
    /// Configura las preferencias de actualización automática
    /// </summary>
    /// <param name="enabled">Si las actualizaciones automáticas están habilitadas</param>
    /// <param name="includePreReleases">Si incluir versiones pre-release</param>
    /// <param name="checkInterval">Intervalo de verificación en horas</param>
    Task ConfigureAutoUpdateAsync(bool enabled, bool includePreReleases = false, int checkInterval = 24);

    /// <summary>
    /// Programa la verificación automática de actualizaciones
    /// </summary>
    Task ScheduleUpdateCheckAsync();

    /// <summary>
    /// Obtiene el registro de actualizaciones recientes
    /// </summary>
    Task<IEnumerable<UpdateInfo>> GetUpdateHistoryAsync();
}
