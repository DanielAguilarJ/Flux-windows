using System;

namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Information about a foreground application
/// </summary>
public class ForegroundAppInfo
{
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public int ProcessId { get; set; }
}

/// <summary>
/// Service for detecting the currently active foreground application
/// </summary>
public interface IForegroundApplicationService
{
    /// <summary>
    /// Gets the name of the currently active foreground application
    /// </summary>
    /// <returns>The process name of the foreground application, or null if detection fails</returns>
    string? GetForegroundApplicationName();

    /// <summary>
    /// Gets detailed information about the foreground application
    /// </summary>
    /// <returns>Application information including process name, window title, and executable path</returns>
    ForegroundAppInfo? GetForegroundApplicationInfo();

    /// <summary>
    /// Event fired when the foreground application changes
    /// </summary>
    event EventHandler<string>? ForegroundApplicationChanged;

    /// <summary>
    /// Starts monitoring for foreground application changes
    /// </summary>
    void StartMonitoring();

    /// <summary>
    /// Stops monitoring for foreground application changes
    /// </summary>
    void StopMonitoring();

    /// <summary>
    /// Gets whether monitoring is currently active
    /// </summary>
    bool IsMonitoring { get; }
}
