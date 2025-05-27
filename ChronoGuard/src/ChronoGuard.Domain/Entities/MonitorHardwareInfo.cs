using System.ComponentModel.DataAnnotations;

namespace ChronoGuard.Domain.Entities;

/// <summary>
/// Comprehensive monitor hardware information including EDID data, WMI information, and capabilities
/// </summary>
public class MonitorHardwareInfo
{
    /// <summary>
    /// Device path identifier
    /// </summary>
    public string DevicePath { get; set; } = string.Empty;

    /// <summary>
    /// Monitor display name
    /// </summary>
    public string MonitorName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is the primary monitor
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Physical position on desktop (X coordinate)
    /// </summary>
    public int PositionX { get; set; }

    /// <summary>
    /// Physical position on desktop (Y coordinate)
    /// </summary>
    public int PositionY { get; set; }

    /// <summary>
    /// Current physical width in pixels
    /// </summary>
    public int PhysicalWidth { get; set; }

    /// <summary>
    /// Current physical height in pixels
    /// </summary>
    public int PhysicalHeight { get; set; }

    /// <summary>
    /// Native (maximum) width resolution
    /// </summary>
    public int NativeWidth { get; set; }

    /// <summary>
    /// Native (maximum) height resolution
    /// </summary>
    public int NativeHeight { get; set; }

    /// <summary>
    /// Current width resolution
    /// </summary>
    public int CurrentWidth { get; set; }

    /// <summary>
    /// Current height resolution
    /// </summary>
    public int CurrentHeight { get; set; }

    /// <summary>
    /// Current refresh rate in Hz
    /// </summary>
    public int CurrentRefreshRate { get; set; }

    /// <summary>
    /// Maximum supported refresh rate in Hz
    /// </summary>
    public int MaxRefreshRate { get; set; }

    /// <summary>
    /// Current bits per pixel (color depth)
    /// </summary>
    public int CurrentBitsPerPixel { get; set; }

    /// <summary>
    /// Current display orientation
    /// </summary>
    public int CurrentOrientation { get; set; }

    /// <summary>
    /// List of all available display modes
    /// </summary>
    public List<DisplayMode> AvailableDisplayModes { get; set; } = new();

    #region Manufacturer and Model Information

    /// <summary>
    /// Monitor manufacturer name from WMI
    /// </summary>
    public string ManufacturerName { get; set; } = "Unknown";

    /// <summary>
    /// Monitor model name from WMI
    /// </summary>
    public string ModelName { get; set; } = "Unknown";

    /// <summary>
    /// Monitor description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Display adapter name
    /// </summary>
    public string AdapterName { get; set; } = string.Empty;

    /// <summary>
    /// Display adapter registry key
    /// </summary>
    public string AdapterKey { get; set; } = string.Empty;

    /// <summary>
    /// Monitor registry key
    /// </summary>
    public string MonitorKey { get; set; } = string.Empty;

    /// <summary>
    /// Monitor hardware ID
    /// </summary>
    public string MonitorId { get; set; } = string.Empty;

    /// <summary>
    /// Video controller name
    /// </summary>
    public string VideoControllerName { get; set; } = string.Empty;

    /// <summary>
    /// Video memory type
    /// </summary>
    public int VideoMemoryType { get; set; }

    /// <summary>
    /// Video memory size in bytes
    /// </summary>
    public long VideoMemorySize { get; set; }

    #endregion

    #region EDID Information

    /// <summary>
    /// Manufacturer from EDID data
    /// </summary>
    public string EDIDManufacturer { get; set; } = string.Empty;

    /// <summary>
    /// Product code from EDID
    /// </summary>
    public string EDIDProductCode { get; set; } = string.Empty;

    /// <summary>
    /// Friendly name from EDID
    /// </summary>
    public string EDIDFriendlyName { get; set; } = string.Empty;

    /// <summary>
    /// Year of manufacture
    /// </summary>
    public ushort YearOfManufacture { get; set; }

