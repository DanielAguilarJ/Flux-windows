using ChronoGuard.Domain.Entities;

namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Service for applying color temperature changes to the display
/// </summary>
public interface IColorTemperatureService
{
    /// <summary>
    /// Applies a color temperature to all displays
    /// </summary>
    Task<bool> ApplyTemperatureAsync(ColorTemperature temperature);

    /// <summary>
    /// Applies a color temperature to a specific monitor
    /// </summary>
    Task<bool> ApplyTemperatureToMonitorAsync(string monitorId, ColorTemperature temperature);

    /// <summary>
    /// Creates and starts a color temperature transition
    /// </summary>
    Task<TransitionState> CreateTransitionAsync(ColorTemperature from, ColorTemperature to, TimeSpan duration, string reason = "");

    /// <summary>
    /// Stops any active transition and applies the target temperature immediately
    /// </summary>
    Task StopTransitionAsync();

    /// <summary>
    /// Restores the original display gamma/color settings
    /// </summary>
    Task RestoreOriginalSettingsAsync();

    /// <summary>
    /// Gets the currently applied color temperature
    /// </summary>
    ColorTemperature? GetCurrentTemperature();

    /// <summary>
    /// Gets information about available monitors
    /// </summary>
    Task<IEnumerable<MonitorInfo>> GetMonitorsAsync();

    /// <summary>
    /// Gets extended information about available monitors with advanced capabilities
    /// </summary>
    Task<IEnumerable<ExtendedMonitorInfo>> GetExtendedMonitorsAsync();

    /// <summary>
    /// Event raised when color temperature changes
    /// </summary>
    event EventHandler<ColorTemperature>? TemperatureChanged;

    /// <summary>
    /// Event raised when a transition completes
    /// </summary>
    event EventHandler<TransitionState>? TransitionCompleted;
}

/// <summary>
/// Information about a display monitor
/// </summary>
public class MonitorInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string DevicePath { get; set; } = string.Empty;
}

/// <summary>
/// Extended monitor information with advanced capabilities
/// </summary>
public class ExtendedMonitorInfo : MonitorInfo
{
    public string ManufacturerName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public bool SupportsHardwareGamma { get; set; } = true;
    public bool SupportsICCProfiles { get; set; } = false;
    public bool SupportsDDCCI { get; set; } = false;
    public int BitDepth { get; set; } = 8;
    public double MaxLuminance { get; set; } = 250.0; // cd/m²
    public string ColorGamut { get; set; } = "sRGB";
    public DateTime LastCalibrationDate { get; set; } = DateTime.MinValue;
    public string? ICCProfilePath { get; set; }
}
