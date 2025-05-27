namespace ChronoGuard.Domain.Entities;

/// <summary>
/// Represents a color temperature transition
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

    public ColorTransition(ColorTemperature from, ColorTemperature to, TimeSpan duration)
    {
        FromTemperature = from;
        ToTemperature = to;
        Duration = duration;
        StartTime = DateTime.UtcNow;
        IsActive = true;
    }

    /// <summary>
    /// Gets the current progress of the transition (0.0 to 1.0)
    /// </summary>
    public double GetProgress()
    {
        if (!IsActive) return 1.0;

        var elapsed = DateTime.UtcNow - StartTime;
        if (elapsed >= Duration) return 1.0;
        if (elapsed.TotalMilliseconds <= 0) return 0.0;

        return elapsed.TotalMilliseconds / Duration.TotalMilliseconds;
    }

    /// <summary>
    /// Gets the current color temperature based on transition progress
    /// </summary>
    public ColorTemperature GetCurrentTemperature()
    {
        var progress = GetProgress();
        var kelvinDiff = ToTemperature.Kelvin - FromTemperature.Kelvin;
        var currentKelvin = FromTemperature.Kelvin + (int)(kelvinDiff * progress);
        
        return new ColorTemperature(currentKelvin);
    }

    /// <summary>
    /// Checks if the transition is completed
    /// </summary>
    public bool IsCompleted => GetProgress() >= 1.0;
}
