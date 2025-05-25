namespace ChronoGuard.Domain.Entities;

/// <summary>
/// Represents the current state of the ChronoGuard application
/// </summary>
public class AppState
{
    public bool IsEnabled { get; set; } = true;
    public bool IsPaused { get; set; } = false;
    public string ActiveProfileId { get; set; } = "classic";
    public DateTime? PausedUntil { get; set; }
    public Location? CurrentLocation { get; set; }
    public SolarTimes? TodaySolarTimes { get; set; }
    public ColorTemperature? CurrentTemperature { get; set; }
    public TransitionState? ActiveTransition { get; set; }
    public List<string> ExcludedApplications { get; set; } = new();
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Checks if the application is effectively active (enabled and not paused)
    /// </summary>
    public bool IsActive => IsEnabled && !IsPaused && (!PausedUntil.HasValue || DateTime.UtcNow > PausedUntil);

    /// <summary>
    /// Checks if we should apply color filtering right now
    /// </summary>
    public bool ShouldApplyFiltering(string? currentApp = null)
    {
        if (!IsActive) return false;
        
        if (!string.IsNullOrEmpty(currentApp) && ExcludedApplications.Contains(currentApp))
            return false;

        return true;
    }

    /// <summary>
    /// Pauses the application for a specific duration
    /// </summary>
    public void PauseFor(TimeSpan duration)
    {
        PausedUntil = DateTime.UtcNow.Add(duration);
        IsPaused = true;
        LastUpdate = DateTime.UtcNow;
    }

    /// <summary>
    /// Pauses until the next sunrise
    /// </summary>
    public void PauseUntilSunrise()
    {
        if (TodaySolarTimes != null)
        {
            var tomorrow = DateTime.Today.AddDays(1);
            // For simplicity, assume sunrise is around the same time tomorrow
            var tomorrowSunrise = tomorrow.Add(TodaySolarTimes.Sunrise.TimeOfDay);
            PausedUntil = tomorrowSunrise;
            IsPaused = true;
            LastUpdate = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Resumes the application
    /// </summary>
    public void Resume()
    {
        IsPaused = false;
        PausedUntil = null;
        LastUpdate = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the current state
    /// </summary>
    public void UpdateState(ColorTemperature? temperature = null, TransitionState? transition = null)
    {
        if (temperature != null)
            CurrentTemperature = temperature;
        
        if (transition != null)
            ActiveTransition = transition;
        
        LastUpdate = DateTime.UtcNow;
    }
}

/// <summary>
/// Represents an active color temperature transition
/// </summary>
public class TransitionState
{
    public ColorTemperature FromTemperature { get; set; }
    public ColorTemperature ToTemperature { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string Reason { get; set; } = string.Empty;

    public TransitionState(ColorTemperature from, ColorTemperature to, TimeSpan duration, string reason = "")
    {
        FromTemperature = from;
        ToTemperature = to;
        StartTime = DateTime.UtcNow;
        Duration = duration;
        Reason = reason;
    }

    /// <summary>
    /// Gets the current progress of the transition (0.0 to 1.0)
    /// </summary>
    public double Progress
    {
        get
        {
            var elapsed = DateTime.UtcNow - StartTime;
            return Math.Max(0, Math.Min(1, elapsed.TotalMilliseconds / Duration.TotalMilliseconds));
        }
    }

    /// <summary>
    /// Checks if the transition is complete
    /// </summary>
    public bool IsComplete => Progress >= 1.0;

    /// <summary>
    /// Gets the current temperature during the transition
    /// </summary>
    public ColorTemperature CurrentTemperature
    {
        get
        {
            if (IsComplete) return ToTemperature;
            return ColorTemperature.Interpolate(FromTemperature, ToTemperature, Progress);
        }
    }

    public override string ToString()
    {
        var progressPercent = (int)(Progress * 100);
        return $"{FromTemperature.Kelvin}K → {ToTemperature.Kelvin}K ({progressPercent}%) - {Reason}";
    }
}
