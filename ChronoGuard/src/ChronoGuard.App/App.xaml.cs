using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ChronoGuard.Application.Services;
using ChronoGuard.App.Services;
using ChronoGuard.Domain.Interfaces;

namespace ChronoGuard.App;

/// <summary>
/// WPF Application class with dependency injection support and enhanced lifecycle management
/// </summary>
public partial class App : System.Windows.Application
{
    public static IServiceProvider? ServiceProvider { get; set; }
      private ILogger<App>? _logger;
    private ChronoGuardBackgroundService? _backgroundService;
    private SystemTrayService? _systemTrayService;
    private ApplicationLifecycleService? _lifecycleService;
    private IStartupManager? _startupManager;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (ServiceProvider == null)
        {
            Shutdown();
            return;
        }

        _logger = ServiceProvider.GetService<ILogger<App>>();
        
        try
        {
            _logger?.LogInformation("ChronoGuard application starting...");

            // Initialize startup manager and configure auto-start if enabled
            _startupManager = ServiceProvider.GetService<IStartupManager>();
            await ConfigureAutoStartAsync();

            // Start background service
            _backgroundService = ServiceProvider.GetRequiredService<ChronoGuardBackgroundService>();
            _ = Task.Run(() => _backgroundService.StartAsync(CancellationToken.None));            // Initialize system tray
            _systemTrayService = ServiceProvider.GetRequiredService<SystemTrayService>();
            _systemTrayService.Initialize();
            _systemTrayService.ShowMainWindowRequested += OnShowMainWindowRequested;
            _systemTrayService.ExitRequested += OnExitRequested;            // Initialize application lifecycle service
            _lifecycleService = ServiceProvider.GetRequiredService<ApplicationLifecycleService>();
            await _lifecycleService.InitializeAsync();
            _lifecycleService.SessionChanged += OnSessionChanged;
            _lifecycleService.PowerStateChanged += OnPowerStateChanged;

            // Create and configure main window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            
            // Configure window behavior
            mainWindow.StateChanged += OnMainWindowStateChanged;
            mainWindow.Closing += OnMainWindowClosing;
            
            // Check if should start minimized
            if (HasStartupArgument("--minimized") || await ShouldStartMinimizedAsync())
            {
                mainWindow.WindowState = WindowState.Minimized;
                mainWindow.ShowInTaskbar = false;
                _logger?.LogInformation("Application started minimized to system tray");
            }
            else
            {
                mainWindow.Show();
            }

            // Show onboarding for first-time users
            await ShowOnboardingIfNeededAsync();

            _logger?.LogInformation("ChronoGuard application started successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Failed to start ChronoGuard application");
            System.Windows.MessageBox.Show($"Error al iniciar ChronoGuard:\n{ex.Message}", 
                "ChronoGuard - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _logger?.LogInformation("ChronoGuard application shutting down...");            // Unsubscribe from events
            if (_systemTrayService != null)
            {
                _systemTrayService.ShowMainWindowRequested -= OnShowMainWindowRequested;
                _systemTrayService.ExitRequested -= OnExitRequested;
            }

            if (_lifecycleService != null)
            {
                _lifecycleService.SessionChanged -= OnSessionChanged;
                _lifecycleService.PowerStateChanged -= OnPowerStateChanged;
            }

            if (MainWindow != null)
            {
                MainWindow.StateChanged -= OnMainWindowStateChanged;
                MainWindow.Closing -= OnMainWindowClosing;
            }

            // Stop background service
            if (_backgroundService != null)
            {
                _backgroundService.StopAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
                _backgroundService.Dispose();
            }            // Dispose system tray service
            _systemTrayService?.Dispose();

            // Dispose lifecycle service
            _lifecycleService?.Dispose();

            _logger?.LogInformation("ChronoGuard application shut down successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during application shutdown");
        }
        finally
        {
            base.OnExit(e);
        }
    }

