using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using Timer = System.Threading.Timer;

namespace ChronoGuard.Infrastructure.Services;

/// <summary>
/// Service for applying color temperature changes to Windows displays
/// Uses SetDeviceGammaRamp for immediate control
/// </summary>
public class WindowsColorTemperatureService : IColorTemperatureService
{
    private readonly ILogger<WindowsColorTemperatureService> _logger;
    private ColorTemperature? _currentTemperature;
    private readonly Dictionary<string, IntPtr> _monitorHandles = new();
    private readonly Dictionary<string, RAMP> _originalGammaRamps = new();
    private TransitionState? _activeTransition;
    private Timer? _transitionTimer;

    public event EventHandler<ColorTemperature>? TemperatureChanged;
    public event EventHandler<TransitionState>? TransitionCompleted;

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

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
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
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    public WindowsColorTemperatureService(ILogger<WindowsColorTemperatureService> logger)
    {
        _logger = logger;
        InitializeMonitors();
    }

    private void InitializeMonitors()
    {
        try
        {
            _monitorHandles.Clear();
            _originalGammaRamps.Clear();

            // Get primary monitor for now (multi-monitor support can be added later)
            var primaryDC = GetDC(IntPtr.Zero);
            if (primaryDC != IntPtr.Zero)
            {
                var originalRamp = new RAMP();
                if (GetDeviceGammaRamp(primaryDC, ref originalRamp))
                {
                    _monitorHandles["primary"] = primaryDC;
                    _originalGammaRamps["primary"] = originalRamp;
                    _logger.LogInformation("Initialized primary monitor");
                }
                else
                {
                    ReleaseDC(IntPtr.Zero, primaryDC);
                    _logger.LogWarning("Failed to get original gamma ramp for primary monitor");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize monitors");
        }
    }

    public Task<bool> ApplyTemperatureAsync(ColorTemperature temperature)
    {
        try
        {
            var ramp = CreateGammaRamp(temperature);
            var success = true;

            foreach (var (monitorId, handle) in _monitorHandles)
            {
                if (!SetDeviceGammaRamp(handle, ref ramp))
                {
                    _logger.LogWarning("Failed to set gamma ramp for monitor {MonitorId}", monitorId);
                    success = false;
                }
            }

            if (success)
            {
                _currentTemperature = temperature;
                TemperatureChanged?.Invoke(this, temperature);
                _logger.LogDebug("Applied color temperature: {Temperature}", temperature);
            }

            return Task.FromResult(success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying color temperature: {Temperature}", temperature);
            return Task.FromResult(false);
        }
    }

    public Task<bool> ApplyTemperatureToMonitorAsync(string monitorId, ColorTemperature temperature)
    {
        try
        {
            if (!_monitorHandles.TryGetValue(monitorId, out var handle))
            {
                _logger.LogWarning("Monitor {MonitorId} not found", monitorId);
                return Task.FromResult(false);
            }

            var ramp = CreateGammaRamp(temperature);
            var success = SetDeviceGammaRamp(handle, ref ramp);

            if (success)
            {
                _logger.LogDebug("Applied color temperature {Temperature} to monitor {MonitorId}", temperature, monitorId);
            }

            return Task.FromResult(success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying temperature to monitor {MonitorId}: {Temperature}", monitorId, temperature);
            return Task.FromResult(false);
        }
    }

    public async Task<TransitionState> CreateTransitionAsync(ColorTemperature from, ColorTemperature to, TimeSpan duration, string reason = "")
    {
        try
        {
            // Stop any existing transition
            await StopTransitionAsync();

            var transition = new TransitionState(from, to, duration, reason);
            _activeTransition = transition;

            // Start transition timer
            _transitionTimer = new Timer(async _ => await UpdateTransitionAsync(), null,
                TimeSpan.Zero, TimeSpan.FromMilliseconds(200)); // Update every 200ms for smooth transitions

            _logger.LogInformation("Started transition: {Transition}", transition);
            return transition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transition from {From} to {To}", from, to);
            throw;
        }
    }

    private async Task UpdateTransitionAsync()
    {
        try
        {
            if (_activeTransition == null) return;

            var currentTemp = _activeTransition.CurrentTemperature;
            await ApplyTemperatureAsync(currentTemp);

            if (_activeTransition.IsComplete)
            {
                await StopTransitionAsync();
                TransitionCompleted?.Invoke(this, _activeTransition);
                _logger.LogInformation("Transition completed: {Transition}", _activeTransition);
                _activeTransition = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during transition update");
        }
    }

    public Task StopTransitionAsync()
    {
        _transitionTimer?.Dispose();
        _transitionTimer = null;
        return Task.CompletedTask;
    }

    public async Task RestoreOriginalSettingsAsync()
    {
        try
        {
            await StopTransitionAsync();

            foreach (var (monitorId, handle) in _monitorHandles)
            {
                if (_originalGammaRamps.TryGetValue(monitorId, out var originalRamp))
                {
                    if (SetDeviceGammaRamp(handle, ref originalRamp))
                    {
                        _logger.LogDebug("Restored original gamma for monitor {MonitorId}", monitorId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to restore original gamma for monitor {MonitorId}", monitorId);
                    }
                }
            }

            _currentTemperature = null;
            _logger.LogInformation("Restored original display settings");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring original settings");
        }
    }

    public ColorTemperature? GetCurrentTemperature()
    {
        return _currentTemperature;
    }

    public Task<IEnumerable<MonitorInfo>> GetMonitorsAsync()
    {
        var monitors = new List<MonitorInfo>();

        try
        {
            // For now, just return primary monitor info
            // Full multi-monitor support would require more complex enumeration
            monitors.Add(new MonitorInfo
            {
                Id = "primary",
                Name = "Primary Monitor",
                IsPrimary = true,
                Width = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Width ?? 1920,
                Height = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Height ?? 1080,
                DevicePath = "primary"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting monitor information");
        }

        return Task.FromResult<IEnumerable<MonitorInfo>>(monitors);
    }

    private RAMP CreateGammaRamp(ColorTemperature temperature)
    {
        var ramp = new RAMP();
        var (r, g, b) = temperature.RGB;

        // Normalize RGB values to 0-1 range
        var rFactor = r / 255.0;
        var gFactor = g / 255.0;
        var bFactor = b / 255.0;

        for (int i = 0; i < 256; i++)
        {
            // Create linear ramp and apply color temperature
            var normalizedValue = i / 255.0;
            
            ramp.Red[i] = (ushort)(Math.Min(65535, normalizedValue * rFactor * 65535));
            ramp.Green[i] = (ushort)(Math.Min(65535, normalizedValue * gFactor * 65535));
            ramp.Blue[i] = (ushort)(Math.Min(65535, normalizedValue * bFactor * 65535));
        }

        return ramp;
    }

    public void Dispose()
    {
        _transitionTimer?.Dispose();
        
        // Release monitor handles
        foreach (var (monitorId, handle) in _monitorHandles)
        {
            if (handle != IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, handle);
            }
        }
        
        _monitorHandles.Clear();
    }
}
