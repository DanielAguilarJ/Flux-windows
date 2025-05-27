using System;
using System.Collections.Generic;

namespace ChronoGuard.Domain.Entities
{
    /// <summary>
    /// Represents an ICC color profile
    /// </summary>
    public class ICCProfile
    {
        /// <summary>
        /// Source file path of the ICC profile
        /// </summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>
        /// Raw ICC profile data
        /// </summary>
        public byte[] RawData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Profile size in bytes
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// ICC profile class (e.g., "mntr" for monitor, "prtr" for printer)
        /// </summary>
        public string ProfileClass { get; set; } = string.Empty;

        /// <summary>
        /// Color space signature (e.g., "RGB ", "CMYK")
        /// </summary>
        public string ColorSpace { get; set; } = string.Empty;

        /// <summary>
        /// Preferred Color Management Module
        /// </summary>
        public string PreferredCMM { get; set; } = string.Empty;

        /// <summary>
        /// ICC profile version
        /// </summary>
        public Version Version { get; set; } = new Version(4, 3, 0, 0);

        /// <summary>
        /// Device class (e.g., "mntr", "prtr", "scnr")
        /// </summary>
        public string DeviceClass { get; set; } = string.Empty;

        /// <summary>
        /// Profile creation date
        /// </summary>
        public DateTime CreationDate { get; set; }

        /// <summary>
        /// White point of the profile
        /// </summary>
        public WhitePoint WhitePoint { get; set; } = new WhitePoint();

