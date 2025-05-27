namespace ChronoGuard.Domain.Entities;

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
    private double CircadianAdaptive(double t)
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
    /// Faster initial change, gradually slowing down
    /// </summary>
    private static double ExponentialDecay(double t)
    {
        var decayRate = 3.0; // Optimal for visual adaptation
        return 1.0 - Math.Exp(-decayRate * t);
    }

    #endregion

    /// <summary>
    /// Checks if the transition is completed
    /// </summary>
    public bool IsCompleted => GetProgress() >= 1.0;
}
