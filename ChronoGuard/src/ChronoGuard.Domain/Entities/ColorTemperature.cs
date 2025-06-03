using System.Text.Json.Serialization;

namespace ChronoGuard.Domain.Entities;

/// <summary>
/// Representa un valor de temperatura de color en Kelvin con conversión RGB automática.
/// Implementa el algoritmo de Tanner Helland para cálculos precisos de RGB.
/// </summary>
public record ColorTemperature
{    /// <summary>
    /// Constantes para los límites de temperatura de color soportados
    /// </summary>
    public const int MinKelvin = 1000;
    public const int MaxKelvin = 10000;
    public const int DefaultDayKelvin = 6500;
    public const int DefaultNightKelvin = 2700;

    /// <summary>
    /// Valor de temperatura de color en Kelvin
    /// </summary>
    [JsonPropertyName("kelvin")]
    public int Kelvin { get; init; }
    
    /// <summary>
    /// Valores RGB calculados automáticamente desde la temperatura de color
    /// usando el algoritmo de Tanner Helland
    /// </summary>
    [JsonPropertyName("rgb")]
    public (byte R, byte G, byte B) RGB { get; init; }    /// <summary>
    /// Constructor que crea una nueva instancia de ColorTemperature.
    /// Valida el rango y calcula automáticamente los valores RGB.
    /// </summary>
    /// <param name="kelvin">Temperatura de color en Kelvin (1000K - 10000K)</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se lanza cuando el valor está fuera del rango soportado
    /// </exception>
    public ColorTemperature(int kelvin)
    {
        if (kelvin < MinKelvin || kelvin > MaxKelvin)
            throw new ArgumentOutOfRangeException(nameof(kelvin), 
                $"La temperatura de color debe estar entre {MinKelvin}K y {MaxKelvin}K. Valor proporcionado: {kelvin}K");

        Kelvin = kelvin;
        RGB = CalculateRGB(kelvin);
    }    /// <summary>
    /// Calcula los valores RGB desde la temperatura de color usando el algoritmo de Tanner Helland.
    /// Este algoritmo proporciona una aproximación visualmente precisa de la temperatura de color
    /// basado en el trabajo de Mitchell Charity y optimizado por Tanner Helland.
    /// 
    /// El algoritmo utiliza funciones logarítmicas y exponenciales para aproximar la curva
    /// de distribución espectral de un cuerpo negro a diferentes temperaturas.
    /// 
    /// Referencia oficial: https://tannerhelland.com/2012/09/18/convert-temperature-rgb-algorithm-code.html
    /// </summary>
    /// <param name="kelvin">Temperatura de color en Kelvin</param>
    /// <returns>Tupla con valores RGB (0-255)</returns>
    private static (byte R, byte G, byte B) CalculateRGB(int kelvin)
    {        // Convertir Kelvin a temperatura para los cálculos (dividir por 100)
        // Este ajuste es necesario para que las constantes del algoritmo funcionen correctamente
        double temp = kelvin / 100.0;
        double red, green, blue;

        // Calcular componente Rojo
        // Para temperaturas ≤ 6600K, el rojo es máximo (255)
        if (temp <= 66)
        {
            red = 255;
        }
        else
        {
            // Para temperaturas > 6600K, usar función exponencial decreciente
            red = temp - 60;
            red = 329.698727446 * Math.Pow(red, -0.1332047592);
            red = Math.Max(0, Math.Min(255, red));
        }

        // Calcular componente Verde
        if (temp <= 66)
        {
            // Para temperaturas ≤ 6600K, usar función logarítmica creciente
            green = temp;
            green = 99.4708025861 * Math.Log(green) - 161.1195681661;
        }
        else
        {
            // Para temperaturas > 6600K, usar función exponencial decreciente
            green = temp - 60;
            green = 288.1221695283 * Math.Pow(green, -0.0755148492);
        }
        green = Math.Max(0, Math.Min(255, green));

        // Calcular componente Azul
        if (temp >= 66)
        {
            // Para temperaturas ≥ 6600K, el azul es máximo (255)
            blue = 255;
        }
        else if (temp <= 19)
        {
            // Para temperaturas muy bajas (≤ 1900K), no hay componente azul
            blue = 0;
        }
        else
        {
            // Para temperaturas medias, usar función logarítmica creciente
            blue = temp - 10;
            blue = 138.5177312231 * Math.Log(blue) - 305.0447927307;
            blue = Math.Max(0, Math.Min(255, blue));
        }

        return ((byte)red, (byte)green, (byte)blue);
    }    /// <summary>
    /// Obtiene una descripción legible en español para la temperatura de color.
    /// Proporciona nombres intuitivos basados en fuentes de luz comunes.
    /// </summary>
    /// <returns>Descripción descriptiva de la temperatura de color</returns>
    public string GetDescription()
    {        return Kelvin switch
        {
            <= 1900 => "Muy Cálido (Luz de Vela - Ambiente Íntimo)",
            <= 2700 => "Cálido (Bombilla Incandescente - Hogar Acogedor)",
            <= 3000 => "Cálido Suave (Bombilla Halógena - Restaurante)",
            <= 4000 => "Neutro Cálido (Fluorescente Cálido - Oficina Cómoda)",
            <= 5000 => "Neutro (Flash de Cámara - Lectura Óptima)",
            <= 5500 => "Neutro Frío (Luz Solar Directa - Exterior)",
            <= 6500 => "Luz de Día (Cielo Despejado - Productividad)",
            <= 7500 => "Frío (Cielo Nublado - Concentración)",
            <= 9000 => "Muy Frío (Pantalla LED - Alerta Máxima)",
            _ => "Extremadamente Frío (Cielo Azul - Técnico/Médico)"
        };
    }    /// <summary>
    /// Interpola entre dos temperaturas de color de forma lineal.
    /// Útil para transiciones suaves entre diferentes configuraciones.
    /// </summary>
    /// <param name="from">Temperatura de color inicial</param>
    /// <param name="to">Temperatura de color final</param>
    /// <param name="progress">Progreso de la interpolación (0.0 a 1.0)</param>
    /// <returns>Nueva ColorTemperature interpolada</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se lanza cuando progress está fuera del rango 0-1
    /// </exception>
    public static ColorTemperature Interpolate(ColorTemperature from, ColorTemperature to, double progress)
    {
        if (progress < 0 || progress > 1)
            throw new ArgumentOutOfRangeException(nameof(progress), 
                $"El progreso debe estar entre 0 y 1. Valor proporcionado: {progress}");

        var interpolatedKelvin = (int)(from.Kelvin + (to.Kelvin - from.Kelvin) * progress);
        return new ColorTemperature(interpolatedKelvin);
    }

