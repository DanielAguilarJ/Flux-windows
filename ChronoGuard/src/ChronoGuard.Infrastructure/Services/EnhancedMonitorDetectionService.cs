using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace ChronoGuard.Infrastructure.Services;

/// <summary>
/// Enhanced monitor detection service with WMI queries and EDID parsing
/// Provides comprehensive monitor information including manufacturer, model, capabilities, and resolution
/// </summary>
public class EnhancedMonitorDetectionService : IMonitorDetectionService
{
    private readonly ILogger<EnhancedMonitorDetectionService> _logger;
    private readonly Dictionary<string, MonitorHardwareInfo> _cachedMonitorInfo = new();
    private readonly SemaphoreSlim _detectionSemaphore = new(1, 1);
    
    public EnhancedMonitorDetectionService(ILogger<EnhancedMonitorDetectionService> logger)
    {
        _logger = logger;
    }

    #region Windows API Declarations

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("dxva2.dll")]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll")]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll")]
    private static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll")]
    private static extern bool GetCapabilitiesStringLength(IntPtr hPhysicalMonitor, ref uint pdwCapabilitiesStringLengthInCharacters);

    [DllImport("dxva2.dll")]
    private static extern bool CapabilitiesRequestAndCapabilitiesReply(IntPtr hPhysicalMonitor, StringBuilder pszASCIICapabilitiesString, uint dwCapabilitiesStringLengthInCharacters);

    private delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    #endregion

    #region Structures

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DISPLAY_DEVICE
    {
        [MarshalAs(UnmanagedType.U4)]
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        [MarshalAs(UnmanagedType.U4)]
        public DisplayDeviceStateFlags StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [Flags]
    private enum DisplayDeviceStateFlags : uint
    {
        AttachedToDesktop = 0x1,
        MultiDriver = 0x2,
        PrimaryDevice = 0x4,
        MirroringDriver = 0x8,
        VGACompatible = 0x10,
        Removable = 0x20,
        ModesPruned = 0x8000000,
        Remote = 0x4000000,
        Disconnect = 0x2000000
    }

    #endregion

    /// <summary>
    /// Detect all monitors with comprehensive information using WMI and Windows APIs
    /// </summary>
    public async Task<IEnumerable<MonitorHardwareInfo>> DetectMonitorsAsync()
    {
        await _detectionSemaphore.WaitAsync();
        try
        {
            _logger.LogInformation("Starting comprehensive monitor detection");
            
            var monitors = new List<MonitorHardwareInfo>();
            var monitorList = new List<(IntPtr monitor, string deviceName, bool isPrimary, RECT bounds)>();

            // Step 1: Enumerate display monitors
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var monitorInfo = new MONITORINFOEX();
                monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);
                
                if (GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    var isPrimary = (monitorInfo.dwFlags & 1) != 0; // MONITORINFOF_PRIMARY
                    var deviceName = monitorInfo.szDevice;
                    monitorList.Add((hMonitor, deviceName, isPrimary, lprcMonitor));
                }
                
                return true;
            }, IntPtr.Zero);

            _logger.LogInformation("Found {Count} display monitors", monitorList.Count);

            // Step 2: Get detailed information for each monitor
            foreach (var (hMonitor, deviceName, isPrimary, bounds) in monitorList)
            {
                try
                {
                    var monitorInfo = await GetDetailedMonitorInfoAsync(hMonitor, deviceName, isPrimary, bounds);
                    if (monitorInfo != null)
                    {
                        monitors.Add(monitorInfo);
                        _cachedMonitorInfo[monitorInfo.DevicePath] = monitorInfo;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get detailed info for monitor {DeviceName}", deviceName);
                }
            }

            _logger.LogInformation("Successfully detected {Count} monitors with detailed information", monitors.Count);
            return monitors;
        }
        finally
        {
            _detectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Get detailed monitor information combining multiple data sources
    /// </summary>
    private async Task<MonitorHardwareInfo?> GetDetailedMonitorInfoAsync(IntPtr hMonitor, string deviceName, bool isPrimary, RECT bounds)
    {
        try
        {
            var monitor = new MonitorHardwareInfo
            {
                DevicePath = deviceName,
                IsPrimary = isPrimary,
                PhysicalWidth = bounds.Right - bounds.Left,
                PhysicalHeight = bounds.Bottom - bounds.Top,
                PositionX = bounds.Left,
                PositionY = bounds.Top
            };

            // Get display device information
            await EnhanceWithDisplayDeviceInfoAsync(monitor, deviceName);

            // Get WMI information
            await EnhanceWithWMIInformationAsync(monitor, deviceName);

            // Get physical monitor information
            await EnhanceWithPhysicalMonitorInfoAsync(monitor, hMonitor);

            // Get EDID information
            await EnhanceWithEDIDInformationAsync(monitor, deviceName);

            // Get current display settings
            await EnhanceWithDisplaySettingsAsync(monitor, deviceName);

            // Detect monitor capabilities
            await DetectMonitorCapabilitiesAsync(monitor, hMonitor);

            return monitor;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get detailed monitor information for {DeviceName}", deviceName);
            return null;
        }
    }

    /// <summary>
    /// Enhance monitor info with display device information
    /// </summary>
    private async Task EnhanceWithDisplayDeviceInfoAsync(MonitorHardwareInfo monitor, string deviceName)
    {
        await Task.Run(() =>
        {
            var displayDevice = new DISPLAY_DEVICE();
            displayDevice.cb = Marshal.SizeOf(displayDevice);

            // Get adapter information
            if (EnumDisplayDevices(null, 0, ref displayDevice, 0))
            {
                monitor.AdapterName = displayDevice.DeviceString;
                monitor.AdapterKey = displayDevice.DeviceKey;

                // Get monitor information
                var monitorDevice = new DISPLAY_DEVICE();
                monitorDevice.cb = Marshal.SizeOf(monitorDevice);
                
                if (EnumDisplayDevices(displayDevice.DeviceName, 0, ref monitorDevice, 0))
                {
                    monitor.MonitorName = monitorDevice.DeviceString;
                    monitor.MonitorKey = monitorDevice.DeviceKey;
                    monitor.MonitorId = monitorDevice.DeviceID;
                }
            }
        });
    }

    /// <summary>
    /// Enhance monitor info with WMI information
    /// </summary>
    private async Task EnhanceWithWMIInformationAsync(MonitorHardwareInfo monitor, string deviceName)
    {
        await Task.Run(() =>
        {
            try
            {
                // Query Win32_DesktopMonitor
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DesktopMonitor");
                using var collection = searcher.Get();
                
                foreach (ManagementObject obj in collection)
                {
                    try
                    {
                        var deviceId = obj["DeviceID"]?.ToString();
                        if (!string.IsNullOrEmpty(deviceId) && deviceName.Contains(deviceId))
                        {
                            monitor.ManufacturerName = obj["MonitorManufacturer"]?.ToString() ?? "Unknown";
                            monitor.ModelName = obj["MonitorType"]?.ToString() ?? "Unknown";
                            
                            if (int.TryParse(obj["ScreenWidth"]?.ToString(), out var width))
                                monitor.NativeWidth = width;
                            if (int.TryParse(obj["ScreenHeight"]?.ToString(), out var height))
                                monitor.NativeHeight = height;
                                
                            monitor.Description = obj["Description"]?.ToString() ?? "";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error processing WMI DesktopMonitor object");
                    }
                }

                // Query Win32_VideoController for additional information
                using var videoSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                using var videoCollection = videoSearcher.Get();
                
                foreach (ManagementObject obj in videoCollection)
                {
                    try
                    {
                        var description = obj["Description"]?.ToString();
                        if (!string.IsNullOrEmpty(description))
                        {
                            monitor.VideoControllerName = description;
                            
                            if (int.TryParse(obj["VideoMemoryType"]?.ToString(), out var memType))
                                monitor.VideoMemoryType = memType;
                                
                            if (long.TryParse(obj["AdapterRAM"]?.ToString(), out var adapterRam))
                                monitor.VideoMemorySize = adapterRam;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error processing WMI VideoController object");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get WMI information for monitor {DeviceName}", deviceName);
            }
        });
    }

    /// <summary>
    /// Enhance monitor info with physical monitor information via DXVA2
    /// </summary>
    private async Task EnhanceWithPhysicalMonitorInfoAsync(MonitorHardwareInfo monitor, IntPtr hMonitor)
    {
        await Task.Run(() =>
        {
            try
            {
                uint numMonitors = 0;
                if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref numMonitors) && numMonitors > 0)
                {
                    var physicalMonitors = new PHYSICAL_MONITOR[numMonitors];
                    if (GetPhysicalMonitorsFromHMONITOR(hMonitor, numMonitors, physicalMonitors))
                    {
                        monitor.PhysicalMonitorDescription = physicalMonitors[0].szPhysicalMonitorDescription;
                        monitor.SupportsDDCCI = true;

                        // Try to get capabilities string
                        var hPhysicalMonitor = physicalMonitors[0].hPhysicalMonitor;
                        uint capLength = 0;
                        
                        if (GetCapabilitiesStringLength(hPhysicalMonitor, ref capLength) && capLength > 0)
                        {
                            var capString = new StringBuilder((int)capLength);
                            if (CapabilitiesRequestAndCapabilitiesReply(hPhysicalMonitor, capString, capLength))
                            {
                                monitor.CapabilitiesString = capString.ToString();
                                ParseCapabilitiesString(monitor, monitor.CapabilitiesString);
                            }
                        }

                        DestroyPhysicalMonitors(numMonitors, physicalMonitors);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to get physical monitor information");
                monitor.SupportsDDCCI = false;
            }
        });
    }

    /// <summary>
    /// Enhance monitor info with EDID information from registry
    /// </summary>
    private async Task EnhanceWithEDIDInformationAsync(MonitorHardwareInfo monitor, string deviceName)
    {
        await Task.Run(() =>
        {
            try
            {
                // EDID information is typically stored in the registry under display device keys
                // This is a simplified approach - real EDID parsing would be more complex
                using var searcher = new ManagementObjectSearcher("SELECT * FROM WmiMonitorID");
                using var collection = searcher.Get();
                
                foreach (ManagementObject obj in collection)
                {
                    try
                    {
                        var manufacturerName = GetStringFromUInt16Array(obj["ManufacturerName"] as ushort[]);
                        var productCodeID = GetStringFromUInt16Array(obj["ProductCodeID"] as ushort[]);
                        var userFriendlyName = GetStringFromUInt16Array(obj["UserFriendlyName"] as ushort[]);
                        
                        if (!string.IsNullOrEmpty(manufacturerName))
                        {
                            monitor.EDIDManufacturer = manufacturerName;
                        }
                        
                        if (!string.IsNullOrEmpty(productCodeID))
                        {
                            monitor.EDIDProductCode = productCodeID;
                        }
                        
                        if (!string.IsNullOrEmpty(userFriendlyName))
                        {
                            monitor.EDIDFriendlyName = userFriendlyName;
                        }

                        // Get year of manufacture
                        if (obj["YearOfManufacture"] is ushort year && year > 0)
                        {
                            monitor.YearOfManufacture = year;
                        }

                        // Get week of manufacture
                        if (obj["WeekOfManufacture"] is byte week)
                        {
                            monitor.WeekOfManufacture = week;
                        }

                        break; // Use first found entry for now
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error processing WMI MonitorID object");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to get EDID information for monitor {DeviceName}", deviceName);
            }
        });
    }

    /// <summary>
    /// Enhance monitor info with current display settings
    /// </summary>
    private async Task EnhanceWithDisplaySettingsAsync(MonitorHardwareInfo monitor, string deviceName)
    {
        await Task.Run(() =>
        {
            try
            {
                var devMode = new DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf(devMode);

                // Get current display settings
                if (EnumDisplaySettings(deviceName, -1, ref devMode)) // ENUM_CURRENT_SETTINGS = -1
                {
                    monitor.CurrentWidth = (int)devMode.dmPelsWidth;
                    monitor.CurrentHeight = (int)devMode.dmPelsHeight;
                    monitor.CurrentRefreshRate = (int)devMode.dmDisplayFrequency;
                    monitor.CurrentBitsPerPixel = (int)devMode.dmBitsPerPel;
                    monitor.CurrentOrientation = devMode.dmDisplayOrientation;
                }

                // Get all available display modes
                var availableModes = new List<DisplayMode>();
                int modeIndex = 0;
                
                while (EnumDisplaySettings(deviceName, modeIndex, ref devMode))
                {
                    availableModes.Add(new DisplayMode
                    {
                        Width = (int)devMode.dmPelsWidth,
                        Height = (int)devMode.dmPelsHeight,
                        RefreshRate = (int)devMode.dmDisplayFrequency,
                        BitsPerPixel = (int)devMode.dmBitsPerPel
                    });
                    modeIndex++;
                }

                monitor.AvailableDisplayModes = availableModes;
                
                // Determine native resolution (highest resolution mode)
                if (availableModes.Any())
                {
                    var nativeMode = availableModes
                        .OrderByDescending(m => m.Width * m.Height)
                        .ThenByDescending(m => m.RefreshRate)
                        .First();
                        
                    if (monitor.NativeWidth == 0) monitor.NativeWidth = nativeMode.Width;
                    if (monitor.NativeHeight == 0) monitor.NativeHeight = nativeMode.Height;
                    monitor.MaxRefreshRate = availableModes.Max(m => m.RefreshRate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get display settings for monitor {DeviceName}", deviceName);
            }
        });
    }

    /// <summary>
    /// Detect advanced monitor capabilities
    /// </summary>
    private async Task DetectMonitorCapabilitiesAsync(MonitorHardwareInfo monitor, IntPtr hMonitor)
    {
        await Task.Run(() =>
        {
            // Detect HDR support (simplified check)
            monitor.SupportsHDR = CheckHDRSupport(monitor);
            
            // Detect wide color gamut support
            monitor.SupportsWideColorGamut = CheckWideColorGamutSupport(monitor);
            
            // Determine color gamut from EDID or capabilities
            monitor.ColorGamut = DetermineColorGamut(monitor);
            
            // Estimate maximum luminance
            monitor.MaxLuminance = EstimateMaxLuminance(monitor);
            
            // Detect if monitor supports ICC profiles
            monitor.SupportsICCProfiles = true; // Most modern monitors support this
            
            // Detect hardware gamma support (most monitors support this)
            monitor.SupportsHardwareGamma = true;
            
            // Set bit depth based on current settings
            monitor.BitDepth = monitor.CurrentBitsPerPixel > 0 ? monitor.CurrentBitsPerPixel : 24;
        });
    }

    /// <summary>
    /// Parse DDC/CI capabilities string to extract features
    /// </summary>
    private void ParseCapabilitiesString(MonitorHardwareInfo monitor, string capabilitiesString)
    {
        if (string.IsNullOrEmpty(capabilitiesString)) return;

        try
        {
            // Parse VCP codes (Virtual Control Panel codes)
            monitor.SupportsBrightnessControl = capabilitiesString.Contains("(10)"); // VCP code 10 = Brightness
            monitor.SupportsContrastControl = capabilitiesString.Contains("(12)"); // VCP code 12 = Contrast
            monitor.SupportsColorTemperatureControl = capabilitiesString.Contains("(14)"); // VCP code 14 = Color Temperature
            
            // Parse supported color temperatures
            if (monitor.SupportsColorTemperatureControl)
            {
                // Extract temperature values (simplified parsing)
                var tempValues = new List<int>();
                var tempSection = ExtractVCPSection(capabilitiesString, "14");
                if (!string.IsNullOrEmpty(tempSection))
                {
                    // Parse hex values in temperature section
                    var tempMatches = System.Text.RegularExpressions.Regex.Matches(tempSection, @"[0-9A-Fa-f]{2}");
                    foreach (System.Text.RegularExpressions.Match match in tempMatches)
                    {
                        if (int.TryParse(match.Value, System.Globalization.NumberStyles.HexNumber, null, out var temp))
                        {
                            tempValues.Add(temp);
                        }
                    }
                }
                monitor.SupportedColorTemperatures = tempValues;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse capabilities string");
        }
    }

    /// <summary>
    /// Extract VCP section from capabilities string
    /// </summary>
    private string ExtractVCPSection(string capabilities, string vcpCode)
    {
        try
        {
            var pattern = $@"\({vcpCode}\s*([^)]*)\)";
            var match = System.Text.RegularExpressions.Regex.Match(capabilities, pattern);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Convert UInt16 array to string (used for EDID data)
    /// </summary>
    private string GetStringFromUInt16Array(ushort[]? array)
    {
        if (array == null || array.Length == 0) return string.Empty;

        try
        {
            var bytes = new byte[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                bytes[i] = (byte)array[i];
            }
            
            return Encoding.ASCII.GetString(bytes).TrimEnd('\0', ' ');
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Check HDR support based on available information
    /// </summary>
    private bool CheckHDRSupport(MonitorHardwareInfo monitor)
    {
        // HDR typically requires:
        // - 10-bit color depth or higher
        // - High maximum luminance (>400 nits)
        // - Wide color gamut
        
        return monitor.BitDepth >= 30 && 
               monitor.MaxLuminance > 400 && 
               (monitor.ColorGamut == "DCI-P3" || monitor.ColorGamut == "Rec. 2020");
    }

    /// <summary>
    /// Check wide color gamut support
    /// </summary>
    private bool CheckWideColorGamutSupport(MonitorHardwareInfo monitor)
    {
        // Check for known wide gamut indicators
        var modelName = monitor.ModelName?.ToLowerInvariant() ?? "";
        var manufacturerName = monitor.ManufacturerName?.ToLowerInvariant() ?? "";
        
        return modelName.Contains("wide") || 
               modelName.Contains("dci") || 
               modelName.Contains("p3") || 
               modelName.Contains("rec") ||
               manufacturerName.Contains("professional") ||
               monitor.BitDepth >= 30;
    }

    /// <summary>
    /// Determine color gamut based on available information
    /// </summary>
    private string DetermineColorGamut(MonitorHardwareInfo monitor)
    {
        var modelName = monitor.ModelName?.ToLowerInvariant() ?? "";
        
        if (modelName.Contains("dci-p3") || modelName.Contains("p3"))
            return "DCI-P3";
        if (modelName.Contains("rec.2020") || modelName.Contains("rec2020"))
            return "Rec. 2020";
        if (modelName.Contains("adobe") || modelName.Contains("wide"))
            return "Adobe RGB";
        if (modelName.Contains("ntsc"))
            return "NTSC";
            
        return "sRGB"; // Default assumption
    }

    /// <summary>
    /// Estimate maximum luminance based on available information
    /// </summary>
    private double EstimateMaxLuminance(MonitorHardwareInfo monitor)
    {
        var modelName = monitor.ModelName?.ToLowerInvariant() ?? "";
        
        // HDR monitors typically have higher luminance
        if (modelName.Contains("hdr"))
        {
            if (modelName.Contains("1000")) return 1000.0;
            if (modelName.Contains("600")) return 600.0;
            if (modelName.Contains("400")) return 400.0;
            return 400.0; // Default HDR assumption
        }
        
        // Professional monitors typically have higher luminance
        if (modelName.Contains("professional") || modelName.Contains("studio"))
            return 350.0;
            
        // Gaming monitors often have higher luminance
        if (modelName.Contains("gaming") || modelName.Contains("curved"))
            return 300.0;
            
        return 250.0; // Standard monitor assumption
    }

    /// <summary>
    /// Get cached monitor information
    /// </summary>
    public MonitorHardwareInfo? GetCachedMonitorInfo(string devicePath)
    {
        return _cachedMonitorInfo.TryGetValue(devicePath, out var info) ? info : null;
    }

    /// <summary>
    /// Clear cached monitor information
    /// </summary>
    public void ClearCache()
    {
        _cachedMonitorInfo.Clear();
    }

    /// <summary>
    /// Get monitor information using WMI for EDID parsing
    /// </summary>
    public async Task<EDIDInfo?> GetMonitorEDIDAsync(string devicePath)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM WmiMonitorDescriptorMethods");
                using var collection = searcher.Get();
                
                foreach (ManagementObject obj in collection)
                {
                    try
                    {
                        // Get EDID descriptor
                        var result = obj.InvokeMethod("WmiGetMonitorRawEEdidV1Block", new object[] { 0 });
                        if (result != null && result["BlockContent"] is byte[] edidData)
                        {
                            return ParseEDID(edidData);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error getting EDID data");
                    }
                }
                
                return null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get EDID information for {DevicePath}", devicePath);
            return null;
        }
    }

    /// <summary>
    /// Parse EDID data to extract monitor information
    /// </summary>
    private EDIDInfo ParseEDID(byte[] edidData)
    {
        if (edidData == null || edidData.Length < 128)
            return new EDIDInfo();

        try
        {
            var edid = new EDIDInfo();
            
            // Manufacturer ID (bytes 8-9)
            var manufacturerBytes = BitConverter.ToUInt16(edidData, 8);
            edid.ManufacturerID = DecodeManufacturerID(manufacturerBytes);
            
            // Product code (bytes 10-11)
            edid.ProductCode = BitConverter.ToUInt16(edidData, 10);
            
            // Serial number (bytes 12-15)
            edid.SerialNumber = BitConverter.ToUInt32(edidData, 12);
            
            // Week of manufacture (byte 16)
            edid.WeekOfManufacture = edidData[16];
            
            // Year of manufacture (byte 17)
            edid.YearOfManufacture = (ushort)(1990 + edidData[17]);
            
            // EDID version (bytes 18-19)
            edid.EDIDVersion = $"{edidData[18]}.{edidData[19]}";
            
            // Parse detailed timing descriptors for native resolution
            for (int i = 54; i < 126; i += 18)
            {
                if (edidData[i] != 0 || edidData[i + 1] != 0) // Valid timing descriptor
                {
                    var pixelClock = BitConverter.ToUInt16(edidData, i) * 10000;
                    if (pixelClock > 0)
                    {
                        edid.HorizontalResolution = ((edidData[i + 4] & 0xF0) << 4) | edidData[i + 2];
                        edid.VerticalResolution = ((edidData[i + 7] & 0xF0) << 4) | edidData[i + 5];
                        break; // Use first valid timing descriptor as native resolution
                    }
                }
            }
            
            return edid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse EDID data");
            return new EDIDInfo();
        }
    }

    /// <summary>
    /// Decode manufacturer ID from EDID
    /// </summary>
    private string DecodeManufacturerID(ushort manufacturerBytes)
    {
        try
        {
            // Manufacturer ID is encoded as 3 5-bit characters
            var char1 = (char)((manufacturerBytes >> 10) + 'A' - 1);
            var char2 = (char)(((manufacturerBytes >> 5) & 0x1F) + 'A' - 1);
            var char3 = (char)((manufacturerBytes & 0x1F) + 'A' - 1);
            
            return $"{char1}{char2}{char3}";
        }
        catch
        {
            return "UNK";
        }
    }
}

/// <summary>
/// Display mode information
/// </summary>
public class DisplayMode
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; }
    public int BitsPerPixel { get; set; }

    public override string ToString()
    {
        return $"{Width}x{Height} @ {RefreshRate}Hz ({BitsPerPixel}-bit)";
    }
}

/// <summary>
/// EDID (Extended Display Identification Data) information
/// </summary>
public class EDIDInfo
{
    public string ManufacturerID { get; set; } = string.Empty;
    public ushort ProductCode { get; set; }
    public uint SerialNumber { get; set; }
    public byte WeekOfManufacture { get; set; }
    public ushort YearOfManufacture { get; set; }
    public string EDIDVersion { get; set; } = string.Empty;
    public int HorizontalResolution { get; set; }
    public int VerticalResolution { get; set; }
}
