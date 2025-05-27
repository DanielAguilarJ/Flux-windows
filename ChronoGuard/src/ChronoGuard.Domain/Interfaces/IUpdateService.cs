using ChronoGuard.Domain.Entities;

namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Service for managing application updates
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks for available updates
    /// </summary>
    Task<UpdateInfo> CheckForUpdatesAsync();
}
