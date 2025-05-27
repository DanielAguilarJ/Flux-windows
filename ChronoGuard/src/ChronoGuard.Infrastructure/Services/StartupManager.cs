using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Security;
using ChronoGuard.Domain.Interfaces;

namespace ChronoGuard.Infrastructure.Services;

/// <summary>
/// Service for managing Windows startup settings
/// </summary>
public class StartupManager : IStartupManager
{
    private readonly ILogger<StartupManager> _logger;
    private const string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string APP_NAME = "ChronoGuard";

    public StartupManager(ILogger<StartupManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Enables automatic startup with Windows
    /// </summary>
    public bool EnableAutoStart()
    {
        try
        {
            var executablePath = GetExecutablePath();
            if (string.IsNullOrEmpty(executablePath))
            {
                _logger.LogError("Could not determine executable path for auto-start");
                return false;
            }

            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true);
            if (key == null)
            {
                _logger.LogError("Could not open registry key for auto-start");
                return false;
            }

            // Add --minimized argument for startup
            var startupValue = $"\"{executablePath}\" --minimized";
            key.SetValue(APP_NAME, startupValue, RegistryValueKind.String);

            _logger.LogInformation("Auto-start enabled successfully: {Path}", startupValue);
            return true;
        }
        catch (SecurityException ex)
        {
            _logger.LogError(ex, "Security exception while enabling auto-start. Run as administrator may be required.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable auto-start");
            return false;
        }
    }

    /// <summary>
    /// Disables automatic startup with Windows
    /// </summary>
    public bool DisableAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true);
            if (key == null)
            {
                _logger.LogWarning("Could not open registry key for auto-start removal");
                return false;
            }

            // Check if the entry exists before trying to delete it
            var currentValue = key.GetValue(APP_NAME);
            if (currentValue != null)
            {
                key.DeleteValue(APP_NAME, false);
                _logger.LogInformation("Auto-start disabled successfully");
                return true;
            }
            else
            {
                _logger.LogInformation("Auto-start was not enabled");
                return true;
            }
        }
        catch (SecurityException ex)
        {
            _logger.LogError(ex, "Security exception while disabling auto-start. Run as administrator may be required.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable auto-start");
            return false;
        }
    }

    /// <summary>
    /// Checks if automatic startup is currently enabled
    /// </summary>
    public bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, false);
            if (key == null)
            {
                return false;
            }

            var value = key.GetValue(APP_NAME) as string;
            var executablePath = GetExecutablePath();

            // Check if the registry entry exists and points to the correct executable
            if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(executablePath))
            {
                // Remove quotes and arguments for comparison
                var registryPath = value.Trim('"').Split(' ')[0];
                return string.Equals(registryPath, executablePath, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check auto-start status");
            return false;
        }
    }

    /// <summary>
    /// Gets the current executable path
    /// </summary>
    private string GetExecutablePath()
    {
        try
        {
            // Try different methods to get the executable path
            var processPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
            {
                return processPath;
            }

            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(assemblyLocation) && File.Exists(assemblyLocation))
            {
                // For .NET applications, we might need to get the .exe instead of .dll
                var exePath = Path.ChangeExtension(assemblyLocation, ".exe");
                if (File.Exists(exePath))
                {
                    return exePath;
                }
                return assemblyLocation;
            }

            var entryAssembly = Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(entryAssembly) && File.Exists(entryAssembly))
            {
                var exePath = Path.ChangeExtension(entryAssembly, ".exe");
                if (File.Exists(exePath))
                {
                    return exePath;
                }
                return entryAssembly;
            }

            // Fallback to application domain base directory
            var appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ChronoGuard.App.exe");
            if (File.Exists(appPath))
            {
                return appPath;
            }

            _logger.LogWarning("Could not determine executable path using any method");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining executable path");
            return string.Empty;
        }
    }

    /// <summary>
    /// Updates the auto-start setting based on configuration
    /// </summary>
    public bool UpdateAutoStartSetting(bool enabled)
    {
        if (enabled)
        {
            return EnableAutoStart();
        }
        else
        {
            return DisableAutoStart();
        }
    }

    /// <summary>
    /// Repairs auto-start entry if it's corrupted or points to wrong location
    /// </summary>
    public bool RepairAutoStart()
    {
        try
        {
            _logger.LogInformation("Repairing auto-start entry");
            
            // Remove any existing entry
            DisableAutoStart();
            
            // Re-enable with correct path
            return EnableAutoStart();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to repair auto-start entry");
            return false;
        }
    }

    // IStartupManager async interface implementation
    public async Task<bool> EnableAutoStartAsync()
    {
        return await Task.FromResult(EnableAutoStart());
    }

    public async Task<bool> DisableAutoStartAsync()
    {
        return await Task.FromResult(DisableAutoStart());
    }

    public async Task<bool> IsAutoStartEnabledAsync()
    {
        return await Task.FromResult(IsAutoStartEnabled());
    }

    public async Task<bool> RepairAutoStartAsync()
    {
        return await Task.FromResult(RepairAutoStart());
    }
}
