namespace ChronoGuard.Domain.Entities;

/// <summary>
/// Represents a color profile with temperature settings for different times of day
/// </summary>
public class ColorProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    // Temperature settings
    public int DayTemperature { get; set; } = ColorTemperature.DefaultDayKelvin;
    public int NightTemperature { get; set; } = ColorTemperature.DefaultNightKelvin;
    public int TransitionDurationMinutes { get; set; } = 20;

    // Advanced settings
    public bool EnableSunsetTransition { get; set; } = true;
    public bool EnableSunriseTransition { get; set; } = true;
    public int SunsetOffsetMinutes { get; set; } = 0; // Start transition X minutes before sunset
    public int SunriseOffsetMinutes { get; set; } = 0; // Start transition X minutes before sunrise

    // Weekly schedule (optional)
    public Dictionary<DayOfWeek, ProfileDaySettings>? WeeklySchedule { get; set; }

    public ColorProfile() { }

    public ColorProfile(string name, string description, int dayTemp, int nightTemp, int transitionMinutes = 20)
    {
        Name = name;
        Description = description;
        DayTemperature = dayTemp;
        NightTemperature = nightTemp;
        TransitionDurationMinutes = transitionMinutes;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the appropriate color temperature for the current time and solar conditions
    /// </summary>
    public ColorTemperature GetColorTemperatureForTime(DateTime currentTime, SolarTimes solarTimes)
    {
        var daySettings = GetDaySettings(currentTime.DayOfWeek);
        var dayTemp = daySettings?.DayTemperature ?? DayTemperature;
        var nightTemp = daySettings?.NightTemperature ?? NightTemperature;
        var transitionMinutes = daySettings?.TransitionDurationMinutes ?? TransitionDurationMinutes;

        var phase = solarTimes.GetDayPhase(currentTime);

        return phase switch
        {
            DayPhase.Day => new ColorTemperature(dayTemp),
            DayPhase.Night => new ColorTemperature(nightTemp),
            DayPhase.Sunrise when EnableSunriseTransition => CalculateTransitionTemperature(
                currentTime, solarTimes.Sunrise.AddMinutes(-SunriseOffsetMinutes), 
                transitionMinutes, nightTemp, dayTemp),
            DayPhase.Sunset when EnableSunsetTransition => CalculateTransitionTemperature(
                currentTime, solarTimes.Sunset.AddMinutes(-SunsetOffsetMinutes), 
                transitionMinutes, dayTemp, nightTemp),
            _ => new ColorTemperature(currentTime.Hour < 12 ? dayTemp : nightTemp)
        };
    }

    private ColorTemperature CalculateTransitionTemperature(DateTime currentTime, DateTime transitionStart, 
        int durationMinutes, int fromTemp, int toTemp)
    {
        var elapsed = currentTime - transitionStart;
        var progress = Math.Max(0, Math.Min(1, elapsed.TotalMinutes / durationMinutes));
        
        // Use sigmoid curve for more natural transition
        var smoothProgress = SigmoidTransition(progress);
        var temperature = (int)(fromTemp + (toTemp - fromTemp) * smoothProgress);
        
        return new ColorTemperature(temperature);
    }

    /// <summary>
    /// Applies sigmoid function for smoother transitions
    /// </summary>
    private static double SigmoidTransition(double x)
    {
        // Sigmoid function: 1 / (1 + e^(-6(x-0.5)))
        // Maps 0-1 input to smooth S-curve
        return 1.0 / (1.0 + Math.Exp(-6 * (x - 0.5)));
    }

    private ProfileDaySettings? GetDaySettings(DayOfWeek dayOfWeek)
    {
        return WeeklySchedule?.GetValueOrDefault(dayOfWeek);
    }

    /// <summary>
    /// Creates a copy of this profile
    /// </summary>
    public ColorProfile Clone()
    {
        return new ColorProfile
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"{Name} (Copy)",
            Description = Description,
            IsBuiltIn = false,
            DayTemperature = DayTemperature,
            NightTemperature = NightTemperature,
            TransitionDurationMinutes = TransitionDurationMinutes,
            EnableSunsetTransition = EnableSunsetTransition,
            EnableSunriseTransition = EnableSunriseTransition,
            SunsetOffsetMinutes = SunsetOffsetMinutes,
            SunriseOffsetMinutes = SunriseOffsetMinutes,
            WeeklySchedule = WeeklySchedule?.ToDictionary(
                kvp => kvp.Key,
                kvp => new ProfileDaySettings
                {
                    DayTemperature = kvp.Value.DayTemperature,
                    NightTemperature = kvp.Value.NightTemperature,
                    TransitionDurationMinutes = kvp.Value.TransitionDurationMinutes
                })
        };
    }

    public override string ToString() => $"{Name} ({DayTemperature}K → {NightTemperature}K)";

    // Built-in profiles factory methods
    public static ColorProfile CreateClassic() => new()
    {
        Id = "classic",
        Name = "Clásico",
        Description = "Configuración estándar similar a f.lux",
        IsBuiltIn = true,
        DayTemperature = 6500,
        NightTemperature = 2700,
        TransitionDurationMinutes = 20
    };

    public static ColorProfile CreateWorkNight() => new()
    {
        Id = "work-night",
        Name = "Trabajo Nocturno",
        Description = "Para trabajo intensivo durante la noche",
        IsBuiltIn = true,
        DayTemperature = 5000,
        NightTemperature = 1900,
        TransitionDurationMinutes = 60,
        SunsetOffsetMinutes = 120 // Start transition 2 hours before sunset
    };

    public static ColorProfile CreateMultimedia() => new()
    {
        Id = "multimedia",
        Name = "Multimedia",
        Description = "Preserva mejor los colores para contenido visual",
        IsBuiltIn = true,
        DayTemperature = 6500,
        NightTemperature = 3400,
        TransitionDurationMinutes = 30
    };
}

/// <summary>
/// Settings for a specific day of the week
/// </summary>
public class ProfileDaySettings
{
    public int? DayTemperature { get; set; }
    public int? NightTemperature { get; set; }
    public int? TransitionDurationMinutes { get; set; }
}
