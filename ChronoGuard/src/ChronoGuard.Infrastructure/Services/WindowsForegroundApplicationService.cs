using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using ChronoGuard.Domain.Interfaces;

namespace ChronoGuard.Infrastructure.Services;

/// <summary>
/// Windows implementation of foreground application detection service
/// </summary>
public class WindowsForegroundApplicationService : IForegroundApplicationService, IDisposable
{
    private readonly ILogger<WindowsForegroundApplicationService> _logger;
    private System.Threading.Timer? _monitoringTimer;
    private string? _lastForegroundApp;
    private bool _isMonitoring;
    private readonly object _lock = new();

    // Windows API imports
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    public event EventHandler<string>? ForegroundApplicationChanged;
    
    public bool IsMonitoring => _isMonitoring;

    public WindowsForegroundApplicationService(ILogger<WindowsForegroundApplicationService> logger)
    {
        _logger = logger;
    }

    public string? GetForegroundApplicationName()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return null;

            GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == 0)
                return null;

            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get foreground application name");
            return null;
        }
    }

    public ForegroundAppInfo? GetForegroundApplicationInfo()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return null;

            GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == 0)
                return null;

            using var process = Process.GetProcessById((int)processId);
            
            // Get window title
            var titleLength = GetWindowTextLength(hwnd);
            var title = new StringBuilder(titleLength + 1);
            GetWindowText(hwnd, title, title.Capacity);

            return new ForegroundAppInfo
            {
                ProcessName = process.ProcessName,
                WindowTitle = title.ToString(),
                ExecutablePath = GetProcessExecutablePath(process),
                ProcessId = (int)processId
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get foreground application info");
            return null;
        }
    }

    public void StartMonitoring()
    {
        lock (_lock)
        {
            if (_isMonitoring)
                return;

            _logger.LogInformation("Starting foreground application monitoring");
            
            // Check every 2 seconds for foreground app changes
            _monitoringTimer = new System.Threading.Timer(CheckForegroundApplication, null, 
                TimeSpan.Zero, TimeSpan.FromSeconds(2));
            
            _isMonitoring = true;
        }
    }

    public void StopMonitoring()
    {
        lock (_lock)
        {
            if (!_isMonitoring)
                return;

            _logger.LogInformation("Stopping foreground application monitoring");
            
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;
            _isMonitoring = false;
        }
    }

    private void CheckForegroundApplication(object? state)
    {
        try
        {
            var currentApp = GetForegroundApplicationName();
            
            if (currentApp != _lastForegroundApp)
            {
                _logger.LogDebug("Foreground application changed from '{LastApp}' to '{CurrentApp}'", 
                    _lastForegroundApp ?? "null", currentApp ?? "null");
                
                _lastForegroundApp = currentApp;
                
                if (!string.IsNullOrEmpty(currentApp))
                {
                    ForegroundApplicationChanged?.Invoke(this, currentApp);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking foreground application");
        }
    }

    private static string GetProcessExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void Dispose()
    {
        StopMonitoring();
        _monitoringTimer?.Dispose();
    }
}
