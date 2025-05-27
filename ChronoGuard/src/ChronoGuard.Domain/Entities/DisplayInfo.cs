namespace ChronoGuard.Domain.Entities;

/// <summary>
/// Display mode information
/// </summary>
public class DisplayMode
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; }
    public int BitsPerPixel { get; set; }

    public override string ToString()
    {
        return $"{Width}x{Height} @ {RefreshRate}Hz ({BitsPerPixel}-bit)";
    }
}

/// <summary>
/// EDID (Extended Display Identification Data) information
/// </summary>
public class EDIDInfo
{
    public string ManufacturerID { get; set; } = string.Empty;
    public ushort ProductCode { get; set; }
    public uint SerialNumber { get; set; }
    public byte WeekOfManufacture { get; set; }
    public ushort YearOfManufacture { get; set; }
    public byte EDIDVersion { get; set; }
    public byte EDIDRevision { get; set; }

    // Display characteristics
    public double DisplaySizeX { get; set; } // cm
    public double DisplaySizeY { get; set; } // cm
    public byte DisplayGamma { get; set; }

    // Color characteristics
    public double RedChromaticityX { get; set; }
    public double RedChromaticityY { get; set; }
    public double GreenChromaticityX { get; set; }
    public double GreenChromaticityY { get; set; }
    public double BlueChromaticityX { get; set; }
    public double BlueChromaticityY { get; set; }
    public double WhitePointX { get; set; }
    public double WhitePointY { get; set; }

    // Timing information
    public List<DetailedTiming> SupportedTimings { get; set; } = new();

    // Monitor name and serial
    public string MonitorName { get; set; } = string.Empty;
    public string MonitorSerialNumber { get; set; } = string.Empty;

    // Resolution information for backward compatibility
    public int HorizontalResolution { get; set; }
    public int VerticalResolution { get; set; }

    public override string ToString()
    {
        return $"{ManufacturerID} {MonitorName} (S/N: {MonitorSerialNumber})";
    }
}

/// <summary>
/// Detailed timing information from EDID
/// </summary>
public class DetailedTiming
{
    public int PixelClock { get; set; }
    public int HorizontalActive { get; set; }
    public int HorizontalBlanking { get; set; }
    public int VerticalActive { get; set; }
    public int VerticalBlanking { get; set; }
    public int HorizontalSync { get; set; }
    public int VerticalSync { get; set; }
    public bool InterlacedMode { get; set; }

    public double RefreshRate => PixelClock * 1000.0 / ((HorizontalActive + HorizontalBlanking) * (VerticalActive + VerticalBlanking));

    public override string ToString()
    {
        return $"{HorizontalActive}x{VerticalActive} @ {RefreshRate:F1}Hz";
    }
}
