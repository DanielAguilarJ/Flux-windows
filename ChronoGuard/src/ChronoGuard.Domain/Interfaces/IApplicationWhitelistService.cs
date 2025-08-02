using ChronoGuard.Domain.Entities;

namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Service for managing application whitelist with smart detection capabilities
/// </summary>
public interface IApplicationWhitelistService
{
    /// <summary>
    /// Checks if an application is whitelisted (should disable color temperature adjustment)
    /// </summary>
    Task<bool> IsApplicationWhitelistedAsync(string processName);

    /// <summary>
    /// Gets detailed whitelist status for an application
    /// </summary>
    Task<WhitelistStatus> GetWhitelistStatusAsync(string processName);

    /// <summary>
    /// Adds an application to the whitelist
    /// </summary>
    Task AddToWhitelistAsync(string processName, WhitelistMode mode = WhitelistMode.CompleteDisable, string? reason = null);

    /// <summary>
    /// Removes an application from the whitelist
    /// </summary>
    Task RemoveFromWhitelistAsync(string processName);

    /// <summary>
    /// Gets all whitelisted applications
    /// </summary>
    Task<IEnumerable<WhitelistedApplication>> GetWhitelistedApplicationsAsync();

    /// <summary>
    /// Automatically detects applications that should be whitelisted based on their category
    /// </summary>
    Task<IEnumerable<DetectedApplication>> ScanForApplicationsToWhitelistAsync();

    /// <summary>
    /// Applies automatic whitelist detection based on the current foreground application
    /// </summary>
    Task<WhitelistAction> ProcessForegroundApplicationAsync(string processName, string windowTitle, string executablePath);

    /// <summary>
    /// Temporarily disables color temperature adjustment for the current application
    /// </summary>
    Task<bool> TemporaryDisableForCurrentAppAsync(TimeSpan duration);

    /// <summary>
    /// Event raised when whitelist status changes
    /// </summary>
    event EventHandler<WhitelistChangedEventArgs>? WhitelistChanged;
}

/// <summary>
/// Whitelist modes for different types of application handling
/// </summary>
public enum WhitelistMode
{
    /// <summary>
    /// Completely disable color temperature adjustment
    /// </summary>
    CompleteDisable,
    
    /// <summary>
    /// Reduce color temperature effect to 25%
    /// </summary>
    ReducedEffect,
    
    /// <summary>
    /// Pause transitions but maintain current temperature
    /// </summary>
    PauseTransitions,
    
    /// <summary>
    /// Only disable during fullscreen mode
    /// </summary>
    FullscreenOnly
}

/// <summary>
/// Status of an application's whitelist configuration
/// </summary>
public class WhitelistStatus
{
    public bool IsWhitelisted { get; set; }
    public WhitelistMode Mode { get; set; } = WhitelistMode.CompleteDisable;
    public string? Reason { get; set; }
    public DateTime AddedAt { get; set; }
    public bool IsTemporary { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsAutoDetected { get; set; }
}

/// <summary>
/// Represents a whitelisted application
/// </summary>
public class WhitelistedApplication
{
    public string ProcessName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public WhitelistMode Mode { get; set; } = WhitelistMode.CompleteDisable;
    public string? Reason { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public bool IsBuiltIn { get; set; }
    public bool IsAutoDetected { get; set; }
    public ApplicationCategory Category { get; set; } = ApplicationCategory.Unknown;
}

/// <summary>
/// Application categories for smart detection
/// </summary>
public enum ApplicationCategory
{
    Unknown,
    DesignSoftware,
    VideoEditing,
    CAD3D,
    Gaming,
    Photography,
    Development,
    Multimedia,
    Presentation,
    ColorCritical
}

/// <summary>
/// Represents an application detected for potential whitelisting
/// </summary>
public class DetectedApplication
{
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ApplicationCategory Category { get; set; }
    public string DetectionReason { get; set; } = string.Empty;
    public WhitelistMode RecommendedMode { get; set; } = WhitelistMode.CompleteDisable;
    public double ConfidenceScore { get; set; } // 0.0 to 1.0
}

/// <summary>
/// Action taken after processing a foreground application
/// </summary>
public class WhitelistAction
{
    public bool ShouldDisableColorAdjustment { get; set; }
    public WhitelistMode Mode { get; set; } = WhitelistMode.CompleteDisable;
    public string? Reason { get; set; }
    public bool WasAutoDetected { get; set; }
    public bool RequiresUserConfirmation { get; set; }
}

/// <summary>
/// Event arguments for whitelist changes
/// </summary>
public class WhitelistChangedEventArgs : EventArgs
{
    public string ProcessName { get; set; } = string.Empty;
    public bool IsAdded { get; set; }
    public WhitelistMode Mode { get; set; }
    public string? Reason { get; set; }
}