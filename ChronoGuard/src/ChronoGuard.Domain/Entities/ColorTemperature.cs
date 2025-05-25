namespace ChronoGuard.Domain.Entities;

/// <summary>
/// Represents a color temperature value in Kelvin
/// </summary>
public record ColorTemperature
{
    public const int MinKelvin = 1000;
    public const int MaxKelvin = 10000;
    public const int DefaultDayKelvin = 6500;
    public const int DefaultNightKelvin = 2700;

    public int Kelvin { get; init; }
    
    /// <summary>
    /// RGB values calculated from the color temperature
    /// </summary>
    public (byte R, byte G, byte B) RGB { get; init; }

    public ColorTemperature(int kelvin)
    {
        if (kelvin < MinKelvin || kelvin > MaxKelvin)
            throw new ArgumentOutOfRangeException(nameof(kelvin), 
                $"Color temperature must be between {MinKelvin}K and {MaxKelvin}K");

        Kelvin = kelvin;
        RGB = CalculateRGB(kelvin);
    }

    /// <summary>
    /// Calculates RGB values from color temperature using Tanner Helland's algorithm
    /// </summary>
    private static (byte R, byte G, byte B) CalculateRGB(int kelvin)
    {
        double temp = kelvin / 100.0;
        double red, green, blue;

        // Calculate Red
        if (temp <= 66)
        {
            red = 255;
        }
        else
        {
            red = temp - 60;
            red = 329.698727446 * Math.Pow(red, -0.1332047592);
            red = Math.Max(0, Math.Min(255, red));
        }

        // Calculate Green
        if (temp <= 66)
        {
            green = temp;
            green = 99.4708025861 * Math.Log(green) - 161.1195681661;
        }
        else
        {
            green = temp - 60;
            green = 288.1221695283 * Math.Pow(green, -0.0755148492);
        }
        green = Math.Max(0, Math.Min(255, green));

        // Calculate Blue
        if (temp >= 66)
        {
            blue = 255;
        }
        else if (temp <= 19)
        {
            blue = 0;
        }
        else
        {
            blue = temp - 10;
            blue = 138.5177312231 * Math.Log(blue) - 305.0447927307;
            blue = Math.Max(0, Math.Min(255, blue));
        }

        return ((byte)red, (byte)green, (byte)blue);
    }

    /// <summary>
    /// Gets a descriptive name for the color temperature
    /// </summary>
    public string GetDescription()
    {
        return Kelvin switch
        {
            <= 1900 => "Muy Cálido (Candlelight)",
            <= 2700 => "Cálido (Tungsten)",
            <= 3000 => "Cálido Suave",
            <= 4000 => "Neutro Cálido",
            <= 5000 => "Neutro",
            <= 5500 => "Neutro Frío",
            <= 6500 => "Luz de Día",
            <= 7500 => "Frío",
            <= 9000 => "Muy Frío",
            _ => "Extremadamente Frío"
        };
    }

    /// <summary>
    /// Interpolates between two color temperatures
    /// </summary>
    public static ColorTemperature Interpolate(ColorTemperature from, ColorTemperature to, double progress)
    {
        if (progress < 0 || progress > 1)
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 1");

        var interpolatedKelvin = (int)(from.Kelvin + (to.Kelvin - from.Kelvin) * progress);
        return new ColorTemperature(interpolatedKelvin);
    }

    public override string ToString() => $"{Kelvin}K ({GetDescription()})";

    // Predefined common temperatures
    public static ColorTemperature Candle => new(1900);
    public static ColorTemperature Tungsten => new(2700);
    public static ColorTemperature WarmWhite => new(3000);
    public static ColorTemperature Halogen => new(3200);
    public static ColorTemperature Fluorescent => new(4000);
    public static ColorTemperature Daylight => new(6500);
    public static ColorTemperature Overcast => new(7000);
    public static ColorTemperature ClearSky => new(10000);
}
