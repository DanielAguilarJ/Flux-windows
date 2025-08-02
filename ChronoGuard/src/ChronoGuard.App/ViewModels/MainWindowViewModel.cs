using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Diagnostics;
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

    // Circular timeline properties
    [ObservableProperty]
    private string _currentTemperature = "6500K";
    
    [ObservableProperty]
    private string _currentPhase = "Día";
    
    [ObservableProperty]
    private string _nextEvent = "Calculando...";
    
    [ObservableProperty]
    private string _sunriseTime = "--:--";
    
    [ObservableProperty]
    private string _sunsetTime = "--:--";
    
    [ObservableProperty]
    private string _dayLength = "--h --m";
    
    [ObservableProperty]
    private string _activeProfileName = "Clásico";
    
    [ObservableProperty]
    private string _dayTemperature = "6500K";
    
    [ObservableProperty]
    private string _nightTemperature = "2700K";
    
    [ObservableProperty]
    private string _currentLocation = "Detectando ubicación...";

    // Solar data properties for dashboard
    [ObservableProperty]
    private string _solarElevation = "--°";

    [ObservableProperty]
    private string _timeUntilSunset = "--h --m";

    [ObservableProperty]
    private string _applicationUptime = "0h 0m";

    // ...existing code...
    public MainWindowViewModel(
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
        _applicationStartTime = DateTime.Now;// Setup update timer for real-time solar data
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
    }    /// <summary>
    /// Sets a specific temperature value (for preset buttons)
    /// </summary>
    [RelayCommand]
    private void SetTemperature(object? parameter)
    {
        if (parameter is string tempStr && int.TryParse(tempStr, out int temperature))
        {
            ManualTemperature = temperature;
            _logger.LogInformation("Manual temperature preset set to {Temperature}K", temperature);
        }
    }    /// <summary>
    /// Applies the manually selected temperature
    /// </summary>
    [RelayCommand]
    private async Task ApplyManualTemperatureAsync()
    {
        try
        {
            var colorTemperature = new ColorTemperature((int)ManualTemperature);
            var success = await _colorTemperatureService.ApplyTemperatureAsync(colorTemperature);
            
            if (success)
            {
                _logger.LogInformation("Manual temperature {Temperature}K applied successfully", ManualTemperature);
                ShowSuccessMessage($"Temperatura {ManualTemperature}K aplicada correctamente");
            }
            else
            {
                _logger.LogWarning("Failed to apply manual temperature {Temperature}K - monitor/driver may not support gamma manipulation", ManualTemperature);
                ShowErrorMessage($"No se pudo aplicar la temperatura {ManualTemperature}K.\n\n" +
                    "Posibles causas:\n" +
                    "• Tu monitor no soporta ajustes de gamma\n" +
                    "• El driver de gráficos es incompatible\n" +
                    "• Otro software está controlando los colores\n" +
                    "• Se necesitan permisos de administrador\n\n" +
                    "Prueba ejecutar como administrador o usar la Luz Nocturna de Windows.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying manual temperature {Temperature}K", ManualTemperature);
            ShowErrorMessage($"Error inesperado al aplicar temperatura {ManualTemperature}K:\n{ex.Message}");
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
                await UpdateCircularTimelineAsync(location);
            }

            // Get current profile
            var profile = await _profileService.GetActiveProfileAsync();
            if (profile != null)
            {
                CurrentProfileName = profile.Name;
                ActiveProfileName = profile.Name;
                DayTemperature = $"{profile.DayTemperature}K";
                NightTemperature = $"{profile.NightTemperature}K";
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
                CurrentTemperature = $"{currentTemp.Kelvin}K";
            }

            // Initialize solar data and circular timeline
            await UpdateSolarDataAsync();
            await UpdateCircularTimelineDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ViewModel initialization");
        }
    }

    /// <summary>
    /// Updates the circular timeline with current location data
    /// </summary>
    private async Task UpdateCircularTimelineAsync(Location location)
    {
        try
        {
            CurrentLocation = location.City ?? $"{location.Latitude:F2}, {location.Longitude:F2}";
            
            var solarTimes = await _solarCalculatorService.CalculateSolarTimesAsync(location, DateTime.Today);
            if (solarTimes != null)
            {
                SunriseTime = solarTimes.Sunrise.ToString("HH:mm");
                SunsetTime = solarTimes.Sunset.ToString("HH:mm");
                DayLength = $"{solarTimes.DayLength.Hours}h {solarTimes.DayLength.Minutes}m";
                
                UpdateCurrentPhaseAndNextEvent(solarTimes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating circular timeline data");
        }
    }

    /// <summary>
    /// Updates circular timeline data periodically
    /// </summary>
    private async Task UpdateCircularTimelineDataAsync()
    {
        try
        {
            var location = await _locationService.GetCurrentLocationAsync();
            if (location != null)
            {
                var solarTimes = await _solarCalculatorService.CalculateSolarTimesAsync(location, DateTime.Today);
                if (solarTimes != null)
                {
                    UpdateCurrentPhaseAndNextEvent(solarTimes);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating circular timeline data");
        }
    }

    /// <summary>
    /// Updates current phase and next event based on solar times
    /// </summary>
    private void UpdateCurrentPhaseAndNextEvent(SolarTimes solarTimes)
    {
        var now = DateTime.Now;
        var phase = solarTimes.GetDayPhase(now);
        
        CurrentPhase = phase switch
        {
            DayPhase.Night => "Noche",
            DayPhase.Sunrise => "Amanecer",
            DayPhase.Day => "Día",
            DayPhase.Sunset => "Atardecer",
            _ => "Desconocido"
        };

        // Calculate next event
        var nowTime = now.TimeOfDay;
        var sunrise = solarTimes.Sunrise.TimeOfDay;
        var sunset = solarTimes.Sunset.TimeOfDay;

        TimeSpan timeToNext;
        string eventName;

        if (nowTime < sunrise)
        {
            timeToNext = sunrise - nowTime;
            eventName = "Amanecer";
        }
        else if (nowTime < sunset)
        {
            timeToNext = sunset - nowTime;
            eventName = "Atardecer";
        }
        else
        {
            timeToNext = TimeSpan.FromDays(1) - nowTime + sunrise;
            eventName = "Amanecer";
        }

        NextEvent = $"{eventName} en {FormatTimeSpan(timeToNext)}";
    }

    /// <summary>
    /// Formats a timespan for display
    /// </summary>
    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
        {
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
        }
        else if (timeSpan.TotalHours >= 1)
        {
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
        }
        else
        {
            return $"{timeSpan.Minutes}m";
        }
    }private void OnLocationChanged(object? sender, Location location)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            UpdateLocationDisplay(location);
            // Update solar data with new location
            _ = UpdateSolarDataAsync();
            // Update circular timeline with new location
            _ = UpdateCircularTimelineAsync(location);
        });
    }

    private void OnActiveProfileChanged(object? sender, ColorProfile profile)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            CurrentProfileName = profile.Name;
            ActiveProfileName = profile.Name;
            DayTemperature = $"{profile.DayTemperature}K";
            NightTemperature = $"{profile.NightTemperature}K";
            
            // Update circular timeline data
            _ = UpdateCircularTimelineDataAsync();
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
            CurrentTemperature = $"{temperature.Kelvin}K";
            
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
            
            // Update circular timeline data
            _ = UpdateCircularTimelineDataAsync();
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
    }    private static void ShowErrorMessage(string message)
    {
        System.Windows.MessageBox.Show(message, "ChronoGuard - Error", 
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }

    private static void ShowSuccessMessage(string message)
    {
        System.Windows.MessageBox.Show(message, "ChronoGuard - Éxito", 
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    /// <summary>
    /// Runs compatibility diagnostics to help troubleshoot color temperature issues
    /// </summary>
    [RelayCommand]
    private async Task RunDiagnosticsAsync()
    {
        try
        {
            _logger.LogInformation("Starting compatibility diagnostics");
            
            // Create a progress window or use existing UI feedback
            var progressWindow = new Window
            {
                Title = "Diagnóstico de Compatibilidad",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = WpfApp.Current.MainWindow,
                ResizeMode = ResizeMode.NoResize
            };

            var textBlock = new TextBlock
            {
                Text = "Ejecutando diagnóstico de compatibilidad...\nEsto puede tomar unos momentos.",
                Margin = new Thickness(20),
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14
            };

            var scrollViewer = new ScrollViewer
            {
                Content = textBlock,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            progressWindow.Content = scrollViewer;
            progressWindow.Show();

            // Run basic diagnostics
            var diagnosticResult = await RunBasicDiagnosticsAsync();
            
            // Update the window with results
            textBlock.Text = FormatDiagnosticResults(diagnosticResult);
            
            _logger.LogInformation("Compatibility diagnostics completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running compatibility diagnostics");
            ShowErrorMessage($"Error ejecutando diagnóstico:\n{ex.Message}");
        }
    }

    /// <summary>
    /// Runs basic compatibility diagnostics without external dependencies
    /// </summary>
    private async Task<DiagnosticResults> RunBasicDiagnosticsAsync()
    {
        var results = new DiagnosticResults();
        
        try
        {
            // Test 1: Check if we can enumerate monitors
            var monitorCount = 0;
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                monitorCount++;
                return true;
            }, IntPtr.Zero);
            
            results.TotalMonitorCount = monitorCount;
            results.MonitorEnumerationSuccess = monitorCount > 0;

            // Test 2: Try to apply a test temperature
            try
            {
                var testTemperature = new ColorTemperature(6500); // Neutral temperature
                var success = await _colorTemperatureService.ApplyTemperatureAsync(testTemperature);
                results.GammaManipulationSuccess = success;
                results.ErrorDetails.Add(success ? "✅ Manipulación de gamma: EXITOSA" : "❌ Manipulación de gamma: FALLÓ");
            }
            catch (Exception ex)
            {
                results.GammaManipulationSuccess = false;
                results.ErrorDetails.Add($"❌ Error en manipulación de gamma: {ex.Message}");
            }

            // Test 3: Check running processes for conflicts
            var conflictingProcesses = CheckForConflictingSoftware();
            results.ConflictingSoftwareFound = conflictingProcesses.Count > 0;
            if (conflictingProcesses.Count > 0)
            {
                results.ErrorDetails.Add("⚠️ Software conflictivo detectado:");
                foreach (var process in conflictingProcesses)
                {
                    results.ErrorDetails.Add($"  • {process}");
                }
            }

            // Test 4: Basic system info
            results.SystemInfo.Add($"🖥️ Monitores detectados: {monitorCount}");
            results.SystemInfo.Add($"💻 Sistema: {Environment.OSVersion}");
            results.SystemInfo.Add($"🎮 Ejecutando como administrador: {IsRunningAsAdministrator()}");

            // Generate recommendations
            GenerateRecommendations(results);
        }
        catch (Exception ex)
        {
            results.ErrorDetails.Add($"❌ Error general en diagnóstico: {ex.Message}");
        }

        return results;
    }

    private List<string> CheckForConflictingSoftware()
    {
        var conflictingProcesses = new List<string>();
        var conflictingNames = new[] { "f.lux", "redshift", "lightbulb", "iris", "sunsetscreen", "nightlight" };

        try
        {
            var processes = System.Diagnostics.Process.GetProcesses();
            foreach (var process in processes)
            {
                try
                {
                    var processName = process.ProcessName.ToLower();
                    if (conflictingNames.Any(name => processName.Contains(name)))
                    {
                        conflictingProcesses.Add(process.ProcessName);
                    }
                }
                catch { /* Ignore access denied errors */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for conflicting software");
        }

        return conflictingProcesses;
    }

    private bool IsRunningAsAdministrator()
    {
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void GenerateRecommendations(DiagnosticResults results)
    {
        if (!results.GammaManipulationSuccess)
        {
            results.Recommendations.Add("💡 Ejecutar ChronoGuard como administrador");
            results.Recommendations.Add("💡 Actualizar drivers de gráficos");
            results.Recommendations.Add("💡 Cerrar software conflictivo (f.lux, etc.)");
            results.Recommendations.Add("💡 Verificar soporte de gamma en monitor");
        }

        if (results.ConflictingSoftwareFound)
        {
            results.Recommendations.Add("⚠️ Cerrar o desinstalar software conflictivo");
        }

        if (!results.MonitorEnumerationSuccess)
        {
            results.Recommendations.Add("🔧 Verificar conexión de monitores");
            results.Recommendations.Add("🔧 Reiniciar sistema de gráficos");
        }
    }

    private string FormatDiagnosticResults(DiagnosticResults results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== RESULTADOS DEL DIAGNÓSTICO ===\n");

        // System info
        sb.AppendLine("📊 INFORMACIÓN DEL SISTEMA:");
        foreach (var info in results.SystemInfo)
        {
            sb.AppendLine($"  {info}");
        }
        sb.AppendLine();

        // Test results
        sb.AppendLine("🧪 RESULTADOS DE PRUEBAS:");
        foreach (var detail in results.ErrorDetails)
        {
            sb.AppendLine($"  {detail}");
        }
        sb.AppendLine();

        // Recommendations
        if (results.Recommendations.Count > 0)
        {
            sb.AppendLine("💡 RECOMENDACIONES:");
            foreach (var recommendation in results.Recommendations)
            {
                sb.AppendLine($"  {recommendation}");
            }
            sb.AppendLine();
        }

        // Overall status
        sb.AppendLine("📋 RESUMEN:");
        if (results.GammaManipulationSuccess && !results.ConflictingSoftwareFound)
        {
            sb.AppendLine("  ✅ Tu sistema es compatible con ChronoGuard");
            sb.AppendLine("  ✅ Los ajustes de temperatura deberían funcionar correctamente");
        }
        else
        {
            sb.AppendLine("  ⚠️ Se detectaron problemas de compatibilidad");
            sb.AppendLine("  📋 Sigue las recomendaciones para resolver los problemas");
        }

        return sb.ToString();
    }

    // Windows API for monitor enumeration
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

    private delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // Diagnostic results class
    private class DiagnosticResults
    {
        public bool MonitorEnumerationSuccess { get; set; }
        public bool GammaManipulationSuccess { get; set; }
        public bool ConflictingSoftwareFound { get; set; }
        public int TotalMonitorCount { get; set; }
        public List<string> SystemInfo { get; set; } = new();
        public List<string> ErrorDetails { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    // ...existing code...
}
