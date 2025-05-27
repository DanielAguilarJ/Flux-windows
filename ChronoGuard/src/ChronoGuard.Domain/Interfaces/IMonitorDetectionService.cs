using ChronoGuard.Domain.Entities;

namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Interface for enhanced monitor detection services
/// Provides comprehensive monitor information using WMI, EDID parsing, and Windows APIs
/// </summary>
public interface IMonitorDetectionService
{
    /// <summary>
    /// Detect all monitors with comprehensive hardware information
    /// </summary>
    /// <returns>Collection of detailed monitor information</returns>
    Task<IEnumerable<MonitorHardwareInfo>> DetectMonitorsAsync();

    /// <summary>
    /// Get cached monitor information for a specific device path
    /// </summary>
    /// <param name="devicePath">Monitor device path</param>
    /// <returns>Cached monitor information or null if not found</returns>
    MonitorHardwareInfo? GetCachedMonitorInfo(string devicePath);

    /// <summary>
    /// Clear cached monitor information (force re-detection)
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Get detailed EDID information for a specific monitor
    /// </summary>
    /// <param name="devicePath">Monitor device path</param>
    /// <returns>EDID information or null if unavailable</returns>
    Task<EDIDInfo?> GetMonitorEDIDAsync(string devicePath);
}