    /// <summary>
    /// Representación en cadena de la temperatura de color
    /// </summary>
    /// <returns>Cadena con formato "TemperaturaK (Descripción)"</returns>
    public override string ToString() => $"{Kelvin}K ({GetDescription()})";

    // Temperaturas predefinidas comunes para facilidad de uso
    /// <summary>Luz de vela (~1900K)</summary>
    public static ColorTemperature Candle => new(1900);
    
    /// <summary>Bombilla incandescente tradicional (~2700K)</summary>
    public static ColorTemperature Tungsten => new(2700);
    
    /// <summary>Blanco cálido (~3000K)</summary>
    public static ColorTemperature WarmWhite => new(3000);
    
    /// <summary>Bombilla halógena (~3200K)</summary>
    public static ColorTemperature Halogen => new(3200);
    
    /// <summary>Fluorescente (~4000K)</summary>
    public static ColorTemperature Fluorescent => new(4000);
    
    /// <summary>Luz de día estándar (~6500K)</summary>
    public static ColorTemperature Daylight => new(6500);
    
    /// <summary>Cielo nublado (~7000K)</summary>
    public static ColorTemperature Overcast => new(7000);
      /// <summary>Cielo azul despejado (~10000K)</summary>
    public static ColorTemperature ClearSky => new(10000);

    // Propiedades de utilidad para clasificación de temperaturas
    
    /// <summary>
    /// Determina si esta temperatura de color se considera "cálida" (< 4000K)
    /// </summary>
    public bool IsWarm => Kelvin < 4000;

    /// <summary>
    /// Determina si esta temperatura de color se considera "fría" (> 6000K)
    /// </summary>
    public bool IsCool => Kelvin > 6000;

    /// <summary>
    /// Determina si esta temperatura de color se considera "neutra" (4000K - 6000K)
    /// </summary>
    public bool IsNeutral => Kelvin >= 4000 && Kelvin <= 6000;

    // Métodos de utilidad

    /// <summary>
    /// Calcula la diferencia de temperatura con otra ColorTemperature
    /// </summary>
    /// <param name="other">Otra temperatura de color para comparar</param>
    /// <returns>Diferencia absoluta en Kelvin</returns>
    public int GetTemperatureDifference(ColorTemperature other) => Math.Abs(Kelvin - other.Kelvin);

    /// <summary>
    /// Crea una nueva ColorTemperature con un ajuste específico en Kelvin
    /// </summary>
    /// <param name="adjustment">Ajuste en Kelvin (puede ser positivo o negativo)</param>
    /// <returns>Nueva ColorTemperature ajustada, limitada al rango válido</returns>
    public ColorTemperature Adjust(int adjustment)
    {
        var newKelvin = Math.Max(MinKelvin, Math.Min(MaxKelvin, Kelvin + adjustment));
        return new ColorTemperature(newKelvin);
    }

    /// <summary>
    /// Valida si un valor en Kelvin está en el rango soportado
    /// </summary>
    /// <param name="kelvin">Valor a validar</param>
    /// <returns>True si está en el rango válido</returns>
    public static bool IsValidKelvin(int kelvin) => kelvin >= MinKelvin && kelvin <= MaxKelvin;
}
