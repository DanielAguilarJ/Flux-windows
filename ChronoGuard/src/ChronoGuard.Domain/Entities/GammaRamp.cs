using System;

namespace ChronoGuard.Domain.Entities
{
    /// <summary>
    /// Represents a gamma ramp for color temperature adjustment with 256 values per color channel.
    /// This is the domain entity for gamma correction data used across different service implementations.
    /// </summary>
    public struct GammaRamp : IEquatable<GammaRamp>
    {
        /// <summary>
        /// Red channel gamma values (0-65535 range, 256 entries)
        /// </summary>
        public ushort[] Red { get; set; }

        /// <summary>
        /// Green channel gamma values (0-65535 range, 256 entries)
        /// </summary>
        public ushort[] Green { get; set; }

        /// <summary>
        /// Blue channel gamma values (0-65535 range, 256 entries)
        /// </summary>
        public ushort[] Blue { get; set; }

        /// <summary>
        /// Color temperature in Kelvin that this gamma ramp represents
        /// </summary>
        public int ColorTemperature { get; set; }

        /// <summary>
        /// Brightness adjustment factor (0.0 to 1.0)
        /// </summary>
        public double Brightness { get; set; }

        /// <summary>
        /// Contrast adjustment factor (0.0 to 2.0, where 1.0 is neutral)
        /// </summary>
        public double Contrast { get; set; }

        /// <summary>
        /// Timestamp when this gamma ramp was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Indicates if this gamma ramp was generated using hardware acceleration (SIMD)
        /// </summary>
        public bool IsHardwareAccelerated { get; set; }

        /// <summary>
        /// Creates a new GammaRamp with initialized arrays
        /// </summary>
        /// <param name="colorTemperature">Color temperature in Kelvin</param>
        /// <param name="brightness">Brightness factor (0.0-1.0)</param>
        /// <param name="contrast">Contrast factor (0.0-2.0)</param>
        /// <param name="isHardwareAccelerated">Whether SIMD acceleration was used</param>
        public GammaRamp(int colorTemperature, double brightness = 1.0, double contrast = 1.0, bool isHardwareAccelerated = false)
        {
            Red = new ushort[256];
            Green = new ushort[256];
            Blue = new ushort[256];
            ColorTemperature = colorTemperature;
            Brightness = brightness;
            Contrast = contrast;
            GeneratedAt = DateTime.UtcNow;
            IsHardwareAccelerated = isHardwareAccelerated;
        }

        /// <summary>
        /// Creates a linear gamma ramp (identity)
        /// </summary>
        /// <returns>A gamma ramp with linear values</returns>
        public static GammaRamp CreateLinear()
        {
            var ramp = new GammaRamp(6500, 1.0, 1.0, false);
            
            for (int i = 0; i < 256; i++)
            {
                ushort value = (ushort)(i * 257); // Scale 0-255 to 0-65535
                ramp.Red[i] = value;
                ramp.Green[i] = value;
                ramp.Blue[i] = value;
            }
            
            return ramp;
        }

        /// <summary>
        /// Validates that the gamma ramp has proper structure and values
        /// </summary>
        /// <returns>True if the gamma ramp is valid</returns>
        public bool IsValid()
        {
            return Red != null && Red.Length == 256 &&
                   Green != null && Green.Length == 256 &&
                   Blue != null && Blue.Length == 256 &&
                   ColorTemperature >= 1000 && ColorTemperature <= 40000 &&
                   Brightness >= 0.0 && Brightness <= 1.0 &&
                   Contrast >= 0.0 && Contrast <= 2.0;
        }

        /// <summary>
        /// Creates a copy of this gamma ramp
        /// </summary>
        /// <returns>A new GammaRamp instance with copied values</returns>
        public GammaRamp Clone()
        {
            var cloned = new GammaRamp(ColorTemperature, Brightness, Contrast, IsHardwareAccelerated);
            cloned.GeneratedAt = GeneratedAt;
            
            Array.Copy(Red, cloned.Red, 256);
            Array.Copy(Green, cloned.Green, 256);
            Array.Copy(Blue, cloned.Blue, 256);
            
            return cloned;
        }

        #region IEquatable<GammaRamp> Implementation

        public bool Equals(GammaRamp other)
        {
            if (ColorTemperature != other.ColorTemperature ||
                Math.Abs(Brightness - other.Brightness) > 0.001 ||
                Math.Abs(Contrast - other.Contrast) > 0.001)
            {
                return false;
            }

            if (Red == null || Green == null || Blue == null ||
                other.Red == null || other.Green == null || other.Blue == null)
            {
                return false;
            }

            for (int i = 0; i < 256; i++)
            {
                if (Red[i] != other.Red[i] || Green[i] != other.Green[i] || Blue[i] != other.Blue[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is GammaRamp other && Equals(other);
        }

        public override int GetHashCode()
        {
            // Simple hash combining the key properties
            return HashCode.Combine(ColorTemperature, Brightness, Contrast);
        }

        public static bool operator ==(GammaRamp left, GammaRamp right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GammaRamp left, GammaRamp right)
        {
            return !left.Equals(right);
        }

        #endregion

        public override string ToString()
        {
            return $"GammaRamp[{ColorTemperature}K, B:{Brightness:F2}, C:{Contrast:F2}, SIMD:{IsHardwareAccelerated}]";
        }
    }
}
