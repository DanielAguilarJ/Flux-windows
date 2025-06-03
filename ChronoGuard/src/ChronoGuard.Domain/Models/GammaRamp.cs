namespace ChronoGuard.Domain.Models
{
    /// <summary>
    /// Represents a gamma ramp with RGB channels for color temperature adjustment
    /// </summary>
    public class GammaRamp
    {
        public ushort[] Red { get; set; } = new ushort[256];
        public ushort[] Green { get; set; } = new ushort[256];
        public ushort[] Blue { get; set; } = new ushort[256];

        public GammaRamp()
        {
            // Initialize with linear ramp
            for (int i = 0; i < 256; i++)
            {
                Red[i] = Green[i] = Blue[i] = (ushort)(i * 257); // Scale to 16-bit
            }
        }
    }
}
