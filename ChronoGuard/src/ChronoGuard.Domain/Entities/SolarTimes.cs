namespace ChronoGuard.Domain.Entities;

/// <summary>
/// Represents sunrise and sunset times for a given location and date
/// </summary>
public record SolarTimes
{
    public DateTime Sunrise { get; init; }
    public DateTime Sunset { get; init; }
    public DateTime Date { get; init; }
    public Location Location { get; init; }
    
    /// <summary>
    /// Solar noon time (when sun is highest)
    /// </summary>
    public DateTime SolarNoon => Date.Date.Add(TimeSpan.FromTicks((Sunrise.TimeOfDay.Ticks + Sunset.TimeOfDay.Ticks) / 2));
    
    /// <summary>
    /// Day length in hours
    /// </summary>
    public TimeSpan DayLength => Sunset - Sunrise;
    
    /// <summary>
    /// Night length in hours
    /// </summary>
    public TimeSpan NightLength => TimeSpan.FromHours(24) - DayLength;

    public SolarTimes(DateTime sunrise, DateTime sunset, DateTime date, Location location)
    {
        if (sunrise >= sunset)
            throw new ArgumentException("Sunrise must be before sunset");
        
        if (date.Date != sunrise.Date || date.Date != sunset.Date)
            throw new ArgumentException("All dates must be the same day");

        Sunrise = sunrise;
        Sunset = sunset;
        Date = date.Date;
        Location = location;
    }

    /// <summary>
    /// Determines if the current time is during daylight hours
    /// </summary>
    public bool IsDaylight(DateTime currentTime)
    {
        var timeOfDay = currentTime.TimeOfDay;
        return timeOfDay >= Sunrise.TimeOfDay && timeOfDay <= Sunset.TimeOfDay;
    }

    /// <summary>
    /// Gets the current phase of the day
    /// </summary>
    public DayPhase GetDayPhase(DateTime currentTime)
    {
        var timeOfDay = currentTime.TimeOfDay;
        var sunrise = Sunrise.TimeOfDay;
        var sunset = Sunset.TimeOfDay;

        if (timeOfDay < sunrise)
            return DayPhase.Night;
        
        if (timeOfDay >= sunrise && timeOfDay < sunrise.Add(TimeSpan.FromHours(1)))
            return DayPhase.Sunrise;
        
        if (timeOfDay >= sunrise.Add(TimeSpan.FromHours(1)) && timeOfDay < sunset.Subtract(TimeSpan.FromHours(1)))
            return DayPhase.Day;
        
        if (timeOfDay >= sunset.Subtract(TimeSpan.FromHours(1)) && timeOfDay <= sunset)
            return DayPhase.Sunset;
        
        return DayPhase.Night;
    }

    /// <summary>
    /// Calculates the progress through the day (0.0 = midnight, 0.5 = noon, 1.0 = next midnight)
    /// </summary>
    public double GetDayProgress(DateTime currentTime)
    {
        var totalMinutesInDay = 24 * 60;
        var currentMinutes = currentTime.TimeOfDay.TotalMinutes;
        return currentMinutes / totalMinutesInDay;
    }

    public override string ToString()
    {
        return $"Sunrise: {Sunrise:HH:mm}, Sunset: {Sunset:HH:mm} (Day: {DayLength:hh\\:mm})";
    }
}

/// <summary>
/// Represents the different phases of a day
/// </summary>
public enum DayPhase
{
    Night,
    Sunrise,
    Day,
    Sunset
}
