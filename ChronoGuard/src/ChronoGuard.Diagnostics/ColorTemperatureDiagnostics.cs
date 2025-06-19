using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace ChronoGuard.Diagnostics
{
    /// <summary>
    /// Diagnostics tool to identify why color temperature application fails
    /// </summary>
    public class ColorTemperatureDiagnostics
    {
        private readonly ILogger<ColorTemperatureDiagnostics> _logger;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAMP
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Red;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Green;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Blue;

            public RAMP()
            {
                Red = new ushort[256];
                Green = new ushort[256];
                Blue = new ushort[256];
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("gdi32.dll")]
        private static extern bool GetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        public ColorTemperatureDiagnostics(ILogger<ColorTemperatureDiagnostics> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Runs comprehensive diagnostics for color temperature functionality
        /// </summary>
        public async Task<DiagnosticResults> RunDiagnosticsAsync()
        {
            var results = new DiagnosticResults();
            
            _logger.LogInformation("🔍 Starting ChronoGuard Color Temperature Diagnostics...");

            // Check 1: Windows Version and Compatibility
            await CheckWindowsCompatibilityAsync(results);

            // Check 2: Monitor and Graphics Driver Support
            await CheckMonitorSupportAsync(results);

            // Check 3: Permission Level
            CheckPermissions(results);

            // Check 4: Conflicting Software
            await CheckConflictingSoftwareAsync(results);

            // Check 5: Try Manual Gamma Ramp Test
            await TestGammaRampDirectlyAsync(results);

            // Generate recommendations
            GenerateRecommendations(results);

            _logger.LogInformation("✅ Diagnostics completed");
            return results;
        }

        private async Task CheckWindowsCompatibilityAsync(DiagnosticResults results)
        {
            _logger.LogInformation("Checking Windows compatibility...");
            
            var version = Environment.OSVersion.Version;
            results.WindowsVersion = $"{version.Major}.{version.Minor}.{version.Build}";
            
            // Windows 10/11 recommended for best compatibility
            if (version.Major >= 10)
            {
                results.WindowsCompatible = true;
                _logger.LogInformation("✅ Windows {Version} is compatible", results.WindowsVersion);
            }
            else
            {
                results.WindowsCompatible = false;
                _logger.LogWarning("⚠️ Windows {Version} may have limited gamma ramp support", results.WindowsVersion);
            }

            await Task.CompletedTask;
        }

        private async Task CheckMonitorSupportAsync(DiagnosticResults results)
        {
            _logger.LogInformation("Checking monitor and driver support...");

            var monitors = new List<MonitorDiagnostic>();
              EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var monitor = new MonitorDiagnostic
                {
                    Handle = hMonitor,
                    Bounds = $"{lprcMonitor.Left},{lprcMonitor.Top},{lprcMonitor.Right},{lprcMonitor.Bottom}"
                };

                // Get monitor info
                var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    monitor.IsPrimary = (monitorInfo.dwFlags & 1) != 0;
                }

                // Test gamma ramp support
                var dc = GetDC(IntPtr.Zero);
                if (dc != IntPtr.Zero)
                {
                    var originalRamp = new RAMP();
                    monitor.CanReadGammaRamp = GetDeviceGammaRamp(dc, ref originalRamp);
                    
                    if (monitor.CanReadGammaRamp)
                    {
                        // Try to set the same ramp back (should always work if reading worked)
                        monitor.CanWriteGammaRamp = SetDeviceGammaRamp(dc, ref originalRamp);
                    }

                    ReleaseDC(IntPtr.Zero, dc);
                }

                monitors.Add(monitor);
                return true;
            }, IntPtr.Zero);

            results.Monitors = monitors;
            results.HasGammaSupport = monitors.Any(m => m.CanWriteGammaRamp);

            foreach (var monitor in monitors)
            {
                var status = monitor.CanWriteGammaRamp ? "✅ Supported" : "❌ Not Supported";
                _logger.LogInformation("Monitor {IsPrimary}: {Status} (Read: {CanRead}, Write: {CanWrite})", 
                    monitor.IsPrimary ? "Primary" : "Secondary", 
                    status, 
                    monitor.CanReadGammaRamp, 
                    monitor.CanWriteGammaRamp);
            }

            await Task.CompletedTask;
        }

        private void CheckPermissions(DiagnosticResults results)
        {
            _logger.LogInformation("Checking application permissions...");

            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                results.IsRunningAsAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

                var status = results.IsRunningAsAdmin ? "✅ Administrator" : "⚠️ Standard User";
                _logger.LogInformation("Permission level: {Status}", status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permissions");
                results.IsRunningAsAdmin = false;
            }
        }

        private async Task CheckConflictingSoftwareAsync(DiagnosticResults results)
        {
            _logger.LogInformation("Checking for conflicting software...");

            var conflictingProcesses = new List<string>();
            var knownConflicts = new[]
            {
                "f.lux", "redshift", "lightbulb", "iris", "nightmode", 
                "bluelight", "gammacontrol", "displaycal", "argyllcms"
            };

            try
            {
                var processes = Process.GetProcesses();
                foreach (var process in processes)
                {
                    try
                    {
                        var processName = process.ProcessName.ToLowerInvariant();
                        if (knownConflicts.Any(conflict => processName.Contains(conflict)))
                        {
                            conflictingProcesses.Add($"{process.ProcessName} (PID: {process.Id})");
                        }
                    }
                    catch
                    {
                        // Skip processes we can't access
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking running processes");
            }

            results.ConflictingSoftware = conflictingProcesses;
            
            if (conflictingProcesses.Any())
            {
                _logger.LogWarning("⚠️ Found potentially conflicting software: {Software}", string.Join(", ", conflictingProcesses));
            }
            else
            {
                _logger.LogInformation("✅ No conflicting software detected");
            }

            await Task.CompletedTask;
        }

        private async Task TestGammaRampDirectlyAsync(DiagnosticResults results)
        {
            _logger.LogInformation("Testing gamma ramp manipulation directly...");

            try
            {
                var dc = GetDC(IntPtr.Zero);
                if (dc == IntPtr.Zero)
                {
                    results.DirectTestSuccess = false;
                    results.DirectTestError = "Failed to get device context";
                    _logger.LogError("❌ Failed to get device context");
                    return;
                }

                // Read original ramp
                var originalRamp = new RAMP();
                if (!GetDeviceGammaRamp(dc, ref originalRamp))
                {
                    results.DirectTestSuccess = false;
                    results.DirectTestError = "Failed to read original gamma ramp";
                    _logger.LogError("❌ Failed to read original gamma ramp");
                    ReleaseDC(IntPtr.Zero, dc);
                    return;
                }

                // Create a test ramp (slightly warmer)
                var testRamp = CreateTestGammaRamp(4000); // 4000K test temperature

                // Apply test ramp
                if (!SetDeviceGammaRamp(dc, ref testRamp))
                {
                    results.DirectTestSuccess = false;
                    results.DirectTestError = "Failed to set test gamma ramp";
                    _logger.LogError("❌ Failed to set test gamma ramp");
                    ReleaseDC(IntPtr.Zero, dc);
                    return;
                }

                _logger.LogInformation("✅ Test gamma ramp applied successfully");
                
                // Wait a moment so user can see the change
                await Task.Delay(2000);

                // Restore original ramp
                if (!SetDeviceGammaRamp(dc, ref originalRamp))
                {
                    _logger.LogWarning("⚠️ Failed to restore original gamma ramp");
                }
                else
                {
                    _logger.LogInformation("✅ Original gamma ramp restored");
                }

                ReleaseDC(IntPtr.Zero, dc);
                results.DirectTestSuccess = true;
            }
            catch (Exception ex)
            {
                results.DirectTestSuccess = false;
                results.DirectTestError = ex.Message;
                _logger.LogError(ex, "❌ Direct gamma ramp test failed");
            }
        }

        private RAMP CreateTestGammaRamp(int temperatureK)
        {
            var ramp = new RAMP();
            
            // Calculate RGB multipliers for the temperature
            var (r, g, b) = CalculateColorMultipliers(temperatureK);

            for (int i = 0; i < 256; i++)
            {
                var normalizedValue = i / 255.0;
                var gammaValue = Math.Pow(normalizedValue, 1.0 / 2.2); // Apply gamma correction

                ramp.Red[i] = (ushort)Math.Min(65535, gammaValue * r * 65535);
                ramp.Green[i] = (ushort)Math.Min(65535, gammaValue * g * 65535);
                ramp.Blue[i] = (ushort)Math.Min(65535, gammaValue * b * 65535);
            }

            return ramp;
        }

        private (double r, double g, double b) CalculateColorMultipliers(int kelvin)
        {
            // Tanner Helland's algorithm
            double temperature = kelvin / 100.0;
            double r, g, b;

            // Red calculation
            if (temperature <= 66)
            {
                r = 255;
            }
            else
            {
                r = temperature - 60;
                r = 329.698727446 * Math.Pow(r, -0.1332047592);
                r = Math.Max(0, Math.Min(255, r));
            }

            // Green calculation
            if (temperature <= 66)
            {
                g = temperature;
                g = 99.4708025861 * Math.Log(g) - 161.1195681661;
                g = Math.Max(0, Math.Min(255, g));
            }
            else
            {
                g = temperature - 60;
                g = 288.1221695283 * Math.Pow(g, -0.0755148492);
                g = Math.Max(0, Math.Min(255, g));
            }

            // Blue calculation
            if (temperature >= 66)
            {
                b = 255;
            }
            else if (temperature <= 19)
            {
                b = 0;
            }
            else
            {
                b = temperature - 10;
                b = 138.5177312231 * Math.Log(b) - 305.0447927307;
                b = Math.Max(0, Math.Min(255, b));
            }

            return (r / 255.0, g / 255.0, b / 255.0);
        }

        private void GenerateRecommendations(DiagnosticResults results)
        {
            var recommendations = new List<string>();

            if (!results.HasGammaSupport)
            {
                recommendations.Add("❌ CRITICAL: Your monitor/graphics driver does not support gamma ramp manipulation.");
                recommendations.Add("💡 Try updating your graphics drivers to the latest version.");
                recommendations.Add("💡 Check if your monitor has built-in color temperature controls.");
                recommendations.Add("💡 Consider using Windows 10/11 Night Light as an alternative.");
            }

            if (!results.IsRunningAsAdmin)
            {
                recommendations.Add("⚠️ Consider running ChronoGuard as Administrator for better hardware access.");
            }

            if (results.ConflictingSoftware.Any())
            {
                recommendations.Add($"⚠️ Close conflicting software: {string.Join(", ", results.ConflictingSoftware)}");
            }

            if (!results.WindowsCompatible)
            {
                recommendations.Add("⚠️ Consider upgrading to Windows 10 or 11 for better compatibility.");
            }

            if (results.HasGammaSupport && !results.DirectTestSuccess)
            {
                recommendations.Add("🔧 Try restarting the application and your computer.");
                recommendations.Add("🔧 Disable other color management software temporarily.");
            }

            if (!recommendations.Any())
            {
                recommendations.Add("✅ Your system appears to be fully compatible with ChronoGuard!");
            }

            results.Recommendations = recommendations;
        }
    }

    public class DiagnosticResults
    {
        public bool WindowsCompatible { get; set; }
        public string WindowsVersion { get; set; } = "";
        public bool HasGammaSupport { get; set; }
        public bool IsRunningAsAdmin { get; set; }
        public List<string> ConflictingSoftware { get; set; } = new();
        public bool DirectTestSuccess { get; set; }
        public string? DirectTestError { get; set; }
        public List<MonitorDiagnostic> Monitors { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class MonitorDiagnostic
    {
        public IntPtr Handle { get; set; }
        public string Bounds { get; set; } = "";
        public bool IsPrimary { get; set; }
        public bool CanReadGammaRamp { get; set; }
        public bool CanWriteGammaRamp { get; set; }
    }
}
