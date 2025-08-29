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
using System.Management;
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

    // New: expose today's solar times for UI bindings (solar curve)
    [ObservableProperty]
    private SolarTimes? _todaySolarTimes;

    // New: expose current time so the UI control can animate the sun position
    [ObservableProperty]
    private DateTime _nowTime = DateTime.Now;

    [ObservableProperty]
    private string _timeUntilSunset = "--h --m";

    [ObservableProperty]
    private string _applicationUptime = "0h 0m";

    // Automatic temperature adjustment properties
    [ObservableProperty]
    private bool _automaticTemperatureEnabled = true;

    [ObservableProperty]
    private string _currentSolarPhase = "Detectando...";

    [ObservableProperty]
    private bool _isLocationDetectionInProgress = false;

    [ObservableProperty]
    private string _locationStatus = "Detectando ubicación...";

    [ObservableProperty]
    private DateTime _lastLocationUpdate = DateTime.MinValue;

    [ObservableProperty]
    private Location? _currentLocation;

    // Temperature configuration
    [ObservableProperty]
    private int _dayTemperature = 6500;

    [ObservableProperty]
    private int _nightTemperature = 2700;

    [ObservableProperty]
    private int _transitionDurationMinutes = 60;

    // Cached solar times for performance
    private SolarTimes? _cachedSolarTimes;
    private DateTime _cachedSolarDate = DateTime.MinValue;
    
    // Timer for automatic temperature updates
    private readonly DispatcherTimer _automaticUpdateTimer;
    
    // Constants for temperature calculation
    private const double TransitionHours = 1.0; // Hours for each transition (sunset/sunrise)
    private const int MinTransitionMinutes = 30;
    private const int MaxTransitionMinutes = 120;

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
        _applicationStartTime = DateTime.Now;        // Setup update timer for real-time solar data
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1) // Update every minute
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
        
        // Setup automatic temperature update timer (more frequent for smooth transitions)
        _automaticUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5) // Check every 5 minutes for automatic updates
        };
        _automaticUpdateTimer.Tick += AutomaticUpdateTimer_Tick;
        _automaticUpdateTimer.Start();// Subscribe to service events
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
    }    /// <summary>
    /// Manually updates location (simplified version, use ForceLocationDetectionAsync for full detection)
    /// </summary>
    [RelayCommand]
    private async Task UpdateLocationAsync()
    {
        await ForceLocationDetectionAsync();
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

    /// <summary>
    /// Toggles automatic temperature adjustment based on solar times
    /// </summary>
    [RelayCommand]
    private async Task ToggleAutomaticTemperatureAsync()
    {
        try
        {
            AutomaticTemperatureEnabled = !AutomaticTemperatureEnabled;
            
            if (AutomaticTemperatureEnabled)
            {
                _logger.LogInformation("Automatic temperature adjustment enabled");
                // Immediately apply automatic temperature
                await ApplyAutomaticTemperatureAsync();
            }
            else
            {
                _logger.LogInformation("Automatic temperature adjustment disabled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling automatic temperature adjustment");
            ShowErrorMessage("Error al cambiar el modo automático");
        }
    }

    /// <summary>
    /// Forces location detection and updates solar calculations
    /// </summary>
    [RelayCommand]
    private async Task ForceLocationDetectionAsync()
    {
        try
        {
            if (IsLocationDetectionInProgress) return;
            
            IsLocationDetectionInProgress = true;
            LocationStatus = "Detectando ubicación...";
            
            _logger.LogInformation("Forcing location detection");
            
            // Try to get fresh location
            var location = await GetBestLocationAsync();
            
            if (location != null)
            {
                CurrentLocation = location;
                CurrentLocationText = FormatLocationText(location);
                LastLocationUpdate = DateTime.Now;
                LocationStatus = "Ubicación actualizada correctamente";
                
                // Recalculate solar times and temperature
                await RecalculateSolarDataAndTemperatureAsync(location);
                
                _logger.LogInformation("Location detection completed: {Location}", location);
            }
            else
            {
                LocationStatus = "No se pudo detectar la ubicación";
                ShowErrorMessage("No se pudo detectar la ubicación. Verifica permisos y conexión.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forced location detection");
            LocationStatus = "Error al detectar ubicación";
            ShowErrorMessage($"Error al detectar ubicación: {ex.Message}");
        }
        finally
        {
            IsLocationDetectionInProgress = false;
        }
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
            }            // Test 4: Detailed system info
            var systemInfo = await GetDetailedSystemInfoAsync();
            results.SystemInfo.Add($"🖥️ Monitores detectados: {monitorCount}");
            results.SystemInfo.AddRange(systemInfo);
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

    /// <summary>
    /// Automatic temperature update timer handler
    /// </summary>
    private async void AutomaticUpdateTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            if (AutomaticTemperatureEnabled)
            {
                await ApplyAutomaticTemperatureAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in automatic temperature update timer");
        }
    }    /// <summary>
    /// Gets the best available location using multiple sources
    /// </summary>
    private async Task<Location?> GetBestLocationAsync()
    {
        try
        {
            // Try Windows Location API first
            var windowsLocation = await _locationService.GetCurrentLocationAsync();
            if (windowsLocation != null)
            {
                _logger.LogInformation("Location obtained from Windows Location API");
                return windowsLocation;
            }

            // Return cached location if available
            if (CurrentLocation != null)
            {
                _logger.LogInformation("Using cached location");
                return CurrentLocation;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting best location");
            return CurrentLocation; // Return cached if available
        }
    }

    /// <summary>
    /// Formats location for display
    /// </summary>
    private string FormatLocationText(Location location)
    {
        if (!string.IsNullOrEmpty(location.City) && !string.IsNullOrEmpty(location.Country))
        {
            return $"{location.City}, {location.Country}";
        }
        else if (!string.IsNullOrEmpty(location.City))
        {
            return location.City;
        }
        else
        {
            return $"{location.Latitude:F1}°N, {location.Longitude:F1}°E";
        }
    }

    /// <summary>
    /// Recalculates solar data and applies appropriate temperature when location changes
    /// </summary>
    private async Task RecalculateSolarDataAndTemperatureAsync(Location location)
    {
        try
        {
            // Clear cached solar times to force recalculation
            _cachedSolarTimes = null;
            _cachedSolarDate = DateTime.MinValue;

            // Get new solar times
            var solarTimes = await GetSolarTimesAsync(location);
            if (solarTimes != null)
            {
                _cachedSolarTimes = solarTimes;
                _cachedSolarDate = DateTime.Today;

                // Update solar phase display
                UpdateSolarPhaseDisplay(solarTimes);

                // Apply new temperature if automatic mode is enabled
                if (AutomaticTemperatureEnabled)
                {
                    await ApplyAutomaticTemperatureAsync();
                }

                _logger.LogInformation("Solar data recalculated for new location");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating solar data for new location");
        }
    }

    /// <summary>
    /// Applies automatic temperature based on current solar phase
    /// </summary>
    private async Task ApplyAutomaticTemperatureAsync()
    {
        try
        {
            if (!AutomaticTemperatureEnabled || CurrentLocation == null || !IsActive)
                return;

            var solarTimes = await GetSolarTimesAsync(CurrentLocation);
            if (solarTimes == null)
                return;

            var now = DateTime.Now;
            var targetTemperature = CalculateAutomaticTemperature(now, solarTimes);

            // Update solar phase display
            UpdateSolarPhaseDisplay(solarTimes);

            // Apply the calculated temperature
            var colorTemperature = new ColorTemperature(targetTemperature);
            var success = await _colorTemperatureService.ApplyTemperatureAsync(colorTemperature);

            if (success)
            {
                CurrentColorTemperature = targetTemperature;
                CurrentTemperatureText = $"{targetTemperature}K";
                ManualTemperature = targetTemperature; // Sync manual slider

                _logger.LogDebug("Automatic temperature applied: {Temperature}K", targetTemperature);
            }
            else
            {
                _logger.LogWarning("Failed to apply automatic temperature: {Temperature}K", targetTemperature);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying automatic temperature");
        }
    }

    /// <summary>
    /// Calculates the appropriate temperature based on current time and solar data
    /// </summary>
    private int CalculateAutomaticTemperature(DateTime currentTime, SolarTimes solarTimes)
    {
        var timeOfDay = currentTime.TimeOfDay;
        var sunrise = solarTimes.Sunrise.TimeOfDay;
        var sunset = solarTimes.Sunset.TimeOfDay;

        // Define transition periods (1 hour before/after sunrise and sunset)
        var transitionDuration = TimeSpan.FromMinutes(TransitionDurationMinutes);
        var sunriseTransitionStart = sunrise - transitionDuration;
        var sunriseTransitionEnd = sunrise + transitionDuration;
        var sunsetTransitionStart = sunset - transitionDuration;
        var sunsetTransitionEnd = sunset + transitionDuration;

        // Handle day/night periods
        if (timeOfDay >= sunriseTransitionEnd && timeOfDay <= sunsetTransitionStart)
        {
            // Daytime - use day temperature
            return DayTemperature;
        }
        else if (timeOfDay <= sunriseTransitionStart || timeOfDay >= sunsetTransitionEnd)
        {
            // Nighttime - use night temperature
            return NightTemperature;
        }
        else if (timeOfDay >= sunriseTransitionStart && timeOfDay <= sunriseTransitionEnd)
        {
            // Sunrise transition - interpolate from night to day
            var progress = (timeOfDay - sunriseTransitionStart).TotalMinutes / (2 * TransitionDurationMinutes);
            progress = Math.Clamp(progress, 0.0, 1.0);
            
            // Use smooth sigmoid interpolation
            var smoothProgress = SmoothInterpolation(progress);
            return (int)Math.Round(NightTemperature + (DayTemperature - NightTemperature) * smoothProgress);
        }
        else if (timeOfDay >= sunsetTransitionStart && timeOfDay <= sunsetTransitionEnd)
        {
            // Sunset transition - interpolate from day to night
            var progress = (timeOfDay - sunsetTransitionStart).TotalMinutes / (2 * TransitionDurationMinutes);
            progress = Math.Clamp(progress, 0.0, 1.0);
            
            // Use smooth sigmoid interpolation
            var smoothProgress = SmoothInterpolation(progress);
            return (int)Math.Round(DayTemperature + (NightTemperature - DayTemperature) * smoothProgress);
        }

        // Fallback - shouldn't reach here
        return DayTemperature;
    }

    /// <summary>
    /// Smooth sigmoid interpolation for natural transitions
    /// </summary>
    private static double SmoothInterpolation(double t)
    {
        // Sigmoid function for smooth transitions
        // f(t) = 1 / (1 + e^(-k*(t-0.5)))
        // where k controls the steepness (6 gives a nice smooth curve)
        const double k = 6.0;
        return 1.0 / (1.0 + Math.Exp(-k * (t - 0.5)));
    }

    /// <summary>
    /// Updates the solar phase display text
    /// </summary>
    private void UpdateSolarPhaseDisplay(SolarTimes solarTimes)
    {
        var now = DateTime.Now;
        var timeOfDay = now.TimeOfDay;
        var sunrise = solarTimes.Sunrise.TimeOfDay;
        var sunset = solarTimes.Sunset.TimeOfDay;

        var transitionDuration = TimeSpan.FromMinutes(TransitionDurationMinutes);

        if (timeOfDay >= sunrise + transitionDuration && timeOfDay <= sunset - transitionDuration)
        {
            CurrentSolarPhase = "🌞 Día - Temperatura cálida";
        }
        else if (timeOfDay <= sunrise - transitionDuration || timeOfDay >= sunset + transitionDuration)
        {
            CurrentSolarPhase = "🌙 Noche - Temperatura fría";
        }
        else if (timeOfDay >= sunrise - transitionDuration && timeOfDay <= sunrise + transitionDuration)
        {
            CurrentSolarPhase = "🌅 Transición de amanecer";
        }
        else if (timeOfDay >= sunset - transitionDuration && timeOfDay <= sunset + transitionDuration)
        {
            CurrentSolarPhase = "🌇 Transición de atardecer";
        }
        else
        {
            CurrentSolarPhase = "⏰ Calculando fase solar...";
        }
    }

    /// <summary>
    /// Gets solar times for the given location with caching
    /// </summary>
    private async Task<SolarTimes?> GetSolarTimesAsync(Location location)
    {
        try
        {
            var today = DateTime.Today;

            // Return cached data if available and current
            if (_cachedSolarTimes != null && 
                _cachedSolarDate == today && 
                Math.Abs(_cachedSolarTimes.Location.Latitude - location.Latitude) < 0.1 &&
                Math.Abs(_cachedSolarTimes.Location.Longitude - location.Longitude) < 0.1)
            {
                return _cachedSolarTimes;
            }

            // Calculate new solar times
            var solarTimes = await _solarCalculatorService.CalculateSolarTimesAsync(location, today);
            if (solarTimes != null)
            {
                _cachedSolarTimes = solarTimes;
                _cachedSolarDate = today;
            }

            return solarTimes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting solar times for location");
            return _cachedSolarTimes; // Return cached data if available
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Get current location
            var location = await GetBestLocationAsync();
            if (location != null)
            {
                CurrentLocation = location;
                CurrentLocationText = FormatLocationText(location);
                LastLocationUpdate = DateTime.Now;
                LocationStatus = "Ubicación detectada correctamente";
            }
            else
            {
                LocationStatus = "No se pudo detectar la ubicación";
                CurrentLocationText = "Ubicación no disponible";
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

            // Initialize solar data and apply automatic temperature
            if (CurrentLocation != null)
            {
                await RecalculateSolarDataAndTemperatureAsync(CurrentLocation);
            }

            // Apply automatic temperature if enabled
            if (AutomaticTemperatureEnabled)
            {
                await ApplyAutomaticTemperatureAsync();
            }

            _logger.LogInformation("ViewModel initialization completed");
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
            CurrentLocation = location;
            CurrentLocationText = FormatLocationText(location);
            LastLocationUpdate = DateTime.Now;
            LocationStatus = "Ubicación actualizada";
            
            // Recalculate solar data with new location
            _ = RecalculateSolarDataAndTemperatureAsync(location);
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

    private void OnTemperatureChanged(object? sender, ColorTemperature temperature)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            CurrentColorTemperature = temperature.Kelvin;
            CurrentTemperatureText = $"{temperature.Kelvin}K";
            
            // Sync manual temperature with current temperature (unless user is actively changing it)
            if (!RealTimeTemperatureAdjustment)
            {
                ManualTemperature = temperature.Kelvin;
            }
            
            _logger.LogDebug("Temperature changed to {Temperature}K", temperature.Kelvin);
        });
    }

    private void OnTransitionCompleted(object? sender, TransitionState transitionState)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            IsTransitioning = false;
            
            // Update to the final temperature
            CurrentColorTemperature = transitionState.ToTemperature.Kelvin;
            CurrentTemperatureText = $"{transitionState.ToTemperature.Kelvin}K";
            
            // Sync manual temperature with final temperature
            if (!RealTimeTemperatureAdjustment)
            {
                ManualTemperature = transitionState.ToTemperature.Kelvin;
            }
            
            _logger.LogInformation("Transition completed: {Reason}", transitionState.Reason);
        });
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
    
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            // Calculate application uptime
            var uptime = DateTime.Now - _applicationStartTime;
            ApplicationUptime = $"{(int)uptime.TotalHours}h {uptime.Minutes % 60}m";

            // Update current time for solar curve animation
            NowTime = DateTime.Now;

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
            var currentLocation = CurrentLocation ?? await GetBestLocationAsync();
            if (currentLocation == null) return;

            var solarTimes = await _solarCalculatorService.CalculateSolarTimesAsync(currentLocation, DateTime.Today);
            if (solarTimes == null) return;

            // New: publish today solar times for the UI control
            TodaySolarTimes = solarTimes;

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

            // Update solar phase display
            UpdateSolarPhaseDisplay(solarTimes);

            // New: during sunrise/sunset transitions, tighten updates to 1-minute by applying temperature here
            if (AutomaticTemperatureEnabled && IsActive && IsWithinTransitionWindow(now, solarTimes))
            {
                await ApplyAutomaticTemperatureAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating solar data");
            SolarElevation = "--°";
            TimeUntilSunset = "--h --m";
        }
    }

    /// <summary>
    /// Returns true if current time is within the configured sunrise/sunset transition windows
    /// </summary>
    private bool IsWithinTransitionWindow(DateTime now, SolarTimes solarTimes)
    {
        var sunrise = solarTimes.Sunrise.TimeOfDay;
        var sunset = solarTimes.Sunset.TimeOfDay;
        var t = now.TimeOfDay;
        var d = TimeSpan.FromMinutes(TransitionDurationMinutes);

        var inSunrise = t >= (sunrise - d) && t <= (sunrise + d);
        var inSunset = t >= (sunset - d) && t <= (sunset + d);
        return inSunrise || inSunset;
    }

    /// <summary>
    /// Forces an immediate UI and automatic temperature refresh (used when window is activated)
    /// </summary>
    public async Task ForceImmediateUpdateAsync()
    {
        try
        {
            NowTime = DateTime.Now;
            await UpdateSolarDataAsync();
            if (AutomaticTemperatureEnabled && IsActive)
            {
                await ApplyAutomaticTemperatureAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forcing immediate update");
        }
    }

    private static void ShowErrorMessage(string message)
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
    /// Gets detailed system information for diagnostics
    /// </summary>
    private async Task<List<string>> GetDetailedSystemInfoAsync()
    {
        return await Task.Run(() =>
        {
            var systemInfo = new List<string>();
            
            try
            {
                // Operating System Information
                var osInfo = Environment.OSVersion;
                var osArchitecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
                systemInfo.Add($"💻 Sistema: {osInfo.VersionString} ({osArchitecture})");
                
                // .NET Runtime Information
                var runtimeVersion = Environment.Version;
                systemInfo.Add($"🔧 .NET Runtime: {runtimeVersion}");
                
                // Processor Information
                var processorName = "Desconocido";
                var processorCores = Environment.ProcessorCount;
                
                try
                {
                    using (var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                    {
                        foreach (System.Management.ManagementObject obj in searcher.Get())
                        {
                            processorName = obj["Name"]?.ToString()?.Trim() ?? "Desconocido";
                            break; // Get first processor
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Could not retrieve processor info: {Error}", ex.Message);
                    processorName = "No disponible";
                }
                
                systemInfo.Add($"⚙️ Procesador: {processorName} ({processorCores} núcleos)");
                
                // Memory Information
                var totalMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
                systemInfo.Add($"💾 Memoria en uso: ~{totalMemoryMB} MB");
                
                // Get total system memory
                try
                {
                    using (var searcher = new System.Management.ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                    {
                        foreach (System.Management.ManagementObject obj in searcher.Get())
                        {
                            if (obj["TotalPhysicalMemory"] != null)
                            {
                                var totalMemoryBytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                                var totalMemoryGB = Math.Round(totalMemoryBytes / (1024.0 * 1024.0 * 1024.0), 1);
                                systemInfo.Add($"🧠 Memoria total: {totalMemoryGB} GB");
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Could not retrieve total memory: {Error}", ex.Message);
                    systemInfo.Add("🧠 Memoria total: No disponible");
                }
                
                // Graphics Card Information
                try
                {
                    using (var searcher = new System.Management.ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                    {
                        var gpuIndex = 1;
                        foreach (System.Management.ManagementObject obj in searcher.Get())
                        {
                            var gpuName = obj["Name"]?.ToString();
                            if (!string.IsNullOrEmpty(gpuName) && !gpuName.Contains("Generic") && !gpuName.Contains("Basic"))
                            {
                                var gpuRamBytes = obj["AdapterRAM"];
                                if (gpuRamBytes != null && gpuRamBytes.ToString() != "0")
                                {
                                    var gpuRamGB = Math.Round(Convert.ToUInt64(gpuRamBytes) / (1024.0 * 1024.0 * 1024.0), 1);
                                    systemInfo.Add($"🎮 GPU {gpuIndex}: {gpuName} ({gpuRamGB} GB VRAM)");
                                }
                                else
                                {
                                    systemInfo.Add($"🎮 GPU {gpuIndex}: {gpuName}");
                                }
                                gpuIndex++;
                            }
                        }
                        
                        if (gpuIndex == 1)
                        {
                            systemInfo.Add("🎮 GPU: No detectado o información no disponible");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Could not retrieve GPU info: {Error}", ex.Message);
                    systemInfo.Add("🎮 GPU: No disponible");
                }
                
                // Display Information
                try
                {
                    using (var searcher = new System.Management.ManagementObjectSearcher("SELECT Name, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController"))
                    {
                        var displayIndex = 1;
                        foreach (System.Management.ManagementObject obj in searcher.Get())
                        {
                            var width = obj["CurrentHorizontalResolution"];
                            var height = obj["CurrentVerticalResolution"];
                            var refreshRate = obj["CurrentRefreshRate"];
                            
                            if (width != null && height != null && width.ToString() != "0" && height.ToString() != "0")
                            {
                                var resolution = $"{width}x{height}";
                                if (refreshRate != null && refreshRate.ToString() != "0")
                                {
                                    resolution += $" @ {refreshRate}Hz";
                                }
                                systemInfo.Add($"🖥️ Pantalla {displayIndex}: {resolution}");
                                displayIndex++;
                            }
                        }
                        
                        if (displayIndex == 1)
                        {
                            // Fallback to screen resolution if WMI fails
                            var primaryScreenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
                            var primaryScreenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
                            systemInfo.Add($"🖥️ Pantalla principal: {primaryScreenWidth}x{primaryScreenHeight}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Could not retrieve display info: {Error}", ex.Message);
                    // Fallback to basic screen info
                    var primaryScreenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
                    var primaryScreenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
                    systemInfo.Add($"🖥️ Pantalla principal: {primaryScreenWidth}x{primaryScreenHeight}");
                }
                
                // Windows Version Details
                try
                {
                    var windowsVersion = GetWindowsVersion();
                    if (!string.IsNullOrEmpty(windowsVersion))
                    {
                        systemInfo.Add($"🪟 Versión Windows: {windowsVersion}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Could not retrieve Windows version: {Error}", ex.Message);
                }
                
                // Power Plan Information (affects gamma ramp performance)
                try
                {
                    using (var searcher = new System.Management.ManagementObjectSearcher("root\\cimv2\\power", "SELECT ElementName, IsActive FROM Win32_PowerPlan"))
                    {
                        foreach (System.Management.ManagementObject obj in searcher.Get())
                        {
                            var isActive = obj["IsActive"];
                            if (isActive != null && (bool)isActive)
                            {
                                var powerPlan = obj["ElementName"]?.ToString() ?? "Desconocido";
                                systemInfo.Add($"🔋 Plan de energía: {powerPlan}");
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Could not retrieve power plan: {Error}", ex.Message);
                }
                
                // Application-specific information
                var appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
                systemInfo.Add($"📱 ChronoGuard: v{appVersion}");
                
                var processArchitecture = Environment.Is64BitProcess ? "64-bit" : "32-bit";
                systemInfo.Add($"🏗️ Proceso: {processArchitecture}");
                
                // Current user privileges
                var isAdmin = IsRunningAsAdministrator();
                var userContext = isAdmin ? "Administrador" : "Usuario estándar";
                systemInfo.Add($"👤 Contexto usuario: {userContext}");
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed system information");
                systemInfo.Add($"❌ Error obteniendo información del sistema: {ex.Message}");
            }
            
            return systemInfo;
        });
    }
    
    /// <summary>
    /// Gets detailed Windows version information
    /// </summary>
    private string GetWindowsVersion()
    {
        try
        {
            using (var searcher = new System.Management.ManagementObjectSearcher("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem"))
            {
                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    var caption = obj["Caption"]?.ToString() ?? "";
                    var version = obj["Version"]?.ToString() ?? "";
                    var buildNumber = obj["BuildNumber"]?.ToString() ?? "";
                    
                    if (!string.IsNullOrEmpty(caption))
                    {
                        if (!string.IsNullOrEmpty(buildNumber))
                        {
                            return $"{caption} (Build {buildNumber})";
                        }
                        return caption;
                    }
                    break;
                }
            }
        }
        catch
        {
            // Fallback to basic version if WMI fails
        }
        
        return Environment.OSVersion.ToString();
    }
}
