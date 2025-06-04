using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Threading;
using ChronoGuard.App;
using ChronoGuard.App.Services;
using ChronoGuard.App.ViewModels;
using ChronoGuard.Domain.Interfaces;
using ChronoGuard.Infrastructure.Services;
using ChronoGuard.Application.Services;

namespace ChronoGuard.App;

/// <summary>
/// Application entry point with dependency injection setup and single-instance enforcement
/// </summary>
public static class Program
{
    private static Mutex? _singleInstanceMutex;
    private const string MutexName = "Global\\ChronoGuard_SingleInstance_E9F7B8A5-2C3D-4F6E-9A8B-1C5D7E9F2A4B";

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Enforce single instance
            if (!EnsureSingleInstance())
            {
                // Another instance is already running, exit gracefully
                Environment.Exit(0);
                return;
            }

            // Create host builder with DI container
            var host = CreateHostBuilder(args).Build();

            // Start the WPF application
            var app = new App();
            
            // Set service provider for the app
            App.ServiceProvider = host.Services;
            
            // Run the application
            app.Run();
        }
        catch (Exception ex)
        {
            // Log fatal errors
            var logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<App>();
            logger.LogCritical(ex, "Application failed to start");
            
            System.Windows.MessageBox.Show($"Error fatal al iniciar ChronoGuard:\n{ex.Message}", 
                "ChronoGuard - Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            // Release mutex on exit
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
    }

    /// <summary>
    /// Ensures only one instance of ChronoGuard is running at a time
    /// </summary>
    private static bool EnsureSingleInstance()
    {
        try
        {
            _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
            return createdNew;
        }
        catch (Exception)
        {
            // If mutex creation fails, allow the application to continue
            return true;
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddDebug();
                    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
                });                // Register HTTP client
                services.AddHttpClient();
                  // Register domain services
                services.AddSingleton<ILocationService, LocationService>();
                services.AddSingleton<ISolarCalculatorService, SolarCalculatorService>();
                  // Register color temperature services
                services.AddSingleton<IColorTemperatureService, WindowsColorTemperatureService>();
                  services.AddSingleton<IProfileService, ChronoGuard.Infrastructure.Services.ProfileService>();
                services.AddSingleton<IConfigurationService, ConfigurationService>();
                services.AddSingleton<IConfigurationPersistenceService, ConfigurationPersistenceService>();services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<IUpdateService, UpdateService>();
                services.AddHostedService<UpdateNotificationService>();                services.AddSingleton<IStartupManager, StartupManager>();
                services.AddSingleton<IForegroundApplicationService, ChronoGuard.Infrastructure.Services.WindowsForegroundApplicationService>();
                services.AddSingleton<IPerformanceMonitoringService, ChronoGuard.Infrastructure.Services.PerformanceMonitoringService>();
                  // Register application services
                services.AddSingleton<ChronoGuardBackgroundService>();
                services.AddSingleton<SystemTrayService>();
                services.AddSingleton<ApplicationLifecycleService>();
                  // Register ViewModels
                services.AddTransient<MainWindowViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<PerformanceMonitoringViewModel>();

                // Register Windows
                services.AddTransient<MainWindow>();
                services.AddTransient<SettingsWindow>();
            });
}
