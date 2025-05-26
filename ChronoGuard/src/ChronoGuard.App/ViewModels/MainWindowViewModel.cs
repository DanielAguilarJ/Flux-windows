using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using ChronoGuard.Application.Services;
using WpfApp = System.Windows.Application;
using ChronoGuard.App.Views.Tutorial;

namespace ChronoGuard.App.ViewModels;

/// <summary>
/// ViewModel for the main application window
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ILocationService _locationService;
    private readonly IProfileService _profileService;
    private readonly IConfigurationService _configurationService;
    private readonly ChronoGuardBackgroundService _backgroundService;

    [ObservableProperty]
    private string _currentLocationText = "Detectando ubicación...";

    [ObservableProperty]
    private string _currentTemperatureText = "6500K";

    [ObservableProperty]
    private string _currentProfileName = "Clásico";

    [ObservableProperty]
    private string _currentStatusText = "Activo";

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _isTransitioning = false;

    [ObservableProperty]
    private double _currentColorTemperature = 6500;

    [ObservableProperty]
    private DateTime _nextTransitionTime;

    [ObservableProperty]
    private string _nextTransitionText = "";

    [ObservableProperty]
    private Visibility _pauseButtonVisibility = Visibility.Visible;

    [ObservableProperty]
    private Visibility _resumeButtonVisibility = Visibility.Collapsed;

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        ILocationService locationService,
        IProfileService profileService,
        IConfigurationService configurationService,
        ChronoGuardBackgroundService backgroundService)
    {
        _logger = logger;
        _locationService = locationService;
        _profileService = profileService;
        _configurationService = configurationService;
        _backgroundService = backgroundService;

        // Subscribe to service events
        _locationService.LocationChanged += OnLocationChanged;
        _profileService.ActiveProfileChanged += OnActiveProfileChanged;
        _backgroundService.StateChanged += OnBackgroundServiceStateChanged;

        // Initialize data
        _ = Task.Run(InitializeAsync);
    }

    /// <summary>
    /// Pauses color temperature adjustments
    /// </summary>
    [RelayCommand]
    private async Task PauseAsync()
    {
        try
        {
            await _backgroundService.PauseAsync();
            IsActive = false;
            CurrentStatusText = "Pausado";
            PauseButtonVisibility = Visibility.Collapsed;
            ResumeButtonVisibility = Visibility.Visible;
            
            _logger.LogInformation("ChronoGuard paused by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing ChronoGuard");
            ShowErrorMessage("Error al pausar ChronoGuard");
        }
    }

    /// <summary>
    /// Resumes color temperature adjustments
    /// </summary>
    [RelayCommand]
    private async Task ResumeAsync()
    {
        try
        {
            await _backgroundService.ResumeAsync();
            IsActive = true;
            CurrentStatusText = "Activo";
            PauseButtonVisibility = Visibility.Visible;
            ResumeButtonVisibility = Visibility.Collapsed;
            
            _logger.LogInformation("ChronoGuard resumed by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming ChronoGuard");
            ShowErrorMessage("Error al reanudar ChronoGuard");
        }
        return;
    }

    /// <summary>
    /// Toggles ChronoGuard on/off
    /// </summary>
    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (IsActive)
        {
            await PauseAsync();
        }
        else
        {
            await ResumeAsync();
        }
    }

    /// <summary>
    /// Opens settings window
    /// </summary>
    [RelayCommand]
    private void OpenSettings()
    {
        try
        {
            var settingsWindow = App.ServiceProvider?.GetService(typeof(SettingsWindow)) as SettingsWindow;
            if (settingsWindow != null)
            {
                settingsWindow.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening settings window");
            ShowErrorMessage("Error al abrir configuración");
        }
    }

    /// <summary>
    /// Shows about dialog
    /// </summary>
    [RelayCommand]
    private void ShowAbout()
    {
        var aboutText = $@"ChronoGuard v1.0
Filtro de luz azul avanzado para Windows

© 2025 ChronoGuard
Desarrollado con ❤️ para proteger tus ojos

Características:
• Ajuste automático basado en ubicación
• Múltiples perfiles personalizables  
• Transiciones suaves
• Integración con Windows

Más información: https://github.com/chronoguard/chronoguard";

        System.Windows.MessageBox.Show(aboutText, "Acerca de ChronoGuard", 
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    /// <summary>
    /// Manually updates location
    /// </summary>
    [RelayCommand]
    private async Task UpdateLocationAsync()
    {
        try
        {
            CurrentLocationText = "Actualizando ubicación...";
            var location = await _locationService.GetCurrentLocationAsync();
            
            if (location != null)
            {
                UpdateLocationDisplay(location);
                _logger.LogInformation("Location manually updated: {Location}", location);
            }
            else
            {
                CurrentLocationText = "No se pudo obtener ubicación";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location manually");
            CurrentLocationText = "Error al obtener ubicación";
        }
    }

    /// <summary>
    /// Minimizes window to system tray
    /// </summary>
    [RelayCommand]
    private void MinimizeToTray()
    {
        if (WpfApp.Current.MainWindow != null)
        {
            WpfApp.Current.MainWindow.WindowState = WindowState.Minimized;
            WpfApp.Current.MainWindow.ShowInTaskbar = false;
        }
    }

    /// <summary>
    /// Exits the application
    /// </summary>
    [RelayCommand]
    private void ExitApplication()
    {
        WpfApp.Current.Shutdown();
    }

    /// <summary>
    /// Shows the interactive tutorial
    /// </summary>
    [RelayCommand]
    private void ShowTutorial()
    {
        var tutorial = new TutorialWindow();
        tutorial.ShowDialog();
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Get current location
            var location = await _locationService.GetCurrentLocationAsync();
            if (location != null)
            {
                UpdateLocationDisplay(location);
            }

            // Get current profile
            var profile = await _profileService.GetActiveProfileAsync();
            if (profile != null)
            {
                CurrentProfileName = profile.Name;
            }

            // Get current state from background service
            var state = _backgroundService.CurrentState;
            if (state != null)
            {
                UpdateFromAppState(state);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ViewModel initialization");
        }
    }

    private void OnLocationChanged(object? sender, Location location)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            UpdateLocationDisplay(location);
        });
    }

    private void OnActiveProfileChanged(object? sender, ColorProfile profile)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            CurrentProfileName = profile.Name;
        });
    }

    private void OnBackgroundServiceStateChanged(object? sender, AppState state)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            UpdateFromAppState(state);
        });
    }

    private void UpdateLocationDisplay(Location location)
    {
        if (!string.IsNullOrEmpty(location.City))
        {
            CurrentLocationText = $"{location.City}, {location.Country}";
        }
        else
        {
            CurrentLocationText = $"{location.Latitude:F1}°, {location.Longitude:F1}°";
        }
    }

    private void UpdateFromAppState(AppState state)
    {
        IsActive = !state.IsPaused;
        CurrentStatusText = state.IsPaused ? "Pausado" : "Activo";
        IsTransitioning = state.IsTransitioning;
        
        CurrentColorTemperature = state.CurrentColorTemperature;
        CurrentTemperatureText = $"{state.CurrentColorTemperature}K";

        if (state.NextTransitionTime.HasValue)
        {
            NextTransitionTime = state.NextTransitionTime.Value;
            var timeUntil = NextTransitionTime - DateTime.Now;
            
            if (timeUntil.TotalMinutes > 60)
            {
                NextTransitionText = $"Próxima transición en {timeUntil.Hours}h {timeUntil.Minutes}m";
            }
            else if (timeUntil.TotalMinutes > 0)
            {
                NextTransitionText = $"Próxima transición en {timeUntil.Minutes}m";
            }
            else
            {
                NextTransitionText = "Transición en progreso";
            }
        }
        else
        {
            NextTransitionText = "";
        }

        // Update button visibility
        PauseButtonVisibility = IsActive ? Visibility.Visible : Visibility.Collapsed;
        ResumeButtonVisibility = IsActive ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void ShowErrorMessage(string message)
    {
        System.Windows.MessageBox.Show(message, "ChronoGuard - Error", 
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }
}
