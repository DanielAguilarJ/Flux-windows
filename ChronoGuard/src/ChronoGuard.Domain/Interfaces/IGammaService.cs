using ChronoGuard.Domain.Entities;

namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Interface for gamma ramp calculation services
/// Provides abstraction for different gamma calculation implementations
/// </summary>
public interface IGammaService
{    /// <summary>
    /// Generates gamma ramp for the specified color temperature
    /// </summary>
    /// <param name="colorTemperature">Target color temperature in Kelvin</param>
    /// <param name="brightness">Brightness adjustment factor (0.0-1.0, default 1.0)</param>
    /// <param name="contrast">Contrast adjustment factor (0.0-2.0, default 1.0)</param>
    /// <returns>Generated gamma ramp structure</returns>
    GammaRamp GenerateOptimizedGammaRamp(int colorTemperature, double brightness = 1.0, double contrast = 1.0);

    /// <summary>
    /// Gets information about the service capabilities and performance characteristics
    /// </summary>
    /// <returns>Service capability information</returns>
    string GetCapabilities();

    /// <summary>
    /// Gets the service name for logging and identification
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Indicates if this service supports hardware acceleration
    /// </summary>
    bool IsHardwareAccelerated { get; }
}
