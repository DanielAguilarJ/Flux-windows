using ChronoGuard.Domain.Interfaces;

namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Service for managing application startup behavior
/// </summary>
public interface IStartupManager
{
    /// <summary>
    /// Enables automatic startup of the application
    /// </summary>
    Task<bool> EnableAutoStartAsync();

    /// <summary>
    /// Disables automatic startup of the application
    /// </summary>
    Task<bool> DisableAutoStartAsync();

    /// <summary>
    /// Checks if automatic startup is currently enabled
    /// </summary>
    Task<bool> IsAutoStartEnabledAsync();

    /// <summary>
    /// Repairs auto-start if registry entry is corrupted
    /// </summary>
    Task<bool> RepairAutoStartAsync();
}
