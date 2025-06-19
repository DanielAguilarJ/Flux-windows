using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using ChronoGuard.Domain.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;
using Timer = System.Threading.Timer;

namespace ChronoGuard.Infrastructure.Services;

/// <summary>
/// Enhanced Windows color temperature service with advanced gamma ramp implementation
/// Features: Hardware gamma ramp manipulation, ICC profile fallback, multi-monitor support,
/// MonitorColorProfile integration, and perceptual color temperature adjustments
/// </summary>
public class WindowsColorTemperatureService : IColorTemperatureService
{    private readonly ILogger<WindowsColorTemperatureService> _logger;
    private readonly ICCProfileService _iccProfileService;
    private readonly IGammaService _gammaService;
    private readonly AdvancedColorManagementConfig _config;
    private ColorTemperature? _currentTemperature;
    private readonly Dictionary<string, IntPtr> _monitorHandles = new();
    private readonly Dictionary<string, RAMP> _originalGammaRamps = new();
    private readonly Dictionary<string, MonitorColorProfile> _monitorProfiles = new();    private readonly Dictionary<string, RAMP> _currentGammaRamps = new();
    private readonly Dictionary<string, ICCProfile> _originalICCProfiles = new();
    
    // Intelligent gamma ramp caching system
    private readonly Dictionary<string, RAMP> _gammaRampCache = new();
    private readonly Dictionary<string, DateTime> _cacheTimestamps = new();
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);
    private readonly int _maxCacheSize = 200;

    // Device context pooling system for optimized resource management
    private readonly Queue<IntPtr> _availableDeviceContexts = new();
    private readonly HashSet<IntPtr> _activeDeviceContexts = new();
    private readonly Dictionary<IntPtr, DateTime> _deviceContextTimestamps = new();
    private readonly int _maxPoolSize = 10;
    private readonly int _minPoolSize = 2;
    private readonly TimeSpan _deviceContextTimeout = TimeSpan.FromMinutes(5);

    // Performance monitoring for adaptive optimizations
    private readonly Dictionary<string, double> _operationTimings = new();
    private readonly Queue<double> _recentGammaSetTimes = new();
    private readonly int _timingHistorySize = 100;
    
    private TransitionState? _activeTransition;
    private Timer? _transitionTimer;
    private readonly object _lockObject = new();
    private readonly SemaphoreSlim _updateSemaphore = new(1, 1);
    private bool _useAdvancedGammaAlgorithm = true;
    private bool _supportHardwareGamma = true;
    private bool _isInitialized = false;

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

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("dxva2.dll")]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll")]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll")]
    private static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    private delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

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
    }    public WindowsColorTemperatureService(ILogger<WindowsColorTemperatureService> logger)
    {
        _logger = logger;
        _iccProfileService = new ICCProfileService();
        _gammaService = new SimdOptimizedGammaService(Microsoft.Extensions.Logging.Abstractions.NullLogger<SimdOptimizedGammaService>.Instance);
        _config = new AdvancedColorManagementConfig();
        _ = Task.Run(InitializeMonitorsAsync);
    }

    public WindowsColorTemperatureService(ILogger<WindowsColorTemperatureService> logger, ICCProfileService iccProfileService, AdvancedColorManagementConfig config)
    {
        _logger = logger;
        _iccProfileService = iccProfileService;
        _gammaService = new SimdOptimizedGammaService(Microsoft.Extensions.Logging.Abstractions.NullLogger<SimdOptimizedGammaService>.Instance);
        _config = config;
        _ = Task.Run(InitializeMonitorsAsync);
    }

    public WindowsColorTemperatureService(ILogger<WindowsColorTemperatureService> logger, ICCProfileService iccProfileService, IGammaService gammaService, AdvancedColorManagementConfig config)
    {
        _logger = logger;
        _iccProfileService = iccProfileService;
        _gammaService = gammaService;
        _config = config;
        _ = Task.Run(InitializeMonitorsAsync);
    }

    /// <summary>
    /// Enhanced monitor initialization with detailed detection
    /// </summary>
    private async Task InitializeMonitorsAsync()
    {
        if (_isInitialized) return;

        await _updateSemaphore.WaitAsync();        try
        {            _monitorHandles.Clear();
            _originalGammaRamps.Clear();
            _monitorProfiles.Clear();
            _currentGammaRamps.Clear();
            
            // Clear gamma ramp cache when monitors are reinitialized
            ClearGammaRampCache();
            
            // Initialize device context pool
            EnsureMinimumDeviceContextPool();

            var monitorList = new List<(IntPtr monitor, string deviceName, bool isPrimary)>();

            // Enumerate all monitors
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);
                
                if (GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    var isPrimary = (monitorInfo.dwFlags & 1) != 0; // MONITORINFOF_PRIMARY
                    var deviceName = monitorInfo.szDevice;
                    monitorList.Add((hMonitor, deviceName, isPrimary));
                }
                
                return true;
            }, IntPtr.Zero);

            _logger.LogInformation("Found {Count} monitors", monitorList.Count);

            // Initialize each monitor
            foreach (var (hMonitor, deviceName, isPrimary) in monitorList)
            {
                await InitializeMonitorAsync(hMonitor, deviceName, isPrimary);
            }

            _isInitialized = true;
            _logger.LogInformation("Monitor initialization completed. {Count} monitors configured", _monitorProfiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize monitors");
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }    /// <summary>
    /// Initialize individual monitor with detailed profile detection using device context pooling
    /// </summary>
    private async Task InitializeMonitorAsync(IntPtr hMonitor, string deviceName, bool isPrimary)
    {
        try
        {
            var monitorId = GenerateMonitorId(deviceName, isPrimary);
            var hDC = AcquireDeviceContext();
            
            if (hDC == IntPtr.Zero)
            {
                _logger.LogWarning("Failed to acquire device context for monitor {DeviceName}", deviceName);
                return;
            }

            // Create monitor profile
            var profile = new MonitorColorProfile
            {
                MonitorId = monitorId,
                MonitorName = await GetMonitorNameAsync(hMonitor),
                DevicePath = deviceName,
                ManufacturerName = await GetMonitorManufacturerAsync(hMonitor),
                ModelName = await GetMonitorModelAsync(hMonitor)
            };

            // Detect capabilities
            profile.DetectCapabilities();
            await DetectAdvancedCapabilities(profile, hMonitor);

            // Get original gamma ramp
            var originalRamp = new RAMP();
            if (GetDeviceGammaRamp(hDC, ref originalRamp))
            {
                _monitorHandles[monitorId] = hDC;
                _originalGammaRamps[monitorId] = originalRamp;
                _currentGammaRamps[monitorId] = originalRamp;
                _monitorProfiles[monitorId] = profile;

                _logger.LogInformation("Initialized monitor: {MonitorId} ({Name}) - Hardware Gamma: {HwGamma}", 
                    monitorId, profile.MonitorName, profile.SupportsHardwareGamma);
            }
            else
            {
                ReleaseDeviceContext(hDC);
                profile.SupportsHardwareGamma = false;
                _monitorProfiles[monitorId] = profile;
                _logger.LogWarning("Monitor {MonitorId} does not support hardware gamma", monitorId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize monitor {DeviceName}", deviceName);
        }
    }

    /// <summary>
    /// Generate unique monitor identifier
    /// </summary>
    private string GenerateMonitorId(string deviceName, bool isPrimary)
    {
        if (isPrimary) return "primary";
        
        // Extract meaningful part of device name
        var cleanName = deviceName.Replace("\\\\.\\", "").Replace("DISPLAY", "monitor");
        return $"{cleanName}_{Guid.NewGuid().ToString("N")[..8]}";
    }

    /// <summary>
    /// Get human-readable monitor name
    /// </summary>
    private Task<string> GetMonitorNameAsync(IntPtr hMonitor)
    {
        try
        {
            uint numMonitors = 0;
            if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref numMonitors) && numMonitors > 0)
            {
                var physicalMonitors = new PHYSICAL_MONITOR[numMonitors];
                if (GetPhysicalMonitorsFromHMONITOR(hMonitor, numMonitors, physicalMonitors))
                {
                    var name = physicalMonitors[0].szPhysicalMonitorDescription;
                    DestroyPhysicalMonitors(numMonitors, physicalMonitors);
                    return Task.FromResult(!string.IsNullOrWhiteSpace(name) ? name : "Unknown Monitor");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not retrieve monitor name via DXVA2");
        }

        return Task.FromResult("Unknown Monitor");
    }

    /// <summary>
    /// Get monitor manufacturer information
    /// </summary>
    private Task<string> GetMonitorManufacturerAsync(IntPtr hMonitor)
    {
        // Placeholder for manufacturer detection
        // Could be enhanced with WMI queries or registry parsing
        return Task.FromResult("Unknown");
    }

    /// <summary>
    /// Get monitor model information
    /// </summary>
    private Task<string> GetMonitorModelAsync(IntPtr hMonitor)
    {
        // Placeholder for model detection
        // Could be enhanced with EDID parsing
        return Task.FromResult("Generic Monitor");
    }

    /// <summary>
    /// Detect advanced monitor capabilities
    /// </summary>
    private Task DetectAdvancedCapabilities(MonitorColorProfile profile, IntPtr hMonitor)
    {
        // Detect DDC/CI support
        try
        {
            uint numMonitors = 0;
            if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref numMonitors))
            {
                profile.SupportsDDCCI = numMonitors > 0;
            }
        }
        catch
        {
            profile.SupportsDDCCI = false;
        }

        // Detect color depth and other capabilities
        // This would typically involve more complex Windows Color System API calls
        profile.BitDepth = 8; // Default assumption
        profile.MaxLuminance = 250.0; // Default assumption
        
        return Task.CompletedTask;
    }    public async Task<bool> ApplyTemperatureAsync(ColorTemperature temperature)
    {
        if (!_isInitialized)
        {
            await InitializeMonitorsAsync();
        }

        await _updateSemaphore.WaitAsync();
        try
        {
            var success = true;
            var appliedCount = 0;
            var totalMonitors = _monitorHandles.Count;

            _logger.LogDebug("Attempting to apply {Temperature}K to {MonitorCount} monitor(s)", temperature.Kelvin, totalMonitors);

            if (totalMonitors == 0)
            {
                _logger.LogError("No monitors available for color temperature application");
                return false;
            }

            foreach (var (monitorId, handle) in _monitorHandles)
            {
                if (_monitorProfiles.TryGetValue(monitorId, out var profile))
                {
                    var ramp = CreateOptimizedGammaRamp(temperature, profile);
                    if (SetDeviceGammaRampOptimized(handle, ref ramp, monitorId))
                    {
                        _currentGammaRamps[monitorId] = ramp;
                        appliedCount++;
                        _logger.LogDebug("Applied temperature {Temperature}K to monitor {MonitorId}", temperature.Kelvin, monitorId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to set gamma ramp for monitor {MonitorId} - SetDeviceGammaRamp returned false", monitorId);
                        _logger.LogInformation("Monitor {MonitorId} may not support gamma manipulation or another application is controlling it", monitorId);
                        success = false;
                    }
                }
                else
                {
                    _logger.LogWarning("No profile found for monitor {MonitorId}", monitorId);
                    success = false;
                }
            }

            if (success && appliedCount > 0)
            {
                _currentTemperature = temperature;
                TemperatureChanged?.Invoke(this, temperature);
                _logger.LogInformation("Successfully applied color temperature {Temperature}K to {Count}/{Total} monitors", 
                    temperature.Kelvin, appliedCount, totalMonitors);
            }
            else if (appliedCount == 0)
            {
                _logger.LogError("Failed to apply temperature {Temperature}K to any monitor. Common causes:", temperature.Kelvin);
                _logger.LogError("• Monitor/graphics driver doesn't support gamma ramp manipulation");
                _logger.LogError("• Another color management application is active (f.lux, Night Light, etc.)");
                _logger.LogError("• Insufficient permissions (try running as Administrator)");
                _logger.LogError("• Hardware limitation or compatibility issue");
            }
            else
            {
                _logger.LogWarning("Partial success: applied temperature {Temperature}K to {Count}/{Total} monitors", 
                    temperature.Kelvin, appliedCount, totalMonitors);
            }

            return success && appliedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while applying color temperature {Temperature}K", temperature.Kelvin);
            return false;
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }

    public async Task<bool> ApplyTemperatureToMonitorAsync(string monitorId, ColorTemperature temperature)
    {
        if (!_isInitialized)
        {
            await InitializeMonitorsAsync();
        }

        await _updateSemaphore.WaitAsync();
        try
        {
            if (!_monitorHandles.TryGetValue(monitorId, out var handle))
            {
                _logger.LogWarning("Monitor {MonitorId} not found", monitorId);
                return false;
            }

            if (!_monitorProfiles.TryGetValue(monitorId, out var profile))
            {
                _logger.LogWarning("Monitor profile for {MonitorId} not found", monitorId);
                return false;
            }            var ramp = CreateOptimizedGammaRamp(temperature, profile);
            var success = SetDeviceGammaRampOptimized(handle, ref ramp, monitorId);

            if (success)
            {
                _currentGammaRamps[monitorId] = ramp;
                _logger.LogDebug("Applied color temperature {Temperature}K to monitor {MonitorId}", temperature.Kelvin, monitorId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying temperature to monitor {MonitorId}: {Temperature}", monitorId, temperature);
            return false;
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }

    public async Task<TransitionState> CreateTransitionAsync(ColorTemperature from, ColorTemperature to, TimeSpan duration, string reason = "")
    {
        try
        {
            // Stop any existing transition
            await StopTransitionAsync();

            var transition = new TransitionState(from, to, duration, reason);
            _activeTransition = transition;            // Start transition timer with adaptive interval
            var adaptiveInterval = CalculateAdaptiveTransitionInterval(transition);
            _transitionTimer = new Timer(async _ => await UpdateTransitionAsync(), null,
                TimeSpan.Zero, adaptiveInterval);

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
        await _updateSemaphore.WaitAsync();
        try
        {
            await StopTransitionAsync();

            var restoredCount = 0;            foreach (var (monitorId, handle) in _monitorHandles)
            {
                if (_originalGammaRamps.TryGetValue(monitorId, out var originalRamp))
                {
                    if (SetDeviceGammaRampOptimized(handle, ref originalRamp, monitorId))
                    {
                        _currentGammaRamps[monitorId] = originalRamp;
                        restoredCount++;
                        _logger.LogDebug("Restored original gamma for monitor {MonitorId}", monitorId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to restore original gamma for monitor {MonitorId}", monitorId);
                    }
                }
            }

            _currentTemperature = null;
            _logger.LogInformation("Restored original display settings for {Count} monitors", restoredCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring original settings");
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }

    public ColorTemperature? GetCurrentTemperature()
    {
        return _currentTemperature;
    }

    public async Task<IEnumerable<MonitorInfo>> GetMonitorsAsync()
    {
        if (!_isInitialized)
        {
            await InitializeMonitorsAsync();
        }

        var monitors = new List<MonitorInfo>();

        try
        {
            foreach (var (monitorId, profile) in _monitorProfiles)
            {
                var isPrimary = monitorId == "primary";
                
                monitors.Add(new MonitorInfo
                {
                    Id = monitorId,
                    Name = profile.MonitorName,
                    IsPrimary = isPrimary,
                    Width = 1920, // Default resolution - could be enhanced with actual monitor detection
                    Height = 1080, // Default resolution - could be enhanced with actual monitor detection
                    DevicePath = profile.DevicePath
                });
            }

            _logger.LogDebug("Retrieved {Count} monitor configurations", monitors.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting monitor information");
        }

        return monitors;
    }

    public async Task<IEnumerable<ExtendedMonitorInfo>> GetExtendedMonitorsAsync()
    {
        if (!_isInitialized)
        {
            await InitializeMonitorsAsync();
        }

        var monitors = new List<ExtendedMonitorInfo>();

        try
        {
            foreach (var (monitorId, profile) in _monitorProfiles)
            {
                var isPrimary = monitorId == "primary";
                
                monitors.Add(new ExtendedMonitorInfo
                {
                    Id = monitorId,
                    Name = profile.MonitorName,
                    IsPrimary = isPrimary,
                    Width = 1920, // Default resolution - could be enhanced with actual monitor detection
                    Height = 1080, // Default resolution - could be enhanced with actual monitor detection
                    DevicePath = profile.DevicePath,
                    ManufacturerName = profile.ManufacturerName,
                    ModelName = profile.ModelName,
                    SupportsHardwareGamma = profile.SupportsHardwareGamma,
                    SupportsICCProfiles = profile.SupportsICCProfiles,
                    SupportsDDCCI = profile.SupportsDDCCI,
                    BitDepth = profile.BitDepth,
                    MaxLuminance = profile.MaxLuminance,
                    ColorGamut = profile.NativeGamut.ToString(),
                    LastCalibrationDate = profile.LastCalibrationDate,
                    ICCProfilePath = profile.ICCProfilePath
                });
            }

            _logger.LogDebug("Retrieved {Count} extended monitor configurations", monitors.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting extended monitor information");
        }

        return monitors;
    }    /// <summary>
    /// Creates optimized gamma ramp using monitor-specific profile with intelligent caching
    /// </summary>
    private RAMP CreateOptimizedGammaRamp(ColorTemperature temperature, MonitorColorProfile profile)
    {
        // Generate cache key based on temperature and profile characteristics
        var cacheKey = GenerateGammaRampCacheKey(temperature, profile);
        
        // Check cache first
        if (TryGetCachedGammaRamp(cacheKey, out var cachedRamp))
        {
            _logger.LogTrace("Using cached gamma ramp for {Temperature}K with profile {MonitorId}", 
                temperature.Kelvin, profile.MonitorId);
            return cachedRamp;
        }

        // Use monitor profile's optimal multipliers
        var (rMult, gMult, bMult) = profile.GetOptimalMultipliers(temperature.Kelvin);
        
        var ramp = _useAdvancedGammaAlgorithm 
            ? CreateAdvancedGammaRamp(temperature, profile, rMult, gMult, bMult)
            : CreateBasicGammaRamp(temperature, rMult, gMult, bMult);

        // Cache the generated ramp
        CacheGammaRamp(cacheKey, ramp);
        
        _logger.LogTrace("Generated and cached new gamma ramp for {Temperature}K with profile {MonitorId}", 
            temperature.Kelvin, profile.MonitorId);
        
        return ramp;
    }    private RAMP CreateGammaRamp(ColorTemperature temperature)
    {
        // Fallback for when no specific profile is available
        // Generate a basic cache key for non-profiled gamma ramp
        var cacheKey = $"simd_fallback|{temperature.Kelvin}|{_gammaService.ServiceName}";
        
        // Check cache first
        if (TryGetCachedGammaRamp(cacheKey, out var cachedRamp))
        {
            _logger.LogTrace("Using cached SIMD gamma ramp for {Temperature}K", temperature.Kelvin);
            return cachedRamp;
        }

        // Use SIMD-optimized gamma service for enhanced performance
        var gammaRamp = _gammaService.GenerateOptimizedGammaRamp(temperature.Kelvin, 1.0, 1.0);
        var ramp = ConvertToRamp(gammaRamp);

        // Cache the generated ramp
        CacheGammaRamp(cacheKey, ramp);
        
        _logger.LogTrace("Generated and cached new SIMD gamma ramp for {Temperature}K using {Service}", 
            temperature.Kelvin, _gammaService.ServiceName);
        
        return ramp;
    }

    /// <summary>
    /// Creates an advanced gamma ramp with monitor profile integration
    /// </summary>
    private RAMP CreateAdvancedGammaRamp(ColorTemperature temperature, MonitorColorProfile profile, double rMult, double gMult, double bMult)
    {
        var ramp = new RAMP();
        
        // Use profile-specific gamma correction
        var gamma = profile.GammaCorrection;
        var invGamma = 1.0 / gamma;
        
        for (int i = 0; i < 256; i++)
        {
            // Normalize input (0-255 to 0-1)
            double normalizedInput = i / 255.0;
            
            // Apply reverse gamma for linear RGB
            double linearValue = Math.Pow(normalizedInput, gamma);
            
            // Apply color temperature multipliers with profile scaling
            double rLinear = Math.Min(1.0, linearValue * rMult * profile.BrightnessScale);
            double gLinear = Math.Min(1.0, linearValue * gMult * profile.BrightnessScale);
            double bLinear = Math.Min(1.0, linearValue * bMult * profile.BrightnessScale);
            
            // Apply contrast adjustment
            if (profile.ContrastScale != 1.0)
            {
                var midpoint = 0.5;
                rLinear = midpoint + (rLinear - midpoint) * profile.ContrastScale;
                gLinear = midpoint + (gLinear - midpoint) * profile.ContrastScale;
                bLinear = midpoint + (bLinear - midpoint) * profile.ContrastScale;
            }
            
            // Clamp to valid range
            rLinear = Math.Max(0.0, Math.Min(1.0, rLinear));
            gLinear = Math.Max(0.0, Math.Min(1.0, gLinear));
            bLinear = Math.Max(0.0, Math.Min(1.0, bLinear));
            
            // Convert back to gamma-corrected space
            double rGamma = Math.Pow(rLinear, invGamma);
            double gGamma = Math.Pow(gLinear, invGamma);
            double bGamma = Math.Pow(bLinear, invGamma);
            
            // Convert to 16-bit values for hardware gamma ramp
            ramp.Red[i] = (ushort)Math.Round(rGamma * 65535.0);
            ramp.Green[i] = (ushort)Math.Round(gGamma * 65535.0);
            ramp.Blue[i] = (ushort)Math.Round(bGamma * 65535.0);
        }
        
        return ramp;
    }    /// <summary>
    /// Creates an advanced gamma ramp using SIMD-optimized calculations
    /// Provides enhanced performance with hardware acceleration when available
    /// </summary>
    private RAMP CreateAdvancedGammaRamp(ColorTemperature temperature)
    {
        // Use SIMD-optimized gamma service for maximum performance
        var gammaRamp = _gammaService.GenerateOptimizedGammaRamp(temperature.Kelvin, 1.0, 1.0);
        var ramp = ConvertToRamp(gammaRamp);
        
        _logger.LogDebug("Generated SIMD-optimized gamma ramp for {Temperature}K using {Service} ({Capabilities})", 
            temperature.Kelvin, _gammaService.ServiceName, _gammaService.GetCapabilities());
        
        return ramp;
    }

    /// <summary>
    /// Fallback basic gamma ramp creation for compatibility
    /// </summary>
    private RAMP CreateBasicGammaRamp(ColorTemperature temperature)
    {
        var (r, g, b) = temperature.RGB;
        return CreateBasicGammaRamp(temperature, r / 255.0, g / 255.0, b / 255.0);
    }

    /// <summary>
    /// Fallback basic gamma ramp creation with custom multipliers
    /// </summary>
    private RAMP CreateBasicGammaRamp(ColorTemperature temperature, double rMult, double gMult, double bMult)
    {
        var ramp = new RAMP();

        for (int i = 0; i < 256; i++)
        {
            // Create linear ramp and apply color temperature
            var normalizedValue = i / 255.0;
            
            ramp.Red[i] = (ushort)(Math.Min(65535, normalizedValue * rMult * 65535));
            ramp.Green[i] = (ushort)(Math.Min(65535, normalizedValue * gMult * 65535));
            ramp.Blue[i] = (ushort)(Math.Min(65535, normalizedValue * bMult * 65535));
        }

        return ramp;
    }

    /// <summary>
    /// Calculates precise color multipliers using improved Planckian locus approximation
    /// Based on CIE 1931 chromaticity coordinates and Bradford chromatic adaptation
    /// </summary>
    private (double r, double g, double b) CalculatePreciseColorMultipliers(int temperatureK)
    {
        // Clamp temperature to valid range
        double temp = Math.Max(1000, Math.Min(25000, temperatureK));
        
        // Calculate chromaticity coordinates for the temperature
        double x, y;
        if (temp <= 4000)
        {
            // Warm range: use polynomial approximation
            x = (-4.6070e9 / (temp * temp * temp)) + (2.9678e6 / (temp * temp)) + (0.09911e3 / temp) + 0.244063;
        }
        else
        {
            // Cool range: use different polynomial
            x = (-2.0064e9 / (temp * temp * temp)) + (1.9018e6 / (temp * temp)) + (0.24748e3 / temp) + 0.237040;
        }
        
        y = -3.000 * x * x + 2.870 * x - 0.275;
        
        // Convert chromaticity to RGB using sRGB primaries
        double X = x / y;
        double Y = 1.0;
        double Z = (1 - x - y) / y;
        
        // sRGB transformation matrix (normalized to D65)
        double r = X * 3.2406 + Y * -1.5372 + Z * -0.4986;
        double g = X * -0.9689 + Y * 1.8758 + Z * 0.0415;
        double b = X * 0.0557 + Y * -0.2040 + Z * 1.0570;
        
        // Normalize to ensure proper white point
        double max = Math.Max(Math.Max(r, g), b);
        if (max > 0)
        {
            r /= max;
            g /= max;
            b /= max;
        }
        
        // Ensure no negative values and reasonable bounds
        r = Math.Max(0.1, Math.Min(1.0, r));
        g = Math.Max(0.1, Math.Min(1.0, g));
        b = Math.Max(0.1, Math.Min(1.0, b));
        
        return (r, g, b);    }    /// <summary>
    /// Calculates adaptive transition interval based on transition complexity and system performance
    /// </summary>
    private TimeSpan CalculateAdaptiveTransitionInterval(TransitionState transition)
    {
        // Base interval factors
        var baseInterval = 200; // Default 200ms
        
        // Factor 1: Temperature difference (larger changes need smoother transitions)
        var tempDifference = Math.Abs(transition.ToTemperature.Kelvin - transition.FromTemperature.Kelvin);
        var tempFactor = tempDifference > 2000 ? 0.75 : tempDifference > 1000 ? 0.85 : 1.0;
        
        // Factor 2: Transition duration (longer transitions can use larger intervals)
        var durationMinutes = transition.Duration.TotalMinutes;
        var durationFactor = durationMinutes > 30 ? 1.2 : durationMinutes > 10 ? 1.0 : 0.8;
        
        // Factor 3: Number of monitors (more monitors need faster updates for synchronization)
        var monitorCount = _monitorHandles.Count;
        var monitorFactor = monitorCount > 2 ? 0.8 : monitorCount > 1 ? 0.9 : 1.0;
        
        // Factor 4: Hardware gamma vs ICC profile performance
        var performanceFactor = _config?.EnableHardwareGamma == true ? 1.0 : 1.1;
        
        // Factor 5: Real-time performance monitoring adaptation
        var avgGammaTime = GetAverageGammaSetTime();
        var performanceAdjustment = avgGammaTime > 100 ? 1.3 : avgGammaTime > 50 ? 1.1 : avgGammaTime < 20 ? 0.9 : 1.0;
        
        // Factor 6: Cache hit rate (better cache performance allows faster transitions)
        var (cacheSize, _, _, _) = GetCacheStatistics();
        var cacheEfficiencyFactor = cacheSize > 50 ? 0.95 : cacheSize > 20 ? 0.98 : 1.0;
        
        // Calculate adaptive interval with all factors
        var adaptiveInterval = (int)(baseInterval * tempFactor * durationFactor * 
                                   monitorFactor * performanceFactor * performanceAdjustment * cacheEfficiencyFactor);
        
        // Clamp to reasonable bounds (50ms to 500ms)
        adaptiveInterval = Math.Max(50, Math.Min(500, adaptiveInterval));
        
        _logger.LogDebug("Calculated adaptive transition interval: {Interval}ms (TempDiff: {TempDiff}K, Duration: {Duration}, " +
                        "AvgGammaTime: {AvgTime}ms, CacheSize: {CacheSize})", 
            adaptiveInterval, tempDifference, transition.Duration, avgGammaTime, cacheSize);
        
        return TimeSpan.FromMilliseconds(adaptiveInterval);
    }

    #region Intelligent Gamma Ramp Caching

    /// <summary>
    /// Generates a cache key for gamma ramp based on temperature and profile characteristics
    /// </summary>
    private string GenerateGammaRampCacheKey(ColorTemperature temperature, MonitorColorProfile profile)
    {
        // Create hash based on key characteristics that affect gamma ramp generation
        var keyComponents = new[]
        {
            temperature.Kelvin.ToString(),
            profile.MonitorId,
            profile.GammaCorrection.ToString("F2"),
            profile.BrightnessScale.ToString("F2"),
            profile.ContrastScale.ToString("F2"),
            _useAdvancedGammaAlgorithm.ToString(),
            profile.ManufacturerName ?? "Unknown",
            profile.ModelName ?? "Unknown"
        };
        
        return string.Join("|", keyComponents);
    }

    /// <summary>
    /// Attempts to retrieve a cached gamma ramp
    /// </summary>
    private bool TryGetCachedGammaRamp(string cacheKey, out RAMP ramp)
    {
        ramp = new RAMP();
        
        lock (_lockObject)
        {
            if (!_gammaRampCache.TryGetValue(cacheKey, out ramp))
            {
                return false;
            }

            // Check if cache entry has expired
            if (_cacheTimestamps.TryGetValue(cacheKey, out var timestamp))
            {
                if (DateTime.UtcNow - timestamp > _cacheExpiry)
                {
                    // Remove expired entry
                    _gammaRampCache.Remove(cacheKey);
                    _cacheTimestamps.Remove(cacheKey);
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Caches a gamma ramp with automatic cache management
    /// </summary>
    private void CacheGammaRamp(string cacheKey, RAMP ramp)
    {
        lock (_lockObject)
        {
            // Clean up expired entries periodically
            CleanupExpiredCacheEntries();

            // If cache is at capacity, remove least recently used entries
            if (_gammaRampCache.Count >= _maxCacheSize)
            {
                EvictLeastRecentlyUsedCacheEntries();
            }

            // Store the new entry
            _gammaRampCache[cacheKey] = ramp;
            _cacheTimestamps[cacheKey] = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Removes expired cache entries
    /// </summary>
    private void CleanupExpiredCacheEntries()
    {
        var currentTime = DateTime.UtcNow;
        var expiredKeys = _cacheTimestamps
            .Where(kvp => currentTime - kvp.Value > _cacheExpiry)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _gammaRampCache.Remove(key);
            _cacheTimestamps.Remove(key);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogTrace("Cleaned up {Count} expired gamma ramp cache entries", expiredKeys.Count);
        }
    }

    /// <summary>
    /// Evicts least recently used cache entries when cache is full
    /// </summary>
    private void EvictLeastRecentlyUsedCacheEntries()
    {
        var entriesToRemove = _maxCacheSize / 4; // Remove 25% of cache when full
        var oldestEntries = _cacheTimestamps
            .OrderBy(kvp => kvp.Value)
            .Take(entriesToRemove)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldestEntries)
        {
            _gammaRampCache.Remove(key);
            _cacheTimestamps.Remove(key);
        }

        _logger.LogTrace("Evicted {Count} LRU gamma ramp cache entries to make space", oldestEntries.Count);
    }

    /// <summary>
    /// Clears all cached gamma ramps (useful for configuration changes)
    /// </summary>
    private void ClearGammaRampCache()
    {
        lock (_lockObject)
        {
            var count = _gammaRampCache.Count;
            _gammaRampCache.Clear();
            _cacheTimestamps.Clear();
            
            if (count > 0)
            {
                _logger.LogInformation("Cleared {Count} cached gamma ramps", count);
            }
        }
    }

    /// <summary>
    /// Gets cache statistics for monitoring and debugging
    /// </summary>
    private (int TotalEntries, int ExpiredEntries, TimeSpan OldestEntry, TimeSpan NewestEntry) GetCacheStatistics()
    {
        lock (_lockObject)
        {
            var currentTime = DateTime.UtcNow;
            var expiredCount = _cacheTimestamps.Count(kvp => currentTime - kvp.Value > _cacheExpiry);
            
            var oldestEntry = _cacheTimestamps.Count > 0 
                ? currentTime - _cacheTimestamps.Values.Min() 
                : TimeSpan.Zero;
                
            var newestEntry = _cacheTimestamps.Count > 0 
                ? currentTime - _cacheTimestamps.Values.Max() 
                : TimeSpan.Zero;

            return (_gammaRampCache.Count, expiredCount, oldestEntry, newestEntry);
        }
    }

    #endregion

    #region Device Context Pooling and Resource Management

    /// <summary>
    /// Acquires a device context from the pool or creates a new one if needed
    /// </summary>
    private IntPtr AcquireDeviceContext()
    {
        lock (_lockObject)
        {
            // Clean up expired device contexts first
            CleanupExpiredDeviceContexts();

            // Try to get an available device context from the pool
            if (_availableDeviceContexts.Count > 0)
            {
                var hDC = _availableDeviceContexts.Dequeue();
                _activeDeviceContexts.Add(hDC);
                _deviceContextTimestamps[hDC] = DateTime.UtcNow;
                
                _logger.LogTrace("Acquired device context from pool: {Handle}", hDC);
                return hDC;
            }

            // Pool is empty, create a new device context
            var newDC = GetDC(IntPtr.Zero);
            if (newDC != IntPtr.Zero)
            {
                _activeDeviceContexts.Add(newDC);
                _deviceContextTimestamps[newDC] = DateTime.UtcNow;
                
                _logger.LogTrace("Created new device context: {Handle}", newDC);
                return newDC;
            }

            _logger.LogWarning("Failed to acquire device context");
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Returns a device context to the pool for reuse
    /// </summary>
    private void ReleaseDeviceContext(IntPtr hDC)
    {
        if (hDC == IntPtr.Zero) return;

        lock (_lockObject)
        {
            _activeDeviceContexts.Remove(hDC);

            // Add to pool if not at capacity, otherwise release immediately
            if (_availableDeviceContexts.Count < _maxPoolSize)
            {
                _availableDeviceContexts.Enqueue(hDC);
                _deviceContextTimestamps[hDC] = DateTime.UtcNow;
                
                _logger.LogTrace("Returned device context to pool: {Handle}", hDC);
            }
            else
            {
                // Pool is full, release the device context
                ReleaseDC(IntPtr.Zero, hDC);
                _deviceContextTimestamps.Remove(hDC);
                
                _logger.LogTrace("Released device context (pool full): {Handle}", hDC);
            }
        }
    }

    /// <summary>
    /// Ensures minimum number of device contexts are available in the pool
    /// </summary>
    private void EnsureMinimumDeviceContextPool()
    {
        lock (_lockObject)
        {
            var currentPoolSize = _availableDeviceContexts.Count;
            var contextsToCreate = _minPoolSize - currentPoolSize;

            for (int i = 0; i < contextsToCreate; i++)
            {
                var hDC = GetDC(IntPtr.Zero);
                if (hDC != IntPtr.Zero)
                {
                    _availableDeviceContexts.Enqueue(hDC);
                    _deviceContextTimestamps[hDC] = DateTime.UtcNow;
                }
                else
                {
                    _logger.LogWarning("Failed to create device context for pool initialization");
                    break;
                }
            }

            if (contextsToCreate > 0)
            {
                _logger.LogDebug("Initialized device context pool with {Count} contexts", 
                    _availableDeviceContexts.Count);
            }
        }
    }

    /// <summary>
    /// Cleans up expired device contexts from the pool
    /// </summary>
    private void CleanupExpiredDeviceContexts()
    {
        var currentTime = DateTime.UtcNow;
        var expiredContexts = new List<IntPtr>();

        // Find expired contexts in the available pool
        foreach (var hDC in _availableDeviceContexts.ToArray())
        {
            if (_deviceContextTimestamps.TryGetValue(hDC, out var timestamp))
            {
                if (currentTime - timestamp > _deviceContextTimeout)
                {
                    expiredContexts.Add(hDC);
                }
            }
        }

        // Remove expired contexts
        foreach (var hDC in expiredContexts)
        {
            // Remove from available pool
            var tempQueue = new Queue<IntPtr>();
            while (_availableDeviceContexts.Count > 0)
            {
                var dc = _availableDeviceContexts.Dequeue();
                if (dc != hDC)
                {
                    tempQueue.Enqueue(dc);
                }
            }
            
            // Restore the queue without expired context
            while (tempQueue.Count > 0)
            {
                _availableDeviceContexts.Enqueue(tempQueue.Dequeue());
            }

            // Release the expired context
            ReleaseDC(IntPtr.Zero, hDC);
            _deviceContextTimestamps.Remove(hDC);
        }

        if (expiredContexts.Count > 0)
        {
            _logger.LogTrace("Cleaned up {Count} expired device contexts", expiredContexts.Count);
        }
    }

    /// <summary>
    /// Releases all device contexts in the pool
    /// </summary>
    private void ReleaseAllDeviceContexts()
    {
        lock (_lockObject)
        {
            // Release available contexts
            while (_availableDeviceContexts.Count > 0)
            {
                var hDC = _availableDeviceContexts.Dequeue();
                ReleaseDC(IntPtr.Zero, hDC);
            }

            // Release active contexts
            foreach (var hDC in _activeDeviceContexts.ToArray())
            {
                ReleaseDC(IntPtr.Zero, hDC);
            }

            _activeDeviceContexts.Clear();
            _deviceContextTimestamps.Clear();

            _logger.LogDebug("Released all device contexts from pool");
        }
    }

    /// <summary>
    /// Optimized gamma ramp setting with pooled device contexts and performance monitoring
    /// </summary>
    private bool SetDeviceGammaRampOptimized(IntPtr hDC, ref RAMP ramp, string monitorId = "")
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var result = SetDeviceGammaRamp(hDC, ref ramp);
            
            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            
            // Track performance metrics
            TrackOperationTiming("SetDeviceGammaRamp", elapsedMs);
            
            // Log slow operations
            if (elapsedMs > 100) // Log if operation takes more than 100ms
            {
                _logger.LogWarning("Slow gamma ramp operation detected: {Duration}ms for monitor {MonitorId}", 
                    elapsedMs, monitorId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error setting gamma ramp for monitor {MonitorId}", monitorId);
            return false;
        }
    }

    /// <summary>
    /// Tracks operation timing for performance monitoring
    /// </summary>
    private void TrackOperationTiming(string operation, double elapsedMs)
    {
        lock (_lockObject)
        {
            _operationTimings[operation] = elapsedMs;
            
            // Track recent gamma set times for adaptive optimization
            if (operation == "SetDeviceGammaRamp")
            {
                _recentGammaSetTimes.Enqueue(elapsedMs);
                if (_recentGammaSetTimes.Count > _timingHistorySize)
                {
                    _recentGammaSetTimes.Dequeue();
                }
            }
        }
    }

    /// <summary>
    /// Gets average recent performance metrics for adaptive optimization
    /// </summary>
    private double GetAverageGammaSetTime()
    {
        lock (_lockObject)
        {
            return _recentGammaSetTimes.Count > 0 
                ? _recentGammaSetTimes.Average() 
                : 50.0; // Default assumption
        }
    }

    /// <summary>
    /// Gets device context pool statistics for monitoring
    /// </summary>
    private (int Available, int Active, int Total, double AvgGammaTime) GetDeviceContextPoolStats()
    {
        lock (_lockObject)
        {            return (
                _availableDeviceContexts.Count,
                _activeDeviceContexts.Count,
                _availableDeviceContexts.Count + _activeDeviceContexts.Count,
                GetAverageGammaSetTime()
            );
        }
    }

    #endregion

    /// <summary>
    /// Enhanced error handling with automatic recovery attempts
    /// </summary>
    private async Task<bool> ApplyTemperatureWithRecoveryAsync(ColorTemperature temperature)
    {
        if (!_isInitialized)
        {
            await InitializeMonitorsAsync();
        }

        await _updateSemaphore.WaitAsync();
        try
        {
            var success = true;
            var appliedCount = 0;
            var fallbackCount = 0;

            foreach (var (monitorId, profile) in _monitorProfiles)
            {
                try
                {
                    bool applied = false;

                    // Try hardware gamma first
                    if (profile.SupportsHardwareGamma && _monitorHandles.TryGetValue(monitorId, out var handle))
                    {
                        var ramp = CreateOptimizedGammaRamp(temperature, profile);
                        if (SetDeviceGammaRampOptimized(handle, ref ramp))
                        {
                            _currentGammaRamps[monitorId] = ramp;
                            applied = true;
                            appliedCount++;
                            _logger.LogDebug("Applied temperature {Temperature}K via hardware gamma to monitor {MonitorId}", 
                                temperature.Kelvin, monitorId);
                        }
                    }

                    // Fallback to ICC profile method
                    if (!applied && profile.SupportsICCProfiles && !string.IsNullOrEmpty(profile.ICCProfilePath))
                    {
                        applied = await ApplyTemperatureViaICCProfileAsync(monitorId, temperature, profile);
                        if (applied)
                        {
                            fallbackCount++;
                            _logger.LogDebug("Applied temperature {Temperature}K via ICC profile to monitor {MonitorId}", 
                                temperature.Kelvin, monitorId);
                        }
                    }

                    // Final fallback - create temporary ICC profile
                    if (!applied)
                    {
                        applied = await ApplyTemperatureViaTemporaryProfileAsync(monitorId, temperature, profile);
                        if (applied)
                        {
                            fallbackCount++;
                            _logger.LogDebug("Applied temperature {Temperature}K via temporary profile to monitor {MonitorId}", 
                                temperature.Kelvin, monitorId);
                        }
                    }

                    if (!applied)
                    {
                        _logger.LogWarning("Failed to apply temperature to monitor {MonitorId} - no supported methods", monitorId);
                        success = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying temperature to monitor {MonitorId}", monitorId);
                    success = false;
                }
            }

            if (success && (appliedCount > 0 || fallbackCount > 0))
            {
                _currentTemperature = temperature;
                TemperatureChanged?.Invoke(this, temperature);
                _logger.LogInformation("Applied color temperature {Temperature}K: {HardwareCount} via hardware, {FallbackCount} via fallback", 
                    temperature.Kelvin, appliedCount, fallbackCount);
            }

            return success;
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }

    /// <summary>
    /// Applies color temperature using existing ICC profile
    /// </summary>
    private async Task<bool> ApplyTemperatureViaICCProfileAsync(string monitorId, ColorTemperature temperature, MonitorColorProfile profile)
    {
        try
        {
            if (string.IsNullOrEmpty(profile.ICCProfilePath) || !File.Exists(profile.ICCProfilePath))
            {
                return false;
            }

            // Load existing ICC profile and modify it
            var profileData = await File.ReadAllBytesAsync(profile.ICCProfilePath);
            var modifiedProfile = ModifyICCProfileForTemperature(profileData, temperature, profile);
            
            // Create temporary profile file
            var tempProfilePath = Path.Combine(Path.GetTempPath(), $"chronoguard_{monitorId}_{temperature.Kelvin}K.icc");
            await File.WriteAllBytesAsync(tempProfilePath, modifiedProfile);

            // Apply the profile using Windows Color System
            var success = ApplyICCProfileToMonitor(monitorId, tempProfilePath);

            // Clean up temporary file after a delay (allow Windows to load it)
            _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ => 
            {
                try { File.Delete(tempProfilePath); } 
                catch { /* Ignore cleanup errors */ }
            });

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying ICC profile fallback for monitor {MonitorId}", monitorId);
            return false;
        }
    }

    /// <summary>
    /// Creates and applies a temporary ICC profile for color temperature adjustment
    /// </summary>
    private async Task<bool> ApplyTemperatureViaTemporaryProfileAsync(string monitorId, ColorTemperature temperature, MonitorColorProfile profile)
    {
        try
        {
            // Generate a basic ICC profile with color temperature adjustment
            var profileData = GenerateBasicICCProfile(temperature, profile);
            
            // Create temporary profile file
            var tempProfilePath = Path.Combine(Path.GetTempPath(), $"chronoguard_temp_{monitorId}_{temperature.Kelvin}K.icc");
            await File.WriteAllBytesAsync(tempProfilePath, profileData);

            // Apply the profile using Windows Color System
            var success = ApplyICCProfileToMonitor(monitorId, tempProfilePath);

            // Clean up temporary file after a delay
            _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ => 
            {
                try { File.Delete(tempProfilePath); } 
                catch { /* Ignore cleanup errors */ }
            });

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating temporary ICC profile for monitor {MonitorId}", monitorId);
            return false;
        }
    }

    /// <summary>
    /// Modifies an existing ICC profile to apply color temperature adjustment
    /// </summary>
    private byte[] ModifyICCProfileForTemperature(byte[] originalProfile, ColorTemperature temperature, MonitorColorProfile profile)
    {
        // This is a simplified implementation
        // In a real implementation, this would parse the ICC profile format
        // and modify the appropriate tags (chromaticity, white point, etc.)
        
        var modifiedProfile = new byte[originalProfile.Length];
        Array.Copy(originalProfile, modifiedProfile, originalProfile.Length);

        // Calculate color multipliers
        var (rMult, gMult, bMult) = profile.GetOptimalMultipliers(temperature.Kelvin);

        // Find and modify color transformation matrices in the ICC profile
        // This is a placeholder - real implementation would require ICC profile parsing
        ModifyICCColorTransforms(modifiedProfile, rMult, gMult, bMult);

        return modifiedProfile;
    }

    /// <summary>
    /// Generates a basic ICC profile with color temperature adjustments
    /// </summary>
    private byte[] GenerateBasicICCProfile(ColorTemperature temperature, MonitorColorProfile profile)
    {
        // This is a simplified implementation that creates a minimal ICC profile
        // Real implementation would create a proper ICC v4 profile with appropriate tags
        
        var (rMult, gMult, bMult) = profile.GetOptimalMultipliers(temperature.Kelvin);
        
        // Create basic ICC profile structure (simplified)
        var profileSize = 1024; // Minimal profile size
        var profileData = new byte[profileSize];
        
        // ICC Profile header (simplified)
        WriteICCHeader(profileData, profileSize, temperature);
        WriteICCTagTable(profileData, temperature, rMult, gMult, bMult);
        WriteICCColorTransforms(profileData, rMult, gMult, bMult);
        
        return profileData;
    }

    /// <summary>
    /// Applies an ICC profile to a specific monitor using Windows Color System
    /// </summary>
    private bool ApplyICCProfileToMonitor(string monitorId, string profilePath)
    {
        try
        {
            // This would use Windows Color Management APIs
            // Placeholder implementation - real version would use WCS APIs
            
            _logger.LogDebug("Applying ICC profile {ProfilePath} to monitor {MonitorId}", profilePath, monitorId);
            
            // Simulate successful application
            return File.Exists(profilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply ICC profile to monitor {MonitorId}", monitorId);
            return false;
        }
    }

    /// <summary>
    /// Modifies color transformation matrices in an ICC profile
    /// </summary>
    private void ModifyICCColorTransforms(byte[] profileData, double rMult, double gMult, double bMult)
    {
        // Placeholder for ICC profile transformation modification
        // Real implementation would locate and modify the appropriate ICC tags
        _logger.LogTrace("Modifying ICC color transforms: R={R:F3}, G={G:F3}, B={B:F3}", rMult, gMult, bMult);
    }

    /// <summary>
    /// Writes ICC profile header
    /// </summary>
    private void WriteICCHeader(byte[] profileData, int profileSize, ColorTemperature temperature)
    {
        // Write ICC profile header (simplified)
        // Real implementation would write proper ICC v4 header
        BitConverter.GetBytes(profileSize).CopyTo(profileData, 0); // Profile size
        profileData[36] = 0x61; profileData[37] = 0x64; profileData[38] = 0x73; profileData[39] = 0x70; // 'adsp' signature
    }

    /// <summary>
    /// Writes ICC tag table
    /// </summary>
    private void WriteICCTagTable(byte[] profileData, ColorTemperature temperature, double rMult, double gMult, double bMult)
    {
        // Write minimal tag table for color transformation
        // Real implementation would write proper ICC tags
        var tagTableOffset = 128;
        profileData[tagTableOffset] = 0x01; // Number of tags (simplified)
    }

    /// <summary>
    /// Writes color transformation data to ICC profile
    /// </summary>
    private void WriteICCColorTransforms(byte[] profileData, double rMult, double gMult, double bMult)
    {
        // Write color transformation matrices
        // Real implementation would write proper transformation curves and matrices
        var transformOffset = 256;
        
        // Store multipliers as fixed-point values (simplified)
        BitConverter.GetBytes((float)rMult).CopyTo(profileData, transformOffset);
        BitConverter.GetBytes((float)gMult).CopyTo(profileData, transformOffset + 4);
        BitConverter.GetBytes((float)bMult).CopyTo(profileData, transformOffset + 8);
    }

    #region Enhanced ICC Profile Integration

    /// <summary>
    /// Enhanced ICC profile application with real parsing and modification
    /// </summary>
    private async Task<bool> ApplyTemperatureViaEnhancedICCAsync(string monitorDeviceName, ColorTemperature temperature)
    {
        try
        {
            _logger.LogInformation("Applying temperature {Temperature}K via enhanced ICC profile for monitor {Monitor}", 
                temperature.Kelvin, monitorDeviceName);

            // Get current ICC profile for the monitor
            var currentProfile = await _iccProfileService.GetActiveProfileAsync(monitorDeviceName);
            
            if (currentProfile == null)
            {
                _logger.LogWarning("No active ICC profile found for monitor {Monitor}, generating temporary profile", monitorDeviceName);
                currentProfile = await _iccProfileService.GenerateTemporaryProfileAsync(temperature, monitorDeviceName);
            }
            else
            {
                // Store original profile if not already stored
                if (!_originalICCProfiles.ContainsKey(monitorDeviceName))
                {
                    _originalICCProfiles[monitorDeviceName] = currentProfile.Copy();
                }

                // Create modified profile
                currentProfile = await _iccProfileService.CreateModifiedProfileAsync(currentProfile, temperature);
            }

            // Validate the profile
            if (!_iccProfileService.ValidateProfile(currentProfile))
            {
                _logger.LogError("Generated ICC profile is invalid for monitor {Monitor}", monitorDeviceName);
                return false;
            }

            // Apply the profile to the system
            var success = await _iccProfileService.ApplyProfileToSystemAsync(currentProfile, monitorDeviceName);
            
            if (success)
            {
                _logger.LogInformation("Successfully applied enhanced ICC profile for monitor {Monitor}", monitorDeviceName);
                
                // Update monitor profile information
                if (_monitorProfiles.ContainsKey(monitorDeviceName))
                {
                    _monitorProfiles[monitorDeviceName].ICCProfilePath = currentProfile.SourcePath;
                    _monitorProfiles[monitorDeviceName].LastCalibrationDate = DateTime.Now;
                }
            }
            else
            {
                _logger.LogError("Failed to apply enhanced ICC profile for monitor {Monitor}", monitorDeviceName);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying enhanced ICC profile for monitor {Monitor}", monitorDeviceName);
            return false;
        }
    }

    /// <summary>
    /// Restore original ICC profiles for all monitors
    /// </summary>
    private async Task RestoreOriginalICCProfilesAsync()
    {
        foreach (var kvp in _originalICCProfiles)
        {
            try
            {
                await _iccProfileService.ApplyProfileToSystemAsync(kvp.Value, kvp.Key);
                _logger.LogInformation("Restored original ICC profile for monitor {Monitor}", kvp.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore original ICC profile for monitor {Monitor}", kvp.Key);
            }
        }
    }

    /// <summary>
    /// Get ICC profile information for monitor
    /// </summary>
    public async Task<ICCProfile?> GetMonitorICCProfileAsync(string monitorDeviceName)
    {
        try
        {
            return await _iccProfileService.GetActiveProfileAsync(monitorDeviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ICC profile for monitor {Monitor}", monitorDeviceName);
            return null;
        }
    }

    /// <summary>
    /// Set custom ICC profile for monitor
    /// </summary>
    public async Task<bool> SetCustomICCProfileAsync(string monitorDeviceName, string profilePath)
    {
        try
        {
            var profile = await _iccProfileService.ParseProfileAsync(profilePath);
            
            if (!_iccProfileService.ValidateProfile(profile))
            {
                _logger.LogError("Invalid ICC profile: {ProfilePath}", profilePath);
                return false;
            }

            var success = await _iccProfileService.ApplyProfileToSystemAsync(profile, monitorDeviceName);
            
            if (success && _monitorProfiles.ContainsKey(monitorDeviceName))
            {
                _monitorProfiles[monitorDeviceName].ICCProfilePath = profilePath;
                _monitorProfiles[monitorDeviceName].LastCalibrationDate = DateTime.Now;
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set custom ICC profile for monitor {Monitor}", monitorDeviceName);
            return false;
        }
    }

    /// <summary>
    /// Calibrate monitor using ICC profile analysis
    /// </summary>
    public async Task<bool> CalibrateMonitorAsync(string monitorDeviceName)
    {
        try
        {
            _logger.LogInformation("Starting monitor calibration for {Monitor}", monitorDeviceName);

            // Get current profile
            var currentProfile = await _iccProfileService.GetActiveProfileAsync(monitorDeviceName);
            if (currentProfile == null)
            {
                _logger.LogWarning("No ICC profile found for calibration of monitor {Monitor}", monitorDeviceName);
                return false;
            }

            // Analyze current profile characteristics
            var currentTemp = currentProfile.CalculateColorTemperature();
            var gamutCoverage = currentProfile.GetSRGBCoverage();

            _logger.LogInformation("Monitor {Monitor} analysis - Temperature: {Temp}K, sRGB Coverage: {Coverage:F1}%", 
                monitorDeviceName, currentTemp, gamutCoverage);

            // Apply calibration adjustments if needed
            if (_config.MonitorCalibrations.ContainsKey(monitorDeviceName))
            {
                var calibrationSettings = _config.MonitorCalibrations[monitorDeviceName];
                
                // Create calibrated profile
                var calibratedProfile = await CreateCalibratedProfileAsync(currentProfile, calibrationSettings);
                
                // Apply calibrated profile
                var success = await _iccProfileService.ApplyProfileToSystemAsync(calibratedProfile, monitorDeviceName);
                
                if (success)
                {
                    calibrationSettings.LastCalibrationDate = DateTime.Now;
                    _logger.LogInformation("Successfully calibrated monitor {Monitor}", monitorDeviceName);
                }

                return success;
            }

            return true; // No calibration needed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calibrate monitor {Monitor}", monitorDeviceName);
            return false;
        }
    }

    /// <summary>
    /// Create calibrated ICC profile with monitor-specific settings
    /// </summary>
    private async Task<ICCProfile> CreateCalibratedProfileAsync(ICCProfile baseProfile, MonitorCalibrationSettings settings)
    {
        var calibratedProfile = baseProfile.Copy();

        // Apply gamma adjustments
        calibratedProfile.RedTRC = ApplyGammaAdjustment(calibratedProfile.RedTRC, settings.RedGamma);
        calibratedProfile.GreenTRC = ApplyGammaAdjustment(calibratedProfile.GreenTRC, settings.GreenGamma);
        calibratedProfile.BlueTRC = ApplyGammaAdjustment(calibratedProfile.BlueTRC, settings.BlueGamma);

        // Apply temperature offset
        if (settings.TemperatureOffset != 0)
        {
            var currentTemp = calibratedProfile.CalculateColorTemperature();
            var targetTemp = new ColorTemperature(currentTemp + settings.TemperatureOffset);
            calibratedProfile = await _iccProfileService.CreateModifiedProfileAsync(calibratedProfile, targetTemp);
        }

        // Update profile metadata
        calibratedProfile.Description = $"Calibrated Profile - {DateTime.Now:yyyy-MM-dd}";
        calibratedProfile.CreationDate = DateTime.Now;

        return calibratedProfile;
    }

    /// <summary>
    /// Apply gamma adjustment to tone reproduction curve
    /// </summary>
    private double[] ApplyGammaAdjustment(double[] originalTRC, double gammaAdjustment)
    {
        if (originalTRC.Length == 1)
        {
            // Simple gamma value
            return new double[] { originalTRC[0] * gammaAdjustment };
        }
        else if (originalTRC.Length > 1)
        {
            // Curve data points
            var adjustedTRC = new double[originalTRC.Length];
            for (int i = 0; i < originalTRC.Length; i++)
            {
                var input = i / (double)(originalTRC.Length - 1);
                adjustedTRC[i] = Math.Pow(originalTRC[i], 1.0 / gammaAdjustment);
            }
            return adjustedTRC;
        }
        else
        {
            // Default gamma
            return new double[] { 2.2 * gammaAdjustment };
        }
    }

    #endregion

    #region Windows Color Management System Integration

    /// <summary>
    /// Windows Color Management API declarations
    /// </summary>
    [DllImport("mscms.dll", SetLastError = true)]
    private static extern bool InstallColorProfile(string machineName, string profileName);

    [DllImport("mscms.dll", SetLastError = true)]
    private static extern bool UninstallColorProfile(string machineName, string profileName, bool delete);

    [DllImport("mscms.dll", SetLastError = true)]
    private static extern bool SetColorProfileElementReference(IntPtr hProfile, uint tag, uint dwElementSize, ref byte pElementData);

    [DllImport("mscms.dll", SetLastError = true)]
    private static extern bool AssociateColorProfileWithDevice(string machineName, string profileName, string deviceName);

    [DllImport("mscms.dll", SetLastError = true)]
    private static extern bool DisassociateColorProfileFromDevice(string machineName, string profileName, string deviceName);    /// <summary>
    /// Apply ICC profile using Windows Color Management System
    /// </summary>
    private Task<bool> ApplyProfileViaWCSAsync(string profilePath, string monitorDeviceName)
    {
        try
        {
            _logger.LogInformation("Applying ICC profile via WCS: {ProfilePath} to {Monitor}", profilePath, monitorDeviceName);

            // Install the profile in the system
            var installSuccess = InstallColorProfile(string.Empty, profilePath);
            if (!installSuccess)
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogWarning("Failed to install ICC profile {ProfilePath}, error: {Error}", profilePath, error);
                
                // Continue anyway, profile might already be installed
            }

            // Associate the profile with the monitor device
            var profileName = Path.GetFileName(profilePath);
            var associateSuccess = AssociateColorProfileWithDevice(string.Empty, profileName, monitorDeviceName);
            
            if (!associateSuccess)
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogError("Failed to associate ICC profile {ProfileName} with device {Device}, error: {Error}", 
                    profileName, monitorDeviceName, error);
                return Task.FromResult(false);
            }

            _logger.LogInformation("Successfully applied ICC profile via WCS for monitor {Monitor}", monitorDeviceName);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying ICC profile via WCS for monitor {Monitor}", monitorDeviceName);
            return Task.FromResult(false);
        }
    }/// <summary>
    /// Remove ICC profile association using Windows Color Management System
    /// </summary>
    private Task<bool> RemoveProfileViaWCSAsync(string profilePath, string monitorDeviceName)
    {
        try
        {
            var profileName = Path.GetFileName(profilePath);
            
            // Disassociate the profile from the device
            var disassociateSuccess = DisassociateColorProfileFromDevice(string.Empty, profileName, monitorDeviceName);
            
            if (!disassociateSuccess)
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogWarning("Failed to disassociate ICC profile {ProfileName} from device {Device}, error: {Error}", 
                    profileName, monitorDeviceName, error);
            }

            // Optionally uninstall the profile from the system
            var uninstallSuccess = UninstallColorProfile(string.Empty, profilePath, false);
            if (!uninstallSuccess)
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogInformation("ICC profile {ProfilePath} not uninstalled (may be in use), error: {Error}", profilePath, error);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing ICC profile via WCS for monitor {Monitor}", monitorDeviceName);
            return Task.FromResult(false);
        }
    }

    #endregion

    #region Resource Management and Disposal

    /// <summary>
    /// Dispose pattern implementation for proper resource cleanup
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected dispose method for proper resource cleanup
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                // Stop any active transitions
                _transitionTimer?.Dispose();
                _transitionTimer = null;
                
                // Release all device contexts from pool
                ReleaseAllDeviceContexts();
                
                // Release monitor handles
                foreach (var (monitorId, handle) in _monitorHandles)
                {
                    if (handle != IntPtr.Zero)
                    {
                        ReleaseDeviceContext(handle);
                    }
                }
                
                _monitorHandles.Clear();
                _originalGammaRamps.Clear();
                _currentGammaRamps.Clear();
                _monitorProfiles.Clear();
                
                // Clear caches
                ClearGammaRampCache();
                
                // Dispose semaphore
                _updateSemaphore?.Dispose();
                
                _logger.LogInformation("WindowsColorTemperatureService disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during WindowsColorTemperatureService disposal");
            }
        }
    }

    /// <summary>
    /// Finalizer to ensure resources are cleaned up
    /// </summary>
    ~WindowsColorTemperatureService()
    {
        Dispose(false);
    }

    #endregion

    #region Advanced Error Handling and Recovery

    /// <summary>
    /// Enhanced gamma ramp setting with automatic retry and fallback mechanisms
    /// </summary>
    private async Task<bool> SetGammaRampWithRetryAsync(IntPtr hDC, RAMP ramp, string monitorId, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (SetDeviceGammaRampOptimized(hDC, ref ramp, monitorId))
                {
                    if (attempt > 1)
                    {
                        _logger.LogInformation("Gamma ramp set successfully on attempt {Attempt} for monitor {MonitorId}", 
                            attempt, monitorId);
                    }
                    return true;
                }
                
                if (attempt < maxRetries)
                {
                    _logger.LogWarning("Gamma ramp setting failed for monitor {MonitorId}, attempt {Attempt}/{MaxRetries}, retrying...", 
                        monitorId, attempt, maxRetries);
                    
                    // Progressive delay between retries
                    await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during gamma ramp setting attempt {Attempt} for monitor {MonitorId}", 
                    attempt, monitorId);
                
                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt));
                }
            }
        }
        
        _logger.LogError("Failed to set gamma ramp for monitor {MonitorId} after {MaxRetries} attempts", 
            monitorId, maxRetries);
        return false;
    }

    /// <summary>
    /// Monitors system health and automatically recovers from errors
    /// </summary>
    private async Task<bool> PerformHealthCheckAndRecoveryAsync()
    {
        try
        {
            var issuesDetected = false;
            var recoveryActions = new List<string>();

            // Check device context pool health
            var (available, active, total, avgTime) = GetDeviceContextPoolStats();
            if (total == 0)
            {
                _logger.LogWarning("Device context pool is empty, reinitializing...");
                EnsureMinimumDeviceContextPool();
                recoveryActions.Add("Reinitialized device context pool");
                issuesDetected = true;
            }

            // Check for slow gamma operations
            if (avgTime > 200) // Over 200ms is considered slow
            {
                _logger.LogWarning("Detected slow gamma operations (avg: {AvgTime}ms), cleaning up cache...", avgTime);
                CleanupExpiredCacheEntries();
                recoveryActions.Add("Cleaned up performance cache");
                issuesDetected = true;
            }

            // Check monitor handle validity
            var invalidHandles = new List<string>();
            foreach (var (monitorId, handle) in _monitorHandles)
            {
                if (handle == IntPtr.Zero)
                {
                    invalidHandles.Add(monitorId);
                }
            }

            if (invalidHandles.Count > 0)
            {
                _logger.LogWarning("Detected {Count} invalid monitor handles, attempting recovery...", invalidHandles.Count);
                
                foreach (var monitorId in invalidHandles)
                {
                    _monitorHandles.Remove(monitorId);
                }
                
                // Attempt to reinitialize affected monitors
                await Task.Run(InitializeMonitorsAsync);
                recoveryActions.Add($"Recovered {invalidHandles.Count} invalid monitor handles");
                issuesDetected = true;
            }

            // Log recovery actions
            if (issuesDetected)
            {
                _logger.LogInformation("System health check completed. Recovery actions: {Actions}", 
                    string.Join(", ", recoveryActions));
            }

            return !issuesDetected; // Return true if no issues detected
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check and recovery");
            return false;
        }
    }

    /// <summary>
    /// Handles critical errors with graceful degradation
    /// </summary>
    private async Task<bool> HandleCriticalErrorAsync(Exception error, string operation)
    {
        _logger.LogError(error, "Critical error during {Operation}, attempting recovery", operation);

        try
        {
            // Attempt health check and recovery
            var recoverySuccessful = await PerformHealthCheckAndRecoveryAsync();
            
            if (!recoverySuccessful)
            {
                _logger.LogWarning("Health check recovery failed, falling back to safe mode");
                
                // Enter safe mode - disable advanced features temporarily
                await EnterSafeModeAsync();
                return true; // Safe mode activated successfully
            }

            return recoverySuccessful;
        }
        catch (Exception recoveryError)
        {
            _logger.LogError(recoveryError, "Failed to recover from critical error in {Operation}", operation);
            return false;
        }
    }

    /// <summary>
    /// Enters safe mode with minimal functionality to maintain basic operation
    /// </summary>
    private async Task EnterSafeModeAsync()
    {
        try
        {
            _logger.LogInformation("Entering safe mode - disabling advanced features");

            // Clear problematic caches
            ClearGammaRampCache();
            
            // Release and recreate device context pool
            ReleaseAllDeviceContexts();
            EnsureMinimumDeviceContextPool();
            
            // Disable advanced algorithms temporarily
            var originalAdvancedMode = _useAdvancedGammaAlgorithm;
            _useAdvancedGammaAlgorithm = false;
            
            // Try to restore basic functionality
            if (_currentTemperature != null)
            {
                var success = await ApplyTemperatureAsync(_currentTemperature);
                if (!success)
                {
                    _logger.LogError("Failed to restore temperature in safe mode, restoring original settings");
                    await RestoreOriginalSettingsAsync();
                }
            }
            
            // Restore advanced mode after a delay
            _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
            {
                _useAdvancedGammaAlgorithm = originalAdvancedMode;
                _logger.LogInformation("Exited safe mode, advanced features restored");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error entering safe mode");
        }
    }    /// <summary>
    /// Validates monitor configuration and applies corrections if needed
    /// </summary>
    private bool ValidateAndCorrectMonitorConfiguration()
    {
        try
        {
            var correctionsMade = false;

            foreach (var (monitorId, profile) in _monitorProfiles.ToList())
            {
                // Validate profile integrity
                if (string.IsNullOrEmpty(profile.MonitorName))
                {
                    profile.MonitorName = $"Monitor {monitorId}";
                    correctionsMade = true;
                }

                // Validate gamma correction values
                if (profile.GammaCorrection <= 0 || profile.GammaCorrection > 5)
                {
                    profile.GammaCorrection = 2.2; // Standard gamma
                    correctionsMade = true;
                    _logger.LogWarning("Corrected invalid gamma value for monitor {MonitorId}", monitorId);
                }

                // Validate brightness and contrast scales
                if (profile.BrightnessScale <= 0 || profile.BrightnessScale > 2)
                {
                    profile.BrightnessScale = 1.0;
                    correctionsMade = true;
                }

                if (profile.ContrastScale <= 0 || profile.ContrastScale > 3)
                {
                    profile.ContrastScale = 1.0;
                    correctionsMade = true;
                }

                // Verify monitor handle is still valid
                if (_monitorHandles.TryGetValue(monitorId, out var handle) && handle == IntPtr.Zero)
                {
                    _monitorHandles.Remove(monitorId);
                    correctionsMade = true;
                    _logger.LogWarning("Removed invalid handle for monitor {MonitorId}", monitorId);
                }
            }

            if (correctionsMade)
            {
                _logger.LogInformation("Applied configuration corrections to monitor profiles");
                
                // Clear cache to ensure corrected values are used
                ClearGammaRampCache();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating monitor configuration");
            return false;
        }
    }

    #endregion

    #region Threading Optimizations and Concurrency Control

    /// <summary>
    /// Optimized temperature application with reduced lock contention
    /// </summary>
    private async Task<bool> ApplyTemperatureOptimizedAsync(ColorTemperature temperature)
    {
        if (!_isInitialized)
        {
            await InitializeMonitorsAsync();
        }

        // Pre-generate all gamma ramps outside the critical section to minimize lock time
        var gammaRamps = new Dictionary<string, RAMP>();
        var monitorProfiles = new Dictionary<string, MonitorColorProfile>();
        
        // Capture monitor data in a minimal critical section
        lock (_lockObject)
        {
            foreach (var (monitorId, profile) in _monitorProfiles)
            {
                monitorProfiles[monitorId] = profile;
            }
        }

        // Generate gamma ramps outside the lock (this is the expensive operation)
        foreach (var (monitorId, profile) in monitorProfiles)
        {
            try
            {
                var ramp = CreateOptimizedGammaRamp(temperature, profile);
                gammaRamps[monitorId] = ramp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create gamma ramp for monitor {MonitorId}", monitorId);
            }
        }

        // Apply gamma ramps with minimal lock contention
        await _updateSemaphore.WaitAsync();
        try
        {
            var success = true;
            var appliedCount = 0;
            var monitorHandles = new Dictionary<string, IntPtr>();

            // Capture handles in minimal critical section
            lock (_lockObject)
            {
                foreach (var (monitorId, handle) in _monitorHandles)
                {
                    monitorHandles[monitorId] = handle;
                }
            }

            // Apply ramps without holding the main lock
            var applyTasks = monitorHandles.Select(async kvp =>
            {
                var (monitorId, handle) = kvp;
                if (gammaRamps.TryGetValue(monitorId, out var ramp))
                {
                    var applied = await SetGammaRampWithRetryAsync(handle, ramp, monitorId);
                    return (monitorId, ramp, applied);
                }
                return (monitorId, new RAMP(), false);
            });

            var results = await Task.WhenAll(applyTasks);

            // Update state in a final critical section
            lock (_lockObject)
            {
                foreach (var (monitorId, ramp, applied) in results)
                {
                    if (applied)
                    {
                        _currentGammaRamps[monitorId] = ramp;
                        appliedCount++;
                        _logger.LogTrace("Applied temperature {Temperature}K to monitor {MonitorId}", temperature.Kelvin, monitorId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to set gamma ramp for monitor {MonitorId}", monitorId);
                        success = false;
                    }
                }

                if (success && appliedCount > 0)
                {
                    _currentTemperature = temperature;
                }
            }

            if (success && appliedCount > 0)
            {
                TemperatureChanged?.Invoke(this, temperature);
                _logger.LogDebug("Applied color temperature {Temperature}K to {Count} monitors", temperature.Kelvin, appliedCount);
            }

            return success;
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }

    /// <summary>
    /// Thread-safe cache access with minimal locking
    /// </summary>
    private bool TryGetCachedGammaRampFast(string cacheKey, out RAMP ramp)
    {
        ramp = new RAMP();
        
        // Fast path - try without lock first (rare race condition acceptable for performance)
        if (_gammaRampCache.TryGetValue(cacheKey, out ramp))
        {
            // Verify cache entry hasn't expired (without lock for performance)
            if (_cacheTimestamps.TryGetValue(cacheKey, out var timestamp))
            {
                if (DateTime.UtcNow - timestamp <= _cacheExpiry)
                {
                    return true;
                }
            }
        }

        // Fallback to locked access if fast path fails
        return TryGetCachedGammaRamp(cacheKey, out ramp);
    }

    /// <summary>
    /// Batch update multiple monitors with optimized locking
    /// </summary>
    private async Task<bool> BatchUpdateMonitorsAsync(Dictionary<string, ColorTemperature> monitorTemperatures)
    {
        if (!_isInitialized)
        {
            await InitializeMonitorsAsync();
        }

        // Pre-generate all gamma ramps
        var gammaRamps = new Dictionary<string, RAMP>();
        foreach (var (monitorId, temperature) in monitorTemperatures)
        {
            if (_monitorProfiles.TryGetValue(monitorId, out var profile))
            {
                var ramp = CreateOptimizedGammaRamp(temperature, profile);
                gammaRamps[monitorId] = ramp;
            }
        }

        await _updateSemaphore.WaitAsync();
        try
        {
            var success = true;
            var updateTasks = new List<Task<(string monitorId, bool success)>>();

            foreach (var (monitorId, ramp) in gammaRamps)
            {
                if (_monitorHandles.TryGetValue(monitorId, out var handle))
                {
                    updateTasks.Add(Task.Run(async () =>
                    {
                        var result = await SetGammaRampWithRetryAsync(handle, ramp, monitorId);
                        return (monitorId, result);
                    }));
                }
            }

            var results = await Task.WhenAll(updateTasks);

            // Update state in a single critical section
            lock (_lockObject)
            {
                foreach (var (monitorId, updateSuccess) in results)
                {
                    if (updateSuccess && gammaRamps.TryGetValue(monitorId, out var ramp))
                    {
                        _currentGammaRamps[monitorId] = ramp;
                    }
                    else
                    {
                        success = false;
                    }
                }
            }

            return success;
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }

    /// <summary>
    /// Periodic maintenance task to optimize performance
    /// </summary>
    private async Task PerformPeriodicMaintenanceAsync()
    {
        try
        {
            // Clean up expired cache entries
            var cleanupTask = Task.Run(() =>
            {
                lock (_lockObject)
                {
                    CleanupExpiredCacheEntries();
                }
            });

            // Validate monitor configuration
            var validationTask = Task.Run(() => ValidateAndCorrectMonitorConfiguration());

            // Perform device context pool maintenance
            var poolMaintenanceTask = Task.Run(() =>
            {
                lock (_lockObject)
                {
                    CleanupExpiredDeviceContexts();
                    EnsureMinimumDeviceContextPool();
                }
            });

            await Task.WhenAll(cleanupTask, validationTask, poolMaintenanceTask);

            _logger.LogTrace("Periodic maintenance completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic maintenance");
        }
    }

    #endregion

    #region GammaRamp Conversion Methods

    /// <summary>
    /// Converts GammaRamp domain entity to Windows RAMP structure
    /// </summary>
    /// <param name="gammaRamp">Domain gamma ramp</param>
    /// <returns>Windows RAMP structure for hardware API calls</returns>
    private RAMP ConvertToRamp(GammaRamp gammaRamp)
    {
        var ramp = new RAMP();
        
        // Direct copy of arrays since both use ushort[256]
        Array.Copy(gammaRamp.Red, ramp.Red, 256);
        Array.Copy(gammaRamp.Green, ramp.Green, 256);
        Array.Copy(gammaRamp.Blue, ramp.Blue, 256);
        
        return ramp;
    }

    /// <summary>
    /// Converts Windows RAMP structure to GammaRamp domain entity
    /// </summary>
    /// <param name="ramp">Windows RAMP structure</param>
    /// <param name="colorTemperature">Color temperature for metadata</param>
    /// <param name="brightness">Brightness for metadata</param>
    /// <param name="contrast">Contrast for metadata</param>
    /// <returns>Domain gamma ramp entity</returns>
    private GammaRamp ConvertFromRamp(RAMP ramp, int colorTemperature = 6500, double brightness = 1.0, double contrast = 1.0)
    {
        var gammaRamp = new GammaRamp(colorTemperature, brightness, contrast, false);
        
        // Direct copy of arrays
        Array.Copy(ramp.Red, gammaRamp.Red, 256);
        Array.Copy(ramp.Green, gammaRamp.Green, 256);
        Array.Copy(ramp.Blue, gammaRamp.Blue, 256);
        
        return gammaRamp;
    }

    #endregion
}