    private void OnShowMainWindowRequested(object? sender, EventArgs e)
    {
        try
        {
            if (MainWindow != null)
            {
                MainWindow.Show();
                MainWindow.WindowState = WindowState.Normal;
                MainWindow.ShowInTaskbar = true;
                MainWindow.Activate();
                MainWindow.Focus();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to show main window from system tray");
        }
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        try
        {
            Shutdown();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to shutdown application from system tray");
        }
    }

    private void OnMainWindowStateChanged(object? sender, EventArgs e)
    {
        try
        {
            if (MainWindow?.WindowState == WindowState.Minimized)
            {
                MainWindow.Hide();
                MainWindow.ShowInTaskbar = false;
                _logger?.LogDebug("Main window minimized to system tray");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error handling window state change");
        }
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            // Prevent actual closing, minimize to tray instead
            e.Cancel = true;
            if (MainWindow != null)
            {
                MainWindow.WindowState = WindowState.Minimized;
                MainWindow.Hide();
                MainWindow.ShowInTaskbar = false;
            }
            _logger?.LogDebug("Main window close request intercepted, minimized to tray");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error handling window closing");
        }
    }

    private async Task ConfigureAutoStartAsync()
    {
        try
        {
            if (_startupManager != null)
            {
                var configService = ServiceProvider?.GetService<IConfigurationService>();
                if (configService != null)
                {
                    var config = await configService.GetConfigurationAsync();
                    if (config.General.AutoStart)
                    {
                        var isEnabled = await _startupManager.IsAutoStartEnabledAsync();
                        if (!isEnabled)
                        {
                            await _startupManager.EnableAutoStartAsync();
                            _logger?.LogInformation("Auto-start enabled");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to configure auto-start");
        }
    }    private async Task ShowOnboardingIfNeededAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "ChronoGuard", "config.json");
                
                bool isFirstRun = !File.Exists(configPath);
                if (isFirstRun)
                {
                    _logger?.LogInformation("First run detected, showing onboarding");
                    
                    // Show onboarding on UI thread
                    Dispatcher.Invoke(() =>
                    {
                        var onboarding = new OnboardingWindow();
                        onboarding.ShowDialog();
                    });
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to show onboarding");
        }
    }

    private static bool HasStartupArgument(string argument)
    {
        var args = Environment.GetCommandLineArgs();
        return args.Contains(argument, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<bool> ShouldStartMinimizedAsync()
    {
        try
        {
            var configService = ServiceProvider?.GetService<IConfigurationService>();
            if (configService != null)
            {
                var config = await configService.GetConfigurationAsync();
                return config.General.MinimizeToTray && config.General.AutoStart;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not determine startup behavior from configuration");
        }
          return false;
    }

    private void OnSessionChanged(object? sender, SessionChangeEventArgs e)
    {
        try
        {
            _logger?.LogInformation("Session change detected: {ChangeType}", e.ChangeType);
            
            // Handle different session change types
            switch (e.ChangeType)
            {
                case SessionChangeType.SessionLock:
                    _logger?.LogDebug("Session locked - pausing color temperature adjustments");
                    // Could pause the background service or notify it of the session lock
                    break;
                    
                case SessionChangeType.SessionUnlock:
                    _logger?.LogDebug("Session unlocked - resuming color temperature adjustments");
                    // Could resume the background service
                    break;
                    
                case SessionChangeType.SessionLogoff:
                case SessionChangeType.SessionRemoteDisconnect:
                    _logger?.LogDebug("Session ended - minimizing application");
                    if (MainWindow != null)
                    {
                        MainWindow.Hide();
                        MainWindow.ShowInTaskbar = false;
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error handling session change event");
        }
    }

    private void OnPowerStateChanged(object? sender, PowerEventArgs e)
    {
        try
        {
            _logger?.LogInformation("Power state change detected: {EventType}", e.EventType);
            
            // Handle different power state changes
            switch (e.EventType)
            {
                case PowerEventType.Suspend:
                    _logger?.LogDebug("System suspending - saving application state");
                    // Could save current state or pause background operations
                    break;
                    
                case PowerEventType.Resume:
                    _logger?.LogDebug("System resumed - refreshing application state");
                    // Could refresh location, solar times, and resume normal operation
                    _systemTrayService?.RefreshTrayState();
                    break;
                    
                case PowerEventType.BatteryLow:
                    _logger?.LogDebug("Battery low - potentially adjusting performance settings");
                    // Could reduce update frequency to save battery
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error handling power state change event");
        }
    }
}
