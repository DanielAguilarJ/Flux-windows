using System;
using System.Collections.Generic;

namespace ChronoGuard.Domain.Entities;

/// <summary>
/// Advanced monitor color profile with ICC support and hardware calibration
/// Manages per-monitor color temperature settings and calibration data
/// </summary>
public class MonitorColorProfile
{
    public string MonitorId { get; set; } = string.Empty;
    public string MonitorName { get; set; } = string.Empty;
    public string ManufacturerName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string DevicePath { get; set; } = string.Empty;
    
    // Hardware capabilities
    public bool SupportsHardwareGamma { get; set; } = true;
    public bool SupportsICCProfiles { get; set; } = false;
    public bool SupportsDDCCI { get; set; } = false;
    public int BitDepth { get; set; } = 8;
    public double MaxLuminance { get; set; } = 250.0; // cd/m²
    
    // Color space information
    public ColorGamut NativeGamut { get; set; } = ColorGamut.sRGB;
    public WhitePoint NativeWhitePoint { get; set; } = new WhitePoint(6500); // D65
    public double[] RedPrimary { get; set; } = new double[] { 0.64, 0.33 }; // sRGB red
    public double[] GreenPrimary { get; set; } = new double[] { 0.30, 0.60 }; // sRGB green
    public double[] BluePrimary { get; set; } = new double[] { 0.15, 0.06 }; // sRGB blue
    
    // Calibration data
    public Dictionary<int, CalibrationPoint> CalibrationCurve { get; set; } = new();
    public DateTime LastCalibrationDate { get; set; } = DateTime.MinValue;
    public string? ICCProfilePath { get; set; }
    public byte[]? ICCProfileData { get; set; }
    
    // User preferences
    public int PreferredTemperatureDaylight { get; set; } = 6500;
    public int PreferredTemperatureNight { get; set; } = 3400;
    public double BrightnessScale { get; set; } = 1.0;
    public double ContrastScale { get; set; } = 1.0;
    public double GammaCorrection { get; set; } = 2.2;
    
    // Advanced settings
    public bool UsePerceptualSmoothing { get; set; } = true;
    public bool ApplyBradfordAdaptation { get; set; } = true;
    public bool UseHDRToneMapping { get; set; } = false;
    public TransitionEasingType PreferredTransitionType { get; set; } = TransitionEasingType.SigmoidSmooth;

    /// <summary>
    /// Gets the optimal color multipliers for a given temperature
    /// Accounts for monitor-specific characteristics and calibration
    /// </summary>
    public (double r, double g, double b) GetOptimalMultipliers(int temperatureKelvin)
    {
        // Base multipliers from blackbody radiation
        var (baseR, baseG, baseB) = CalculateBlackbodyMultipliers(temperatureKelvin);
        
        // Apply monitor-specific calibration
        if (CalibrationCurve.Count > 0)
        {
            baseR *= GetCalibrationFactor(baseR, "red");
            baseG *= GetCalibrationFactor(baseG, "green");
            baseB *= GetCalibrationFactor(baseB, "blue");
        }
        
        // Apply gamut mapping if necessary
        if (NativeGamut != ColorGamut.sRGB)
        {
            (baseR, baseG, baseB) = ApplyGamutMapping(baseR, baseG, baseB);
        }
        
        // Apply Bradford chromatic adaptation if enabled
        if (ApplyBradfordAdaptation)
        {
            (baseR, baseG, baseB) = ApplyBradfordTransform(baseR, baseG, baseB, temperatureKelvin);
        }
        
        return (baseR, baseG, baseB);
    }

    /// <summary>
    /// Calculates blackbody radiation color multipliers for temperature
    /// </summary>
    private (double r, double g, double b) CalculateBlackbodyMultipliers(int kelvin)
    {
        // Clamp temperature to reasonable range
        kelvin = Math.Max(1000, Math.Min(40000, kelvin));
        
        double r, g, b;
        
        // Red calculation
        if (kelvin >= 6600)
        {
            r = kelvin / 100.0;
            r = 329.698727446 * Math.Pow(r - 60, -0.1332047592);
        }
        else
        {
            r = 255;
        }
        
        // Green calculation
        if (kelvin >= 6600)
        {
            g = kelvin / 100.0;
            g = 288.1221695283 * Math.Pow(g - 60, -0.0755148492);
        }
        else
        {
            g = kelvin / 100.0;
            g = 99.4708025861 * Math.Log(g) - 161.1195681661;
        }
        
        // Blue calculation
        if (kelvin >= 6600)
        {
            b = 255;
        }
        else if (kelvin < 1900)
        {
            b = 0;
        }
        else
        {
            b = kelvin / 100.0;
            b = 138.5177312231 * Math.Log(b - 10) - 305.0447927307;
        }
        
        // Normalize to 0-1 range
        r = Math.Max(0, Math.Min(255, r)) / 255.0;
        g = Math.Max(0, Math.Min(255, g)) / 255.0;
        b = Math.Max(0, Math.Min(255, b)) / 255.0;
        
        return (r, g, b);
    }

    /// <summary>
    /// Gets calibration factor for a given channel and intensity
    /// </summary>
    private double GetCalibrationFactor(double intensity, string channel)
    {
        // Simple interpolation from calibration curve
        // In a real implementation, this would use the actual calibration data
        return 1.0; // Placeholder
    }

