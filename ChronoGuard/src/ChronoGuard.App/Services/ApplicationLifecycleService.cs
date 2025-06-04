using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ChronoGuard.Domain.Interfaces;

namespace ChronoGuard.App.Services;

/// <summary>
/// Service for managing application lifecycle events and system state detection
/// </summary>
public class ApplicationLifecycleService : IDisposable
{
    private readonly ILogger<ApplicationLifecycleService> _logger;
    private readonly IConfigurationService _configurationService;
    private ManagementEventWatcher? _sessionWatcher;
    private ManagementEventWatcher? _powerWatcher;
    private bool _disposed = false;

    public event EventHandler<SessionChangeEventArgs>? SessionChanged;
    public event EventHandler<PowerEventArgs>? PowerStateChanged;

    public ApplicationLifecycleService(
        ILogger<ApplicationLifecycleService> logger,
        IConfigurationService configurationService)
    {
        _logger = logger;
        _configurationService = configurationService;
    }

    /// <summary>
    /// Initializes the lifecycle service and starts monitoring system events
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            await StartSystemEventMonitoringAsync();
            _logger.LogInformation("Application lifecycle service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize application lifecycle service");
        }
    }

    /// <summary>
    /// Starts monitoring system events like session changes and power events
    /// </summary>
    private async Task StartSystemEventMonitoringAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                // Monitor session changes (lock/unlock, logon/logoff)
                StartSessionMonitoring();
                
                // Monitor power events (sleep/wake, battery changes)
                StartPowerMonitoring();
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start system event monitoring");
        }
    }

    private void StartSessionMonitoring()
    {
        try
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_SessionChangeEvent");
            _sessionWatcher = new ManagementEventWatcher(query);
            _sessionWatcher.EventArrived += OnSessionChange;
            _sessionWatcher.Start();
            
            _logger.LogDebug("Session monitoring started");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start session monitoring");
        }
    }

    private void StartPowerMonitoring()
    {
        try
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_PowerManagementEvent");
            _powerWatcher = new ManagementEventWatcher(query);
            _powerWatcher.EventArrived += OnPowerChange;
            _powerWatcher.Start();
            
            _logger.LogDebug("Power monitoring started");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start power monitoring");
        }
    }

    private void OnSessionChange(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var sessionChangeType = Convert.ToInt32(e.NewEvent["Type"]);
            var sessionChangeEvent = (SessionChangeType)sessionChangeType;
            
            _logger.LogDebug("Session change detected: {ChangeType}", sessionChangeEvent);
            
            var eventArgs = new SessionChangeEventArgs(sessionChangeEvent);
            SessionChanged?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error handling session change event");
        }
    }

    private void OnPowerChange(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var powerEventType = Convert.ToInt32(e.NewEvent["EventType"]);
            var powerEvent = (PowerEventType)powerEventType;
            
            _logger.LogDebug("Power change detected: {EventType}", powerEvent);
            
            var eventArgs = new PowerEventArgs(powerEvent);
            PowerStateChanged?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error handling power change event");
        }
    }

    /// <summary>
    /// Detects the current execution state of the application
    /// </summary>
    public ExecutionState GetCurrentExecutionState()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            
            // Check if running from installed location
            var exePath = currentProcess.MainModule?.FileName ?? string.Empty;
            var isInstalled = IsInstalledApplication(exePath);
            
            // Check if running with elevated privileges
            var isElevated = IsRunningElevated();
            
            // Check if running in debug mode
            var isDebug = IsDebugMode();
            
            // Check system state
            var sessionState = GetCurrentSessionState();
            var powerState = GetCurrentPowerState();

            return new ExecutionState
            {
                IsInstalled = isInstalled,
                IsElevated = isElevated,
                IsDebugMode = isDebug,
                ExecutablePath = exePath,
                SessionState = sessionState,
                PowerState = powerState,
                StartTime = currentProcess.StartTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine execution state");
            return new ExecutionState();
        }
    }

    private static bool IsInstalledApplication(string exePath)
    {
        if (string.IsNullOrEmpty(exePath))
            return false;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return exePath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase) ||
               exePath.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase) ||
               exePath.StartsWith(Path.Combine(localAppData, "Microsoft\\WindowsApps"), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRunningElevated()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDebugMode()
    {
#if DEBUG
        return true;
#else
        return Debugger.IsAttached;
#endif
    }

    private static SessionState GetCurrentSessionState()
    {
        try
        {
            // Simple check - could be enhanced with more detailed session state detection
            return Environment.UserInteractive ? SessionState.Interactive : SessionState.Service;
        }
        catch
        {
            return SessionState.Unknown;
        }
    }

    private static PowerState GetCurrentPowerState()
    {
        try
        {
            var powerStatus = System.Windows.Forms.SystemInformation.PowerStatus;
            
            if (powerStatus.BatteryChargeStatus.HasFlag(System.Windows.Forms.BatteryChargeStatus.NoSystemBattery))
                return PowerState.AC;
            
            if (powerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online)
                return PowerState.AC;
            
            return PowerState.Battery;
        }
        catch
        {
            return PowerState.Unknown;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _sessionWatcher?.Stop();
            _sessionWatcher?.Dispose();
            
            _powerWatcher?.Stop();
            _powerWatcher?.Dispose();
            
            _logger.LogDebug("Application lifecycle service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing application lifecycle service");
        }

        _disposed = true;
    }
}

/// <summary>
/// Event arguments for session change events
/// </summary>
public class SessionChangeEventArgs : EventArgs
{
    public SessionChangeType ChangeType { get; }
    
    public SessionChangeEventArgs(SessionChangeType changeType)
    {
        ChangeType = changeType;
    }
}

/// <summary>
/// Event arguments for power events
/// </summary>
public class PowerEventArgs : EventArgs
{
    public PowerEventType EventType { get; }
    
    public PowerEventArgs(PowerEventType eventType)
    {
        EventType = eventType;
    }
}

/// <summary>
/// Represents the current execution state of the application
/// </summary>
public class ExecutionState
{
    public bool IsInstalled { get; set; }
    public bool IsElevated { get; set; }
    public bool IsDebugMode { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public SessionState SessionState { get; set; }
    public PowerState PowerState { get; set; }
    public DateTime StartTime { get; set; }
}

/// <summary>
/// Types of session changes that can occur
/// </summary>
public enum SessionChangeType
{
    Unknown = 0,
    SessionLogon = 1,
    SessionLogoff = 2,
    SessionLock = 3,
    SessionUnlock = 4,
    SessionRemoteConnect = 5,
    SessionRemoteDisconnect = 6
}

/// <summary>
/// Types of power events
/// </summary>
public enum PowerEventType
{
    Unknown = 0,
    Suspend = 1,
    Resume = 2,
    BatteryLow = 3,
    PowerSchemeChange = 4
}

/// <summary>
/// Current session state
/// </summary>
public enum SessionState
{
    Unknown,
    Interactive,
    Service,
    RemoteDesktop
}

/// <summary>
/// Current power state
/// </summary>
public enum PowerState
{
    Unknown,
    AC,
    Battery,
    Charging,
    Critical
}
