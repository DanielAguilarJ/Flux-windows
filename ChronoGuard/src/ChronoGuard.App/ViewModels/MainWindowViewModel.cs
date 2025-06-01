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
using System.Windows.Threading;

namespace ChronoGuard.App.ViewModels;

/// <summary>
/// ViewModel for the main application window
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ILocationService _locationService;
    private readonly IProfileService _profileService;
    private readonly IConfigurationService _configurationService;
    private readonly ChronoGuardBackgroundService _backgroundService;
    private readonly ISolarCalculatorService _solarCalculatorService;
    private readonly IColorTemperatureService _colorTemperatureService;
    
    private readonly DispatcherTimer _updateTimer;
    private DateTime _applicationStartTime;

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
    private bool _isTransitioning = false;    [ObservableProperty]
    private double _currentColorTemperature = 6500;    [ObservableProperty]
    private double _manualTemperature = 6500;

    [ObservableProperty]
    private bool _realTimeTemperatureAdjustment = false;

    [ObservableProperty]
    private DateTime _nextTransitionTime;

    [ObservableProperty]
    private string _nextTransitionText = "";

    [ObservableProperty]
    private Visibility _pauseButtonVisibility = Visibility.Visible;

    [ObservableProperty]
    private Visibility _resumeButtonVisibility = Visibility.Collapsed;

    // Solar data properties for dashboard
    [ObservableProperty]
    private string _solarElevation = "--°";

    [ObservableProperty]
    private string _timeUntilSunset = "--h --m";

    [ObservableProperty]
    private string _applicationUptime = "--h --m";    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        ILocationService locationService,
        IProfileService profileService,
        IConfigurationService configurationService,
        ChronoGuardBackgroundService backgroundService,
        ISolarCalculatorService solarCalculatorService,
        IColorTemperatureService colorTemperatureService)
    {        _logger = logger;
        _locationService = locationService;
        _profileService = profileService;
        _configurationService = configurationService;
        _backgroundService = backgroundService;
        _solarCalculatorService = solarCalculatorService;
        _colorTemperatureService = colorTemperatureService;
        
        // Initialize application start time for uptime calculation
        _applicationStartTime = DateTime.Now;        // Setup update timer for real-time solar data
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1) // Update every minute
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();        // Subscribe to service events
        _locationService.LocationChanged += OnLocationChanged;
        _profileService.ActiveProfileChanged += OnActiveProfileChanged;
        _backgroundService.StateChanged += OnBackgroundServiceStateChanged;
        _colorTemperatureService.TemperatureChanged += OnTemperatureChanged;
        _colorTemperatureService.TransitionCompleted += OnTransitionCompleted;

        // Subscribe to property changes for real-time temperature adjustment
        PropertyChanged += OnViewModelPropertyChanged;

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
    }    /// <summary>
    /// Shows the interactive tutorial
    /// </summary>
    [RelayCommand]
    private void ShowTutorial()
    {
        var tutorial = new TutorialWindow();
        tutorial.ShowDialog();
    }

    /// <summary>
    /// Sets a specific temperature value (for preset buttons)
    /// </summary>
    [RelayCommand]
    private void SetTemperature(object parameter)
    {
        if (parameter is string tempStr && int.TryParse(tempStr, out int temperature))
        {
            ManualTemperature = temperature;
            _logger.LogInformation("Manual temperature preset set to {Temperature}K", temperature);
        }
    }

    /// <summary>
    /// Applies the manually selected temperature
    /// </summary>
    [RelayCommand]
    private async Task ApplyManualTemperatureAsync()
    {
        try
        {
            var colorTemperature = new ColorTemperature((int)ManualTemperature);
            await _colorTemperatureService.ApplyTemperatureAsync(colorTemperature);
            
            _logger.LogInformation("Manual temperature {Temperature}K applied", ManualTemperature);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying manual temperature {Temperature}K", ManualTemperature);            ShowErrorMessage($"Error al aplicar temperatura {ManualTemperature}K");
        }
    }

    /// <summary>
    /// Toggles real-time temperature adjustment
    /// </summary>
    [RelayCommand]
    private void ToggleRealTimeAdjustment()
    {
        RealTimeTemperatureAdjustment = !RealTimeTemperatureAdjustment;
        _logger.LogInformation("Real-time temperature adjustment {Status}", 
            RealTimeTemperatureAdjustment ? "enabled" : "disabled");
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ManualTemperature) && RealTimeTemperatureAdjustment)
        {
            // Apply temperature changes in real-time when real-time adjustment is enabled
            _ = Task.Run(async () =>
            {
                try
                {
                    var colorTemperature = new ColorTemperature((int)ManualTemperature);
                    await _colorTemperatureService.ApplyTemperatureAsync(colorTemperature);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying real-time temperature {Temperature}K", ManualTemperature);
                }
            });
        }
    }private async Task InitializeAsync()
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

            // Initialize manual temperature with current temperature
            var currentTemp = _colorTemperatureService.GetCurrentTemperature();
            if (currentTemp != null)
            {
                ManualTemperature = currentTemp.Kelvin;
                CurrentColorTemperature = currentTemp.Kelvin;
                CurrentTemperatureText = $"{currentTemp.Kelvin}K";
            }

            // Initialize solar data
            await UpdateSolarDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ViewModel initialization");
        }
    }private void OnLocationChanged(object? sender, Location location)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            UpdateLocationDisplay(location);
            // Update solar data with new location
            _ = UpdateSolarDataAsync();
        });
    }

    private void OnActiveProfileChanged(object? sender, ColorProfile profile)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            CurrentProfileName = profile.Name;
        });
    }    private void OnBackgroundServiceStateChanged(object? sender, AppState state)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            UpdateFromAppState(state);
        });
    }    private void OnTemperatureChanged(object? sender, ColorTemperature temperature)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            CurrentColorTemperature = temperature.Kelvin;
            CurrentTemperatureText = $"{temperature.Kelvin}K";
            
            // Sync manual temperature with current temperature (unless user is actively changing it)
            ManualTemperature = temperature.Kelvin;
            
            _logger.LogDebug("Temperature changed to {Temperature}K", temperature.Kelvin);
        });
    }    private void OnTransitionCompleted(object? sender, TransitionState transitionState)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            IsTransitioning = false;
            
            // Update to the final temperature
            CurrentColorTemperature = transitionState.ToTemperature.Kelvin;
            CurrentTemperatureText = $"{transitionState.ToTemperature.Kelvin}K";
            
            // Sync manual temperature with final temperature
            ManualTemperature = transitionState.ToTemperature.Kelvin;
            
            _logger.LogInformation("Transition completed: {Reason}", transitionState.Reason);
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

        // Update button visibility        PauseButtonVisibility = IsActive ? Visibility.Visible : Visibility.Collapsed;
        ResumeButtonVisibility = IsActive ? Visibility.Collapsed : Visibility.Visible;
    }
    
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            // Calculate application uptime
            var uptime = DateTime.Now - _applicationStartTime;
            ApplicationUptime = $"{(int)uptime.TotalHours}h {uptime.Minutes % 60}m";

            // Update solar data asynchronously
            _ = UpdateSolarDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating timer data");
        }
    }

    private async Task UpdateSolarDataAsync()
    {
        try
        {
            var currentLocation = await _locationService.GetCurrentLocationAsync();
            if (currentLocation == null) return;

            var solarTimes = await _solarCalculatorService.CalculateSolarTimesAsync(currentLocation, DateTime.Today);
            if (solarTimes == null) return;

            // Calculate current solar elevation (simplified)
            var now = DateTime.Now;
            var dayLength = solarTimes.Sunset - solarTimes.Sunrise;
            var timeSinceSunrise = now - solarTimes.Sunrise;
            
            // Simple approximation of solar elevation based on time of day
            double elevation;
            if (now < solarTimes.Sunrise || now > solarTimes.Sunset)
            {
                elevation = -10; // Sun is below horizon
            }
            else
            {
                // Peak elevation at solar noon (simplified to 60 degrees max)
                var solarNoon = solarTimes.Sunrise.Add(dayLength / 2);
                var timeFromNoon = Math.Abs((now - solarNoon).TotalHours);
                elevation = Math.Max(0, 60 - (timeFromNoon * 10)); // Rough approximation
            }

            // Update solar elevation
            SolarElevation = $"{elevation:F1}°";

            // Calculate time until sunset
            if (now < solarTimes.Sunset)
            {
                var timeUntilSunset = solarTimes.Sunset - now;
                TimeUntilSunset = $"{timeUntilSunset.Hours}h {timeUntilSunset.Minutes}m";
            }
            else
            {
                // Calculate time until next sunrise
                var tomorrow = DateTime.Today.AddDays(1);
                var tomorrowSolar = await _solarCalculatorService.CalculateSolarTimesAsync(currentLocation, tomorrow);
                if (tomorrowSolar != null)
                {
                    var timeUntilSunrise = tomorrowSolar.Sunrise - now;
                    TimeUntilSunset = $"Amanecer en {timeUntilSunrise.Hours}h {timeUntilSunrise.Minutes}m";
                }
                else
                {
                    TimeUntilSunset = "No disponible";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating solar data");
            SolarElevation = "--°";
            TimeUntilSunset = "--h --m";
        }
    }

    private static void ShowErrorMessage(string message)
    {
        System.Windows.MessageBox.Show(message, "ChronoGuard - Error", 
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }
}