    /// <summary>
    /// Applies gamut mapping for non-sRGB monitors
    /// </summary>
    private (double r, double g, double b) ApplyGamutMapping(double r, double g, double b)
    {
        // Placeholder for gamut mapping
        // Would involve matrix transforms between color spaces
        return (r, g, b);
    }

    /// <summary>
    /// Applies Bradford chromatic adaptation transform
    /// </summary>
    private (double r, double g, double b) ApplyBradfordTransform(double r, double g, double b, int targetTemp)
    {
        // Bradford adaptation matrix for more accurate color temperature conversion
        // This is a simplified version - full implementation would use 3x3 matrices
        
        var sourceWhite = NativeWhitePoint.GetChromaticity();
        var targetWhite = GetChromaticityForTemperature(targetTemp);
        
        // Calculate adaptation scaling (simplified)
        var adaptationFactor = CalculateAdaptationFactor(sourceWhite, targetWhite);
        
        return (r * adaptationFactor.r, g * adaptationFactor.g, b * adaptationFactor.b);
    }

    /// <summary>
    /// Gets chromaticity coordinates for a given temperature
    /// </summary>
    private (double x, double y) GetChromaticityForTemperature(int kelvin)
    {
        // CIE 1931 chromaticity calculation for blackbody radiator
        if (kelvin < 4000)
        {
            double x = -0.2661239e9 / (kelvin * kelvin * kelvin) - 0.2343589e6 / (kelvin * kelvin) + 0.8776956e3 / kelvin + 0.179910;
            double y = -1.1063814 * x * x * x - 1.34811020 * x * x + 2.18555832 * x - 0.20219683;
            return (x, y);
        }
        else
        {
            double x = -3.0258469e9 / (kelvin * kelvin * kelvin) + 2.1070379e6 / (kelvin * kelvin) + 0.2226347e3 / kelvin + 0.240390;
            double y = 3.0817580 * x * x * x - 5.87338670 * x * x + 3.75112997 * x - 0.37001483;
            return (x, y);
        }
    }

    /// <summary>
    /// Calculates Bradford adaptation factor
    /// </summary>
    private (double r, double g, double b) CalculateAdaptationFactor((double x, double y) source, (double x, double y) target)
    {
        // Simplified Bradford adaptation calculation
        // Full implementation would use proper XYZ to LMS conversion matrices
        
        double factorR = Math.Sqrt(target.x / source.x);
        double factorG = Math.Sqrt(target.y / source.y);
        double factorB = Math.Sqrt((1 - target.x - target.y) / (1 - source.x - source.y));
        
        return (factorR, factorG, factorB);
    }

    /// <summary>
    /// Validates and auto-detects monitor capabilities
    /// </summary>
    public void DetectCapabilities()
    {
        // Placeholder for actual hardware detection
        // Would use DDC/CI, Windows Color System APIs, etc.
        
        // Set reasonable defaults based on common monitor types
        if (string.IsNullOrEmpty(ManufacturerName))
        {
            SupportsHardwareGamma = true;
            SupportsICCProfiles = false;
            SupportsDDCCI = false;
            NativeGamut = ColorGamut.sRGB;
            BitDepth = 8;
        }
    }
}

/// <summary>
/// Color gamut enumeration for different monitor types
/// </summary>
public enum ColorGamut
{
    sRGB,
    AdobeRGB,
    DCI_P3,
    Rec2020,
    ProPhotoRGB,
    Custom
}

/// <summary>
/// White point information for color temperature calculations
/// </summary>
public class WhitePoint
{
    public int TemperatureKelvin { get; set; }
    public double ChromaticityX { get; set; }
    public double ChromaticityY { get; set; }

    public WhitePoint(int temperatureKelvin)
    {
        TemperatureKelvin = temperatureKelvin;
        var (x, y) = CalculateChromaticity(temperatureKelvin);
        ChromaticityX = x;
        ChromaticityY = y;
    }

    public (double x, double y) GetChromaticity() => (ChromaticityX, ChromaticityY);

    private (double x, double y) CalculateChromaticity(int kelvin)
    {
        // Standard illuminant calculation for CIE 1931
        if (kelvin < 4000)
        {
            double x = -0.2661239e9 / (kelvin * kelvin * kelvin) - 0.2343589e6 / (kelvin * kelvin) + 0.8776956e3 / kelvin + 0.179910;
            double y = -1.1063814 * x * x * x - 1.34811020 * x * x + 2.18555832 * x - 0.20219683;
            return (x, y);
        }
        else
        {
            double x = -3.0258469e9 / (kelvin * kelvin * kelvin) + 2.1070379e6 / (kelvin * kelvin) + 0.2226347e3 / kelvin + 0.240390;
            double y = 3.0817580 * x * x * x - 5.87338670 * x * x + 3.75112997 * x - 0.37001483;
            return (x, y);
        }
    }
}

/// <summary>
/// Calibration point for monitor color correction
/// </summary>
public class CalibrationPoint
{
    public double InputLevel { get; set; }  // 0-1
    public double RedOutput { get; set; }   // 0-1
    public double GreenOutput { get; set; } // 0-1
    public double BlueOutput { get; set; }  // 0-1
    public double Luminance { get; set; }   // cd/m²
    public DateTime MeasurementTime { get; set; }
}