    /// <summary>
    /// Week of manufacture
    /// </summary>
    public byte WeekOfManufacture { get; set; }

    #endregion

    #region Physical Monitor (DDC/CI) Information

    /// <summary>
    /// Physical monitor description from DXVA2
    /// </summary>
    public string PhysicalMonitorDescription { get; set; } = string.Empty;

    /// <summary>
    /// DDC/CI capabilities string
    /// </summary>
    public string CapabilitiesString { get; set; } = string.Empty;

    /// <summary>
    /// Whether monitor supports DDC/CI
    /// </summary>
    public bool SupportsDDCCI { get; set; }

    /// <summary>
    /// Whether monitor supports brightness control via DDC/CI
    /// </summary>
    public bool SupportsBrightnessControl { get; set; }

    /// <summary>
    /// Whether monitor supports contrast control via DDC/CI
    /// </summary>
    public bool SupportsContrastControl { get; set; }

    /// <summary>
    /// Whether monitor supports color temperature control via DDC/CI
    /// </summary>
    public bool SupportsColorTemperatureControl { get; set; }

    /// <summary>
    /// Supported color temperature values from DDC/CI
    /// </summary>
    public List<int> SupportedColorTemperatures { get; set; } = new();

    #endregion

    #region Color and Display Capabilities

    /// <summary>
    /// Effective color bit depth
    /// </summary>
    [Range(16, 48)]
    public int BitDepth { get; set; } = 24;

    /// <summary>
    /// Maximum luminance in cd/m² (nits)
    /// </summary>
    [Range(80, 10000)]
    public double MaxLuminance { get; set; } = 250.0;

    /// <summary>
    /// Color gamut coverage (sRGB, DCI-P3, Adobe RGB, Rec. 2020, etc.)
    /// </summary>
    public string ColorGamut { get; set; } = "sRGB";

    /// <summary>
    /// Whether monitor supports HDR
    /// </summary>
    public bool SupportsHDR { get; set; }

    /// <summary>
    /// Whether monitor supports wide color gamut
    /// </summary>
    public bool SupportsWideColorGamut { get; set; }

    /// <summary>
    /// Whether monitor supports hardware gamma adjustment
    /// </summary>
    public bool SupportsHardwareGamma { get; set; } = true;

    /// <summary>
    /// Whether monitor supports ICC color profiles
    /// </summary>
    public bool SupportsICCProfiles { get; set; } = true;

    #endregion

    #region Physical Dimensions

    /// <summary>
    /// Physical width in millimeters (if available)
    /// </summary>
    public double PhysicalWidthMM { get; set; }

    /// <summary>
    /// Physical height in millimeters (if available)
    /// </summary>
    public double PhysicalHeightMM { get; set; }

    /// <summary>
    /// Pixel density in PPI (pixels per inch)
    /// </summary>
    public double PixelDensity { get; set; }

    /// <summary>
    /// Diagonal size in inches
    /// </summary>
    public double DiagonalSize { get; set; }

    #endregion

    #region Timing and Synchronization

    /// <summary>
    /// Horizontal sync frequency range (min-max in kHz)
    /// </summary>
    public (double Min, double Max) HorizontalSyncRange { get; set; }

    /// <summary>
    /// Vertical sync frequency range (min-max in Hz)
    /// </summary>
    public (double Min, double Max) VerticalSyncRange { get; set; }

    /// <summary>
    /// Maximum pixel clock in MHz
    /// </summary>
    public double MaxPixelClock { get; set; }

    #endregion

    #region Status and Health

    /// <summary>
    /// Last time monitor information was updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    /// <summary>
    /// Whether monitor is currently active/connected
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Connection type (VGA, DVI, HDMI, DisplayPort, etc.)
    /// </summary>
    public string ConnectionType { get; set; } = "Unknown";

    /// <summary>
    /// Monitor health status
    /// </summary>
    public MonitorHealthStatus HealthStatus { get; set; } = MonitorHealthStatus.Healthy;

