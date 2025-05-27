using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ChronoGuard.Domain.Configuration
{
    /// <summary>
    /// Advanced configuration options for ChronoGuard color management
    /// </summary>
    public class AdvancedColorManagementConfig
    {
        /// <summary>
        /// Enable hardware gamma ramp manipulation (fastest, requires hardware support)
        /// </summary>
        public bool EnableHardwareGamma { get; set; } = true;

        /// <summary>
        /// Enable ICC profile fallback when hardware gamma is not available
        /// </summary>
        public bool EnableICCProfileFallback { get; set; } = true;

        /// <summary>
        /// Enable temporary ICC profile generation as last resort
        /// </summary>
        public bool EnableTemporaryProfileGeneration { get; set; } = true;

        /// <summary>
        /// Use Bradford chromatic adaptation transform for more accurate color conversion
        /// </summary>
        public bool UseBradfordAdaptation { get; set; } = true;

        /// <summary>
        /// Use perceptual color interpolation in CIE L*a*b* color space
        /// </summary>
        public bool UsePerceptualInterpolation { get; set; } = true;

        /// <summary>
        /// Monitor-specific calibration offsets (DeviceName -> CalibrationOffset)
        /// </summary>
        public Dictionary<string, MonitorCalibrationSettings> MonitorCalibrations { get; set; } = new();

        /// <summary>
        /// Gamma curve adjustment factor (1.0 = no adjustment, 0.8-1.5 typical range)
        /// </summary>
        [Range(0.5, 3.0)]
        public double GammaCurveAdjustment { get; set; } = 1.0;

        /// <summary>
        /// White point preservation strength (0.0 = none, 1.0 = maximum)
        /// </summary>
        [Range(0.0, 1.0)]
        public double WhitePointPreservation { get; set; } = 0.85;

        /// <summary>
        /// Color saturation adjustment during temperature changes (-1.0 to 1.0)
        /// </summary>
        [Range(-1.0, 1.0)]
        public double SaturationAdjustment { get; set; } = 0.0;

        /// <summary>
        /// Enable monitor-specific color profiles
        /// </summary>
        public bool EnablePerMonitorProfiles { get; set; } = true;

        /// <summary>
        /// Automatic color profile detection from Windows Color Management
        /// </summary>
        public bool AutoDetectColorProfiles { get; set; } = true;

        /// <summary>
        /// Maximum allowed color temperature deviation per update (Kelvin)
        /// </summary>
        [Range(10, 500)]
        public int MaxTemperatureDeviationPerUpdate { get; set; } = 100;

        /// <summary>
        /// Enable dithering to reduce color banding in gradient transitions
        /// </summary>
        public bool EnableDithering { get; set; } = true;

        /// <summary>
        /// Color depth preference for gamma ramp generation
        /// </summary>
        public ColorDepthPreference ColorDepthPreference { get; set; } = ColorDepthPreference.Auto;

        /// <summary>
        /// Validate configuration values
        /// </summary>
        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            if (GammaCurveAdjustment < 0.5 || GammaCurveAdjustment > 3.0)
                errors.Add("GammaCurveAdjustment must be between 0.5 and 3.0");

            if (WhitePointPreservation < 0.0 || WhitePointPreservation > 1.0)
                errors.Add("WhitePointPreservation must be between 0.0 and 1.0");

            if (SaturationAdjustment < -1.0 || SaturationAdjustment > 1.0)
                errors.Add("SaturationAdjustment must be between -1.0 and 1.0");

            if (MaxTemperatureDeviationPerUpdate < 10 || MaxTemperatureDeviationPerUpdate > 500)
                errors.Add("MaxTemperatureDeviationPerUpdate must be between 10 and 500");

            if (!EnableHardwareGamma && !EnableICCProfileFallback && !EnableTemporaryProfileGeneration)
                errors.Add("At least one color adjustment method must be enabled");

            return errors.Count == 0;
        }
    }

    /// <summary>
    /// Monitor-specific calibration settings
    /// </summary>
    public class MonitorCalibrationSettings
    {
        /// <summary>
        /// Red channel gamma adjustment
        /// </summary>
        [Range(0.5, 3.0)]
        public double RedGamma { get; set; } = 1.0;

        /// <summary>
        /// Green channel gamma adjustment
        /// </summary>
        [Range(0.5, 3.0)]
        public double GreenGamma { get; set; } = 1.0;

        /// <summary>
        /// Blue channel gamma adjustment
        /// </summary>
        [Range(0.5, 3.0)]
        public double BlueGamma { get; set; } = 1.0;

        /// <summary>
        /// Brightness offset (-100 to +100)
        /// </summary>
        [Range(-100, 100)]
        public int BrightnessOffset { get; set; } = 0;

        /// <summary>
        /// Contrast adjustment (0.5 to 2.0, 1.0 = no change)
        /// </summary>
        [Range(0.5, 2.0)]
        public double ContrastAdjustment { get; set; } = 1.0;

        /// <summary>
        /// Color temperature offset in Kelvin
        /// </summary>
        [Range(-1000, 1000)]
        public int TemperatureOffset { get; set; } = 0;

        /// <summary>
        /// Custom ICC profile path for this monitor
        /// </summary>
        public string? CustomICCProfilePath { get; set; }

        /// <summary>
        /// Last calibration date
        /// </summary>
        public DateTime? LastCalibrationDate { get; set; }

        /// <summary>
        /// Monitor-specific notes
        /// </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Color depth preference for gamma ramp generation
    /// </summary>
    public enum ColorDepthPreference
    {
        /// <summary>
        /// Automatically detect best color depth
        /// </summary>
        Auto,

        /// <summary>
        /// Use 8-bit per channel (24-bit total)
        /// </summary>
        Bit8,

        /// <summary>
        /// Use 10-bit per channel (30-bit total)
        /// </summary>
        Bit10,

        /// <summary>
        /// Use 12-bit per channel (36-bit total)
        /// </summary>
        Bit12,

        /// <summary>
        /// Use 16-bit per channel (48-bit total)
        /// </summary>
        Bit16
    }

    /// <summary>
    /// Transition behavior configuration
    /// </summary>
    public class TransitionBehaviorConfig
    {
        /// <summary>
        /// Minimum update interval during transitions (milliseconds)
        /// </summary>
        [Range(50, 5000)]
        public int MinimumUpdateIntervalMs { get; set; } = 100;

        /// <summary>
        /// Maximum update interval during stable periods (milliseconds)
        /// </summary>
        [Range(1000, 300000)]
        public int MaximumUpdateIntervalMs { get; set; } = 60000;

        /// <summary>
        /// Transition smoothing factor (0.0 = no smoothing, 1.0 = maximum smoothing)
        /// </summary>
        [Range(0.0, 1.0)]
        public double SmoothingFactor { get; set; } = 0.3;

        /// <summary>
        /// Enable predictive caching for smoother transitions
        /// </summary>
        public bool EnablePredictiveCache { get; set; } = true;

        /// <summary>
        /// Cache duration for predictive calculations (hours)
        /// </summary>
        [Range(1, 72)]
        public int CacheDurationHours { get; set; } = 24;

        /// <summary>
        /// Enable adaptive easing based on circadian rhythm
        /// </summary>
        public bool EnableAdaptiveEasing { get; set; } = true;

        /// <summary>
        /// Flicker reduction strength (0.0 = none, 1.0 = maximum)
        /// </summary>
        [Range(0.0, 1.0)]
        public double FlickerReduction { get; set; } = 0.5;

        /// <summary>
        /// Enable high-frequency updates during rapid transitions
        /// </summary>
        public bool EnableHighFrequencyUpdates { get; set; } = true;
    }

    /// <summary>
    /// Performance optimization configuration
    /// </summary>
    public class PerformanceConfig
    {
        /// <summary>
        /// Enable multi-threading for monitor operations
        /// </summary>
        public bool EnableMultiThreading { get; set; } = true;

        /// <summary>
        /// Maximum number of concurrent monitor operations
        /// </summary>
        [Range(1, 16)]
        public int MaxConcurrentOperations { get; set; } = 4;

        /// <summary>
        /// Enable memory optimization for large caches
        /// </summary>
        public bool EnableMemoryOptimization { get; set; } = true;

        /// <summary>
        /// Maximum memory usage for caching (MB)
        /// </summary>
        [Range(10, 1000)]
        public int MaxCacheMemoryMB { get; set; } = 100;

        /// <summary>
        /// Enable performance monitoring and logging
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = false;

        /// <summary>
        /// CPU usage threshold for reducing update frequency (%)
        /// </summary>
        [Range(50, 95)]
        public int CpuThrottleThreshold { get; set; } = 80;

        /// <summary>
        /// Enable power management optimizations
        /// </summary>
        public bool EnablePowerOptimizations { get; set; } = true;
    }
}
