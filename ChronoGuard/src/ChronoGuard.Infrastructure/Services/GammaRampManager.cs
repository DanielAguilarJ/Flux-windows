using System;
using System.Runtime.InteropServices;
using ChronoGuard.Domain.Models;

namespace ChronoGuard.Infrastructure.Services
{
    public class GammaRampManager
    {
        // Estructura para la rampa gamma utilizada por WinAPI
        [StructLayout(LayoutKind.Sequential)]
        public struct RAMP
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Red;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Green;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Blue;
        }

        public GammaRamp CalculateGammaRampForTemperature(int kelvin)
        {
            // Calcular valores RGB para la temperatura dada
            var (r, g, b) = TemperatureToRGB(kelvin);
            
            // Crear arrays para la rampa gamma
            var ramp = new GammaRamp
            {
                Red = new ushort[256],
                Green = new ushort[256],
                Blue = new ushort[256]
            };
            
            // Rellenar la rampa de gamma
            for (int i = 0; i < 256; i++)
            {
                // Aplicar correcciones de gamma para cada canal
                double intensity = i / 255.0;
                
                // Ajustar según la temperatura de color
                ramp.Red[i] = (ushort)Math.Min(65535, Math.Max(0, Math.Round(ConvertToGamma(intensity * r) * 65535)));
                ramp.Green[i] = (ushort)Math.Min(65535, Math.Max(0, Math.Round(ConvertToGamma(intensity * g) * 65535)));
                ramp.Blue[i] = (ushort)Math.Min(65535, Math.Max(0, Math.Round(ConvertToGamma(intensity * b) * 65535)));
            }
            
            return ramp;
        }

        private (double r, double g, double b) TemperatureToRGB(int kelvin)
        {
            // Algoritmo de aproximación de temperatura Kelvin a RGB 
            // Basado en la aproximación de Tanner Helland
            // https://tannerhelland.com/2012/09/18/convert-temperature-rgb-algorithm-code.html
            
            double temperature = kelvin / 100.0;
            double r, g, b;

            // Cálculo de canal rojo
            if (temperature <= 66)
            {
                r = 1.0;
            }
            else
            {
                r = temperature - 60;
                r = 329.698727446 * Math.Pow(r, -0.1332047592);
                r = Math.Max(0, Math.Min(r / 255.0, 1.0));
            }

            // Cálculo de canal verde
            if (temperature <= 66)
            {
                g = temperature;
                g = 99.4708025861 * Math.Log(g) - 161.1195681661;
            }
            else
            {
                g = temperature - 60;
                g = 288.1221695283 * Math.Pow(g, -0.0755148492);
            }
            
            g = Math.Max(0, Math.Min(g / 255.0, 1.0));

            // Cálculo de canal azul
            if (temperature >= 66)
            {
                b = 1.0;
            }
            else if (temperature <= 19)
            {
                b = 0.0;
            }
            else
            {
                b = temperature - 10;
                b = 138.5177312231 * Math.Log(b) - 305.0447927307;
                b = Math.Max(0, Math.Min(b / 255.0, 1.0));
            }

            return (r, g, b);
        }

        private double ConvertToGamma(double linear, double gamma = 2.2)
        {
            // Aplicar corrección gamma
            return Math.Pow(linear, 1.0 / gamma);
        }

        public RAMP ConvertToWinAPI(GammaRamp ramp)
        {
            // Convertir nuestra estructura interna a la estructura WinAPI
            return new RAMP
            {
                Red = ramp.Red,
                Green = ramp.Green,
                Blue = ramp.Blue
            };
        }
    }
}
