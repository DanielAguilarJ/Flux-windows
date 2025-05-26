using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ChronoGuard.Application.Services;
// using ChronoGuard.App.Services;

namespace ChronoGuard.App;

/// <summary>
/// WPF Application class with dependency injection support
/// </summary>
public partial class App : System.Windows.Application
{
    public static IServiceProvider? ServiceProvider { get; set; }
    
    private ILogger<App>? _logger;
    private ChronoGuardBackgroundService? _backgroundService;
    // private SystemTrayService? _systemTrayService;

    protected override void OnStartup(StartupEventArgs e)
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

            // Start background service
            _backgroundService = ServiceProvider.GetRequiredService<ChronoGuardBackgroundService>();
            _ = Task.Run(() => _backgroundService.StartAsync(CancellationToken.None));

            // Initialize system tray
            // _systemTrayService = ServiceProvider.GetRequiredService<SystemTrayService>();

            // Create and show main window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            
            // Check if should start minimized
            if (HasStartupArgument("--minimized") || ShouldStartMinimized())
            {
                mainWindow.WindowState = WindowState.Minimized;
                mainWindow.ShowInTaskbar = false;
            }
            
            mainWindow.Show();

            // Onboarding for first-time users
            string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChronoGuard", "config.json");
            bool isFirstRun = !File.Exists(configPath);
            if (isFirstRun)
            {
                var onboarding = new OnboardingWindow();
                onboarding.ShowDialog();
            }

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
            _logger?.LogInformation("ChronoGuard application shutting down...");

            // Stop background service
            if (_backgroundService != null)
            {
                _backgroundService.StopAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
                _backgroundService.Dispose();
            }

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

    private static bool HasStartupArgument(string argument)
    {
        var args = Environment.GetCommandLineArgs();
        return args.Contains(argument, StringComparer.OrdinalIgnoreCase);
    }

    private bool ShouldStartMinimized()
    {
        try
        {
            var configService = ServiceProvider?.GetService<ChronoGuard.Domain.Interfaces.IConfigurationService>();
            if (configService != null)
            {
                var config = configService.GetConfigurationAsync().Result;
                return config.General.MinimizeToTray && config.General.AutoStart;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not determine startup behavior from configuration");
        }
        
        return false;
    }
}