        /// <summary>
        /// Red tone reproduction curve
        /// </summary>
        public double[] RedTRC { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Green tone reproduction curve
        /// </summary>
        public double[] GreenTRC { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Blue tone reproduction curve
        /// </summary>
        public double[] BlueTRC { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Red colorant (XYZ values)
        /// </summary>
        public WhitePoint RedColorant { get; set; } = new WhitePoint();

        /// <summary>
        /// Green colorant (XYZ values)
        /// </summary>
        public WhitePoint GreenColorant { get; set; } = new WhitePoint();

        /// <summary>
        /// Blue colorant (XYZ values)
        /// </summary>
        public WhitePoint BlueColorant { get; set; } = new WhitePoint();

        /// <summary>
        /// Profile tags dictionary
        /// </summary>
        public Dictionary<string, ICCTag> Tags { get; set; } = new Dictionary<string, ICCTag>();

        /// <summary>
        /// Indicates if this is a temporary profile
        /// </summary>
        public bool IsTemporary { get; set; }

        /// <summary>
        /// Profile description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Copyright information
        /// </summary>
        public string Copyright { get; set; } = string.Empty;

        /// <summary>
        /// Manufacturer information
        /// </summary>
        public string Manufacturer { get; set; } = string.Empty;

        /// <summary>
        /// Model information
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Get effective gamma value for a color channel
        /// </summary>
        public double GetEffectiveGamma(ColorChannel channel)
        {
            var trc = channel switch
            {
                ColorChannel.Red => RedTRC,
                ColorChannel.Green => GreenTRC,
                ColorChannel.Blue => BlueTRC,
                _ => new double[] { 2.2 }
            };

            if (trc.Length == 1)
                return trc[0]; // Simple gamma

            if (trc.Length > 1)
            {
                // Calculate effective gamma from curve
                // This is a simplified calculation
                var sum = 0.0;
                for (int i = 0; i < trc.Length; i++)
                {
                    var input = i / (double)(trc.Length - 1);
                    if (input > 0)
                    {
                        sum += Math.Log(trc[i]) / Math.Log(input);
                    }
                }
                return sum / (trc.Length - 1);
            }

            return 2.2; // Default gamma
        }

        /// <summary>
        /// Calculate color temperature from white point
        /// </summary>
        public int CalculateColorTemperature()
        {
            if (WhitePoint.X == 0 && WhitePoint.Y == 0 && WhitePoint.Z == 0)
                return 6500; // Default

            // Convert XYZ to chromaticity coordinates
            var sum = WhitePoint.X + WhitePoint.Y + WhitePoint.Z;
            if (sum == 0) return 6500;

            var x = WhitePoint.X / sum;
            var y = WhitePoint.Y / sum;

            // McCamy's approximation for color temperature
            var n = (x - 0.3320) / (0.1858 - y);
            var cct = 449.0 * Math.Pow(n, 3) + 3525.0 * Math.Pow(n, 2) + 6823.3 * n + 5520.33;

            return Math.Max(1000, Math.Min(25000, (int)Math.Round(cct)));
        }

        /// <summary>
        /// Get color gamut coverage as a percentage of sRGB
        /// </summary>
        public double GetSRGBCoverage()
        {
            // This would calculate the actual color gamut coverage
            // For now, return an estimated value based on colorants
            
            if (RedColorant.X == 0 && GreenColorant.X == 0 && BlueColorant.X == 0)
                return 100.0; // Assume sRGB if no colorant data

            // Calculate approximate gamut area using triangle area formula
            // This is a simplified calculation
            var redX = RedColorant.X / (RedColorant.X + RedColorant.Y + RedColorant.Z);
            var redY = RedColorant.Y / (RedColorant.X + RedColorant.Y + RedColorant.Z);
            
            var greenX = GreenColorant.X / (GreenColorant.X + GreenColorant.Y + GreenColorant.Z);
            var greenY = GreenColorant.Y / (GreenColorant.X + GreenColorant.Y + GreenColorant.Z);
            
            var blueX = BlueColorant.X / (BlueColorant.X + BlueColorant.Y + BlueColorant.Z);
            var blueY = BlueColorant.Y / (BlueColorant.X + BlueColorant.Y + BlueColorant.Z);

            var profileArea = Math.Abs((redX * (greenY - blueY) + greenX * (blueY - redY) + blueX * (redY - greenY)) / 2.0);
            
            // sRGB triangle area (approximate)
            var srgbArea = 0.1347; // Approximate area of sRGB gamut in CIE xy
            
            return Math.Min(200.0, (profileArea / srgbArea) * 100.0);
        }

        /// <summary>
        /// Create a copy of this profile
        /// </summary>
        public ICCProfile Copy()
        {
            var copy = new ICCProfile
            {
                SourcePath = SourcePath,
                Size = Size,
                ProfileClass = ProfileClass,
                ColorSpace = ColorSpace,
                PreferredCMM = PreferredCMM,
                Version = new Version(Version.Major, Version.Minor, Version.Build, Version.Revision),
                DeviceClass = DeviceClass,
                CreationDate = CreationDate,
                WhitePoint = new WhitePoint { X = WhitePoint.X, Y = WhitePoint.Y, Z = WhitePoint.Z },
                IsTemporary = IsTemporary,
                Description = Description,
                Copyright = Copyright,
                Manufacturer = Manufacturer,
                Model = Model
            };

            // Deep copy arrays
            copy.RawData = new byte[RawData.Length];
            Array.Copy(RawData, copy.RawData, RawData.Length);

            copy.RedTRC = new double[RedTRC.Length];
            Array.Copy(RedTRC, copy.RedTRC, RedTRC.Length);

            copy.GreenTRC = new double[GreenTRC.Length];
            Array.Copy(GreenTRC, copy.GreenTRC, GreenTRC.Length);

            copy.BlueTRC = new double[BlueTRC.Length];
            Array.Copy(BlueTRC, copy.BlueTRC, BlueTRC.Length);

            copy.RedColorant = new WhitePoint { X = RedColorant.X, Y = RedColorant.Y, Z = RedColorant.Z };
            copy.GreenColorant = new WhitePoint { X = GreenColorant.X, Y = GreenColorant.Y, Z = GreenColorant.Z };
            copy.BlueColorant = new WhitePoint { X = BlueColorant.X, Y = BlueColorant.Y, Z = BlueColorant.Z };

            // Deep copy tags
            copy.Tags = new Dictionary<string, ICCTag>();
            foreach (var tag in Tags)
            {
                copy.Tags[tag.Key] = tag.Value.Copy();
            }

            return copy;
        }

        /// <summary>
        /// Validate profile integrity
        /// </summary>
        public bool IsValid()
        {
            if (RawData == null || RawData.Length < 128)
                return false;

            if (string.IsNullOrEmpty(ColorSpace) || string.IsNullOrEmpty(ProfileClass))
                return false;

            if (WhitePoint.X < 0 || WhitePoint.Y < 0 || WhitePoint.Z < 0)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Represents an ICC profile tag
    /// </summary>
    public class ICCTag
    {
        /// <summary>
        /// Tag signature (4-character identifier)
        /// </summary>
        public string Signature { get; set; } = string.Empty;

        /// <summary>
        /// Offset in the profile data
        /// </summary>
        public uint Offset { get; set; }

        /// <summary>
        /// Size of the tag data
        /// </summary>
        public uint Size { get; set; }

        /// <summary>
        /// Raw tag data
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Tag type signature
        /// </summary>
        public string Type
        {
            get
            {
                if (Data.Length >= 4)
                    return System.Text.Encoding.ASCII.GetString(Data, 0, 4);
                return string.Empty;
            }
        }

        /// <summary>
        /// Create a copy of this tag
        /// </summary>
        public ICCTag Copy()
        {
            var copy = new ICCTag
            {
                Signature = Signature,
                Offset = Offset,
                Size = Size
            };

            copy.Data = new byte[Data.Length];
            Array.Copy(Data, copy.Data, Data.Length);

            return copy;
        }
    }

    /// <summary>
    /// Represents a white point or colorant in XYZ color space
    /// </summary>
    public class WhitePoint
    {
        /// <summary>
        /// X coordinate
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Y coordinate
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Z coordinate
        /// </summary>
        public double Z { get; set; }

        /// <summary>
        /// Convert to chromaticity coordinates
        /// </summary>
        public (double x, double y) ToChromaticity()
        {
            var sum = X + Y + Z;
            if (sum == 0) return (0.33, 0.33); // Default

            return (X / sum, Y / sum);
        }

        /// <summary>
        /// Create from chromaticity coordinates
        /// </summary>
        public static WhitePoint FromChromaticity(double x, double y, double Y = 1.0)
        {
            var z = 1.0 - x - y;
            return new WhitePoint
            {
                X = (x / y) * Y,
                Y = Y,
                Z = (z / y) * Y
            };
        }

        /// <summary>
        /// Calculate distance to another white point
        /// </summary>
        public double DistanceTo(WhitePoint other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            var dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Standard illuminants
        /// </summary>
        public static class StandardIlluminants
        {
            /// <summary>
            /// D50 illuminant (5000K)
            /// </summary>
            public static WhitePoint D50 => new WhitePoint { X = 0.96422, Y = 1.00000, Z = 0.82521 };

            /// <summary>
            /// D65 illuminant (6500K)
            /// </summary>
            public static WhitePoint D65 => new WhitePoint { X = 0.95047, Y = 1.00000, Z = 1.08883 };

            /// <summary>
            /// A illuminant (2856K - incandescent)
            /// </summary>
            public static WhitePoint A => new WhitePoint { X = 1.09850, Y = 1.00000, Z = 0.35585 };

            /// <summary>
            /// F2 illuminant (4230K - cool white fluorescent)
            /// </summary>
            public static WhitePoint F2 => new WhitePoint { X = 0.99186, Y = 1.00000, Z = 0.67393 };
        }
    }

    /// <summary>
    /// Color channel enumeration
    /// </summary>
    public enum ColorChannel
    {
        Red,
        Green,
        Blue,
        All
    }
}
