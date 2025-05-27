using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;
using ChronoGuard.App;
using ChronoGuard.App.Services;
using ChronoGuard.App.ViewModels;
using ChronoGuard.Domain.Interfaces;
using ChronoGuard.Infrastructure.Services;
using ChronoGuard.Application.Services;

namespace ChronoGuard.App;

/// <summary>
/// Application entry point with dependency injection setup
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Create host builder with DI container
            var host = CreateHostBuilder(args).Build();            // Start the WPF application
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
                services.AddSingleton<IColorTemperatureService, WindowsColorTemperatureService>();
                services.AddSingleton<IProfileService, ChronoGuard.Infrastructure.Services.ProfileService>();
                services.AddSingleton<IConfigurationService, ConfigurationService>();
                services.AddSingleton<INotificationService, NotificationService>();                services.AddSingleton<IStartupManager, StartupManager>();
                services.AddSingleton<IForegroundApplicationService, ChronoGuard.Infrastructure.Services.WindowsForegroundApplicationService>();
                services.AddSingleton<IPerformanceMonitoringService, ChronoGuard.Infrastructure.Services.PerformanceMonitoringService>();
                
                // Register application services
                services.AddSingleton<ChronoGuardBackgroundService>();
                services.AddSingleton<SystemTrayService>();
                  // Register ViewModels
                services.AddTransient<MainWindowViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<PerformanceMonitoringViewModel>();

                // Register Windows
                services.AddTransient<MainWindow>();
                services.AddTransient<SettingsWindow>();
            });
}