    /// <summary>
    /// Health check messages
    /// </summary>
    public List<string> HealthMessages { get; set; } = new();

    #endregion

    /// <summary>
    /// Calculate pixel density if physical dimensions are known
    /// </summary>
    public void CalculatePixelDensity()
    {
        if (PhysicalWidthMM > 0 && PhysicalHeightMM > 0 && NativeWidth > 0 && NativeHeight > 0)
        {
            var widthInches = PhysicalWidthMM / 25.4;
            var heightInches = PhysicalHeightMM / 25.4;
            DiagonalSize = Math.Sqrt(widthInches * widthInches + heightInches * heightInches);
            
            var diagonalPixels = Math.Sqrt(NativeWidth * NativeWidth + NativeHeight * NativeHeight);
            PixelDensity = diagonalPixels / DiagonalSize;
        }
    }

    /// <summary>
    /// Perform basic health checks on monitor configuration
    /// </summary>
    public void PerformHealthCheck()
    {
        HealthMessages.Clear();
        HealthStatus = MonitorHealthStatus.Healthy;

        // Check for common issues
        if (CurrentWidth == 0 || CurrentHeight == 0)
        {
            HealthMessages.Add("Monitor has invalid resolution settings");
            HealthStatus = MonitorHealthStatus.Warning;
        }

        if (CurrentRefreshRate < 30)
        {
            HealthMessages.Add("Monitor refresh rate is unusually low");
            HealthStatus = MonitorHealthStatus.Warning;
        }

        if (CurrentRefreshRate > 240)
        {
            HealthMessages.Add("Monitor refresh rate is unusually high - verify settings");
            HealthStatus = MonitorHealthStatus.Info;
        }

        if (CurrentBitsPerPixel < 24)
        {
            HealthMessages.Add("Monitor color depth is below 24-bit");
            HealthStatus = MonitorHealthStatus.Warning;
        }

        if (!SupportsHardwareGamma)
        {
            HealthMessages.Add("Monitor does not support hardware gamma adjustment");
            HealthStatus = MonitorHealthStatus.Info;
        }

        if (string.IsNullOrEmpty(ManufacturerName) || ManufacturerName == "Unknown")
        {
            HealthMessages.Add("Monitor manufacturer information unavailable");
            HealthStatus = MonitorHealthStatus.Info;
        }

        if (HealthMessages.Count == 0)
        {
            HealthMessages.Add("Monitor configuration is optimal");
        }
    }

    /// <summary>
    /// Get a summary string of monitor capabilities
    /// </summary>
    public string GetCapabilitiesSummary()
    {
        var capabilities = new List<string>();

        if (SupportsHDR) capabilities.Add("HDR");
        if (SupportsWideColorGamut) capabilities.Add("Wide Color Gamut");
        if (SupportsDDCCI) capabilities.Add("DDC/CI");
        if (SupportsHardwareGamma) capabilities.Add("Hardware Gamma");
        if (SupportsICCProfiles) capabilities.Add("ICC Profiles");
        if (SupportsBrightnessControl) capabilities.Add("Brightness Control");
        if (SupportsContrastControl) capabilities.Add("Contrast Control");
        if (SupportsColorTemperatureControl) capabilities.Add("Temperature Control");

        return string.Join(", ", capabilities);
    }

    /// <summary>
    /// Get display information summary
    /// </summary>
    public string GetDisplaySummary()
    {
        return $"{NativeWidth}x{NativeHeight} @ {MaxRefreshRate}Hz ({BitDepth}-bit) - {ColorGamut}";
    }

    public override string ToString()
    {
        return $"{ManufacturerName} {ModelName} ({GetDisplaySummary()})";
    }
}

/// <summary>
/// Monitor health status enumeration
/// </summary>
public enum MonitorHealthStatus
{
    Healthy,
    Info,
    Warning,
    Error,
    Critical
}
