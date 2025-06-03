using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ChronoGuard.Domain.Models;
using Microsoft.Extensions.Logging;
using static ChronoGuard.Infrastructure.Services.GammaRampManager;

namespace ChronoGuard.Infrastructure.Services
{
    public class MonitorControlService
    {
        private readonly ILogger<MonitorControlService> _logger;

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        // Para enumerar monitores
        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

        private delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public MonitorControlService(ILogger<MonitorControlService> logger)
        {
            _logger = logger;
        }        public Task<bool> ApplyGammaRampToAllMonitorsAsync(GammaRamp ramp)
        {
            return Task.Run(() =>
            {
                // Obtener todos los monitores
                var monitors = GetAllMonitors();
                
                if (!monitors.Any())
                {
                    _logger.LogWarning("No monitors detected.");
                    return false;
                }

                var winApiRamp = new RAMP
                {
                    Red = ramp.Red,
                    Green = ramp.Green,
                    Blue = ramp.Blue
                };

                bool success = true;

                // Aplicar a cada monitor
                foreach (var monitor in monitors)
                {
                    try
                    {
                        IntPtr hdc = CreateDC(null!, monitor, null!, IntPtr.Zero);
                        
                        if (hdc == IntPtr.Zero)
                        {
                            _logger.LogWarning("Failed to create device context for monitor {Monitor}", monitor);
                            success = false;
                            continue;
                        }

                        if (!SetDeviceGammaRamp(hdc, ref winApiRamp))
                        {
                            _logger.LogWarning("Failed to set gamma ramp for monitor {Monitor}", monitor);
                            success = false;
                        }

                        DeleteDC(hdc);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error applying gamma ramp to monitor {Monitor}", monitor);
                        success = false;
                    }
                }

                return success;
            });
        }

        private List<string> GetAllMonitors()
        {
            var monitors = new List<string>();

            try
            {
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, 
                    (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
                    {
                        // Obtener nombre del monitor
                        var info = new MONITORINFOEX();
                        info.cbSize = Marshal.SizeOf(info);
                        GetMonitorInfo(hMonitor, ref info);
                        
                        string deviceName = new string(info.szDevice).TrimEnd('\0');
                        monitors.Add(deviceName);
                        
                        return true;
                    }, 
                    IntPtr.Zero);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enumerating monitors");
            }

            return monitors;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public char[] szDevice;
        }
    }
}
