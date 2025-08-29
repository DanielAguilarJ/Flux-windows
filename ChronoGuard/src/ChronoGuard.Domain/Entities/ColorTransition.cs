namespace ChronoGuard.Domain.Entities;

using System.Drawing;

/// <summary>
/// Easing algorithms for color temperature transitions
/// </summary>
public enum TransitionEasingType
{
    Linear,              // Constant speed transition
    EaseInQuad,         // Slow start, accelerating
    EaseOutQuad,        // Fast start, decelerating
    EaseInOutQuad,      // Smooth acceleration and deceleration
    EaseInCubic,        // More pronounced slow start
    EaseOutCubic,       // More pronounced fast start
    EaseInOutCubic,     // Smooth S-curve
    SigmoidSmooth,      // Perceptually optimal sigmoid curve
    CircadianAdaptive,  // Adaptive based on time of day
    ExponentialDecay    // Natural exponential transition
}

/// <summary>
/// Public easing type expected by tests
/// </summary>
public enum EasingType
{
    Linear,
    EaseInOut,
    Exponential,
    CircadianRhythm,
    Smooth
}

/// <summary>
/// RGB interpolation modes expected by tests
/// </summary>
public enum InterpolationMode
{
    LinearRGB,
    PerceptualLab
}

/// <summary>
/// Advanced color temperature transition engine with sophisticated interpolation algorithms
/// Supports multiple easing functions, perceptual color space transitions, and adaptive timing
/// Provides smooth, visually pleasing transitions that reduce eye strain
/// </summary>
public class ColorTransition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ColorTemperature FromTemperature { get; set; }
    public ColorTemperature ToTemperature { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime StartTime { get; set; }
    public bool IsActive { get; set; }
    public string? MonitorId { get; set; } // For multi-monitor support
    public TransitionEasingType EasingType { get; set; } = TransitionEasingType.SigmoidSmooth;
    public bool UsePerceptualInterpolation { get; set; } = true;
    public double AdaptiveSpeedFactor { get; set; } = 1.0;

    public ColorTransition(ColorTemperature from, ColorTemperature to, TimeSpan duration)
    {
        FromTemperature = from;
        ToTemperature = to;
        Duration = duration;
        StartTime = DateTime.UtcNow;
        IsActive = true;
    }

    /// <summary>
    /// Gets the current progress of the transition with adaptive timing (0.0 to 1.0)
    /// Applies speed factor and handles edge cases
    /// </summary>
    public double GetProgress()
    {
        if (!IsActive) return 1.0;

        var elapsed = DateTime.UtcNow - StartTime;
        var adjustedDuration = TimeSpan.FromMilliseconds(Duration.TotalMilliseconds / AdaptiveSpeedFactor);
        
        if (elapsed >= adjustedDuration) return 1.0;
        if (elapsed.TotalMilliseconds <= 0) return 0.0;

        return elapsed.TotalMilliseconds / adjustedDuration.TotalMilliseconds;
    }

    /// <summary>
    /// Gets the eased progress using the selected easing function
    /// Applies perceptually optimized interpolation curves
    /// </summary>
    public double GetEasedProgress()
    {
        var linearProgress = GetProgress();
        
        return EasingType switch
        {
            TransitionEasingType.Linear => linearProgress,
            TransitionEasingType.EaseInQuad => EaseInQuad(linearProgress),
            TransitionEasingType.EaseOutQuad => EaseOutQuad(linearProgress),
            TransitionEasingType.EaseInOutQuad => EaseInOutQuad(linearProgress),
            TransitionEasingType.EaseInCubic => EaseInCubic(linearProgress),
            TransitionEasingType.EaseOutCubic => EaseOutCubic(linearProgress),
            TransitionEasingType.EaseInOutCubic => EaseInOutCubic(linearProgress),
            TransitionEasingType.SigmoidSmooth => SigmoidSmooth(linearProgress),
            TransitionEasingType.CircadianAdaptive => CircadianAdaptive(linearProgress),
            TransitionEasingType.ExponentialDecay => ExponentialDecay(linearProgress),
            _ => linearProgress
        };
    }

    /// <summary>
    /// Gets the current color temperature with advanced interpolation
    /// Supports both linear and perceptual color space interpolation
    /// </summary>
    public ColorTemperature GetCurrentTemperature()
    {
        var progress = GetEasedProgress();
        
        if (UsePerceptualInterpolation)
        {
            return InterpolatePerceptual(FromTemperature, ToTemperature, progress);
        }
        else
        {
            return InterpolateLinear(FromTemperature, ToTemperature, progress);
        }
    }

    /// <summary>
    /// Linear interpolation in Kelvin space
    /// </summary>
    private static ColorTemperature InterpolateLinear(ColorTemperature from, ColorTemperature to, double progress)
    {
        var kelvinDiff = to.Kelvin - from.Kelvin;
        var currentKelvin = from.Kelvin + (int)(kelvinDiff * progress);
        return new ColorTemperature(currentKelvin);
    }

    /// <summary>
    /// Perceptual interpolation using CIE L*a*b* color space
    /// Provides more visually uniform color transitions
    /// </summary>
    private static ColorTemperature InterpolatePerceptual(ColorTemperature from, ColorTemperature to, double progress)
    {
        // Convert Kelvin to approximate CIE L*a*b* coordinates
        var (l1, a1, b1) = KelvinToLab(from.Kelvin);
        var (l2, a2, b2) = KelvinToLab(to.Kelvin);
        
        // Interpolate in L*a*b* space
        var currentL = l1 + (l2 - l1) * progress;
        var currentA = a1 + (a2 - a1) * progress;
        var currentB = b1 + (b2 - b1) * progress;
        
        // Convert back to approximate Kelvin
        var currentKelvin = LabToKelvin(currentL, currentA, currentB);
        
        return new ColorTemperature(currentKelvin);
    }

    /// <summary>
    /// Converts color temperature to approximate L*a*b* coordinates
    /// Uses simplified transformation for color temperature range
    /// </summary>
    private static (double l, double a, double b) KelvinToLab(int kelvin)
    {
        // Normalize Kelvin to 0-1 range (1000K-12000K)
        var normalized = Math.Max(0, Math.Min(1, (kelvin - 1000.0) / 11000.0));
        
        // Approximate L*a*b* mapping for color temperature
        var l = 50 + normalized * 50; // Lightness: warmer = darker perception
        var a = (1 - normalized) * 30 - 15; // Red-green: warm = red, cool = green
        var b = (1 - normalized) * 40 - 20; // Yellow-blue: warm = yellow, cool = blue
        
        return (l, a, b);
    }

    /// <summary>
    /// Converts L*a*b* coordinates back to approximate Kelvin
    /// </summary>
    private static int LabToKelvin(double l, double a, double b)
    {
        // Reverse the L*a*b* to Kelvin mapping
        var normalized = (l - 50) / 50.0;
        normalized = Math.Max(0, Math.Min(1, normalized));
        
        var kelvin = 1000 + normalized * 11000;
        return (int)Math.Round(kelvin);
    }

    #region Easing Functions

    private static double EaseInQuad(double t) => t * t;
    
    private static double EaseOutQuad(double t) => 1 - (1 - t) * (1 - t);
    
    private static double EaseInOutQuad(double t) => 
        t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
    
    private static double EaseInCubic(double t) => t * t * t;
    
    private static double EaseOutCubic(double t) => 1 - Math.Pow(1 - t, 3);
    
    private static double EaseInOutCubic(double t) =>
        t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;

    /// <summary>
    /// Sigmoid-based smooth transition - perceptually optimal for color temperature
    /// Provides gentle acceleration and deceleration that feels natural to human vision
    /// </summary>
    private static double SigmoidSmooth(double t)
    {
        // Enhanced sigmoid with optimal steepness for color perception
        var steepness = 6.0; // Optimized for color temperature transitions
        var sigmoid = 1.0 / (1.0 + Math.Exp(-steepness * (t - 0.5)));
        
        // Normalize to 0-1 range
        return (sigmoid - 0.5 / (1.0 + Math.Exp(steepness * 0.5))) / 
               (0.5 / (1.0 + Math.Exp(-steepness * 0.5)) - 0.5 / (1.0 + Math.Exp(steepness * 0.5)));
    }

    /// <summary>
    /// Circadian rhythm adaptive easing - adjusts transition speed based on time of day
    /// Faster transitions during active hours, slower during rest periods
    /// </summary>
    private static double CircadianAdaptive(double t)
    {
        var currentHour = DateTime.Now.Hour;
        var circadianFactor = GetCircadianFactor(currentHour);
        
        // Apply circadian modulation to sigmoid curve
        var sigmoid = SigmoidSmooth(t);
        
        // Adjust curve steepness based on circadian rhythm
        return Math.Pow(sigmoid, circadianFactor);
    }

    /// <summary>
    /// Gets circadian rhythm factor (0.5-2.0) based on hour of day
    /// Lower values = slower transitions (rest periods)
    /// Higher values = faster transitions (active periods)
    /// </summary>
    private static double GetCircadianFactor(int hour)
    {
        // Circadian rhythm model: slower at night (22-6), faster during day (8-20)
        if (hour >= 22 || hour <= 6)
        {
            return 0.5; // Slow transitions during sleep hours
        }
        else if (hour >= 8 && hour <= 20)
        {
            return 1.5; // Faster transitions during active hours
        }
        else
        {
            return 1.0; // Normal transitions during transition periods
        }
    }

    /// <summary>
    /// Exponential decay transition - mimics natural adaptation processes
    /// Faster initial change, gradually slowing down. Normalized so f(0)=0, f(1)=1
    /// </summary>
    private static double ExponentialDecay(double t)
    {
        var decayRate = 3.0; // Optimal for visual adaptation
        var denom = 1.0 - Math.Exp(-decayRate);
        if (denom <= 1e-9) return t; // fallback to linear if degenerate
        return (1.0 - Math.Exp(-decayRate * t)) / denom;
    }

    #endregion

    /// <summary>
    /// Checks if the transition is completed
    /// </summary>
    public bool IsCompleted => GetProgress() >= 1.0;

    // ====== Public static helpers expected by tests ======

    /// <summary>
    /// Applies an easing function to a progress value in the range [0,1].
    /// Throws ArgumentOutOfRangeException for invalid progress values.
    /// </summary>
    public static double ApplyEasing(double progress, global::ChronoGuard.Domain.Entities.EasingType easing)
    {
        if (progress < 0 || progress > 1)
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be within [0,1]");

        return easing switch
        {
            global::ChronoGuard.Domain.Entities.EasingType.Linear => progress,
            global::ChronoGuard.Domain.Entities.EasingType.EaseInOut => EaseInOutCubic(progress),
            global::ChronoGuard.Domain.Entities.EasingType.Exponential => ExponentialDecay(progress),
            global::ChronoGuard.Domain.Entities.EasingType.CircadianRhythm => CircadianAdaptive(progress),
            global::ChronoGuard.Domain.Entities.EasingType.Smooth => SigmoidSmooth(progress),
            _ => progress
        };
    }

    /// <summary>
    /// Interpolates color temperature using the specified easing.
    /// </summary>
    public static ColorTemperature InterpolateTemperature(
        ColorTemperature from, ColorTemperature to, double progress, global::ChronoGuard.Domain.Entities.EasingType easing)
    {
        var eased = ApplyEasing(progress, easing);
        // Interpolate in Kelvin space for predictable behavior in tests
        return ColorTemperature.Interpolate(from, to, eased);
    }

    /// <summary>
    /// Interpolates between two RGB colors.
    /// </summary>
    public static Color InterpolateRGB(Color c1, Color c2, double progress, InterpolationMode mode)
    {
        if (progress < 0 || progress > 1)
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be within [0,1]");

        return mode switch
        {
            InterpolationMode.LinearRGB =>
                Color.FromArgb(
                    ClampToByte(c1.R + (int)((c2.R - c1.R) * progress)),
                    ClampToByte(c1.G + (int)((c2.G - c1.G) * progress)),
                    ClampToByte(c1.B + (int)((c2.B - c1.B) * progress))
                ),
            InterpolationMode.PerceptualLab =>
                InterpolatePerceptualColor(c1, c2, progress),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    /// <summary>
    /// Calculates blue light reduction between two temperatures (0..1)
    /// </summary>
    public static double CalculateBlueReduction(ColorTemperature from, ColorTemperature to)
    {
        var fromBlue = from.RGB.B;
        var toBlue = to.RGB.B;
        if (fromBlue == 0) return 0.0;
        var reduction = (double)(fromBlue - toBlue) / Math.Max(1.0, fromBlue);
        return Math.Max(0.0, Math.Min(1.0, reduction));
    }

    /// <summary>
    /// Chooses an adaptive easing based on delta, blue reduction and time of day.
    /// </summary>
    public static global::ChronoGuard.Domain.Entities.EasingType CalculateAdaptiveEasing(
        ColorTemperature from,
        ColorTemperature to,
        DateTime currentTime,
        DateTime sunrise,
        DateTime sunset)
    {
        var deltaK = Math.Abs(to.Kelvin - from.Kelvin);
        var blueReduction = CalculateBlueReduction(from, to);

        // Small changes can be linear/ease-in-out
        if (deltaK <= 250)
            return global::ChronoGuard.Domain.Entities.EasingType.Linear;

        // Around evening/morning, favor gentler curves
        var oneHour = TimeSpan.FromHours(1);
        var nearSunset = currentTime >= sunset - oneHour && currentTime <= sunset + oneHour;
        var nearSunrise = currentTime >= sunrise - oneHour && currentTime <= sunrise + oneHour;

        if (nearSunset || nearSunrise)
        {
            if (blueReduction >= 0.3)
                return global::ChronoGuard.Domain.Entities.EasingType.Smooth; // very gentle
            return global::ChronoGuard.Domain.Entities.EasingType.EaseInOut;
        }

        // Daytime: allow faster adaptation for larger changes
        var dayPeriod = currentTime.TimeOfDay;
        if (dayPeriod > sunrise.TimeOfDay + TimeSpan.FromHours(2) &&
            dayPeriod < sunset.TimeOfDay - TimeSpan.FromHours(2))
        {
            return global::ChronoGuard.Domain.Entities.EasingType.Exponential;
        }

        return global::ChronoGuard.Domain.Entities.EasingType.EaseInOut;
    }

    // ====== Internal helpers ======

    private static int ClampToByte(int v) => v < 0 ? 0 : v > 255 ? 255 : v;

    private static Color InterpolatePerceptualColor(Color c1, Color c2, double t)
    {
        // Convert to Lab
        var (l1, a1, b1) = RgbToLab(c1);
        var (l2, a2, b2) = RgbToLab(c2);

        // Interpolate in Lab
        var l = l1 + (l2 - l1) * t;
        var a = a1 + (a2 - a1) * t;
        var b = b1 + (b2 - b1) * t;

        // Convert back
        return LabToRgb(l, a, b);
    }

    // Minimal sRGB <-> Lab conversion utilities (D65 reference)
    private static (double L, double A, double B) RgbToLab(Color c)
    {
        // sRGB to linear
        double rl = SrgbToLinear(c.R / 255.0);
        double gl = SrgbToLinear(c.G / 255.0);
        double bl = SrgbToLinear(c.B / 255.0);

        // Linear RGB to XYZ (sRGB D65)
        double x = rl * 0.4124564 + gl * 0.3575761 + bl * 0.1804375;
        double y = rl * 0.2126729 + gl * 0.7151522 + bl * 0.0721750;
        double z = rl * 0.0193339 + gl * 0.1191920 + bl * 0.9503041;

        // Normalize by D65 white
        const double Xn = 0.95047;
        const double Yn = 1.00000;
        const double Zn = 1.08883;

        double fx = Fxyz(x / Xn);
        double fy = Fxyz(y / Yn);
        double fz = Fxyz(z / Zn);

        double L = 116 * fy - 16;
        double A = 500 * (fx - fy);
        double B = 200 * (fy - fz);
        return (L, A, B);
    }

    private static Color LabToRgb(double L, double A, double B)
    {
        // Lab to XYZ
        double fy = (L + 16.0) / 116.0;
        double fx = A / 500.0 + fy;
        double fz = fy - B / 200.0;

        const double Xn = 0.95047;
        const double Yn = 1.00000;
        const double Zn = 1.08883;

        double xr = InvFxyz(fx);
        double yr = InvFxyz(fy);
        double zr = InvFxyz(fz);

        double x = xr * Xn;
        double y = yr * Yn;
        double z = zr * Zn;

        // XYZ to linear RGB
        double rl = x * 3.2404542 + y * -1.5371385 + z * -0.4985314;
        double gl = x * -0.9692660 + y * 1.8760108 + z * 0.0415560;
        double bl = x * 0.0556434 + y * -0.2040259 + z * 1.0572252;

        // Linear to sRGB
        byte r = (byte)ClampToByte((int)Math.Round(LinearToSrgb(rl) * 255.0));
        byte g = (byte)ClampToByte((int)Math.Round(LinearToSrgb(gl) * 255.0));
        byte b = (byte)ClampToByte((int)Math.Round(LinearToSrgb(bl) * 255.0));
        return Color.FromArgb(r, g, b);
    }

    private static double SrgbToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    private static double LinearToSrgb(double c) => c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;

    private static double Fxyz(double t) => t > Math.Pow(6.0 / 29.0, 3) ? Math.Pow(t, 1.0 / 3.0) : (1.0 / 3.0) * Math.Pow(29.0 / 6.0, 2) * t + 4.0 / 29.0;
    private static double InvFxyz(double t)
    {
        double t3 = t * t * t;
        double threshold = Math.Pow(6.0 / 29.0, 3);
        return t3 > threshold ? t3 : 3 * Math.Pow(6.0 / 29.0, 2) * (t - 4.0 / 29.0);
    }
}
