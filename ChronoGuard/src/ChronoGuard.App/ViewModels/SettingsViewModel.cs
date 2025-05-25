using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using static ChronoGuard.Domain.Interfaces.IConfigurationService;

namespace ChronoGuard.App.ViewModels;

/// <summary>
/// ViewModel for the settings window
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IProfileService _profileService;
    private readonly IConfigurationService _configurationService;
    private readonly ILocationService _locationService;

    [ObservableProperty]
    private ObservableCollection<ColorProfile> _profiles = new();

    [ObservableProperty]
    private ColorProfile? _selectedProfile;

    [ObservableProperty]
    private AppConfiguration _configuration = new();

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _checkForUpdates;

    [ObservableProperty]
    private string _selectedLanguage = "es-ES";

    [ObservableProperty]
    private LocationMethod _locationMethod;

    [ObservableProperty]
    private double? _manualLatitude;

    [ObservableProperty]
    private double? _manualLongitude;

    [ObservableProperty]
    private string? _manualCity;

    [ObservableProperty]
    private bool _allowIpLocation;

    [ObservableProperty]
    private bool _notificationsEnabled;

    [ObservableProperty]
    private NotificationLevel _notificationLevel;

    [ObservableProperty]
    private TimeSpan _quietHoursStart;

    [ObservableProperty]
    private TimeSpan _quietHoursEnd;

    [ObservableProperty]
    private bool _showTransitionNotifications;

    [ObservableProperty]
    private bool _showSleepReminders;

    [ObservableProperty]
    private ObservableCollection<string> _excludedApplications = new();

    [ObservableProperty]
    private bool _multiMonitorSupport;

    [ObservableProperty]
    private bool _useHardwareAcceleration;

    [ObservableProperty]
    private LogLevel _logLevel;

    [ObservableProperty]
    private string _newExcludedApp = "";

    public ObservableCollection<string> AvailableLanguages { get; } = new()
    {
        "es-ES", "en-US", "fr-FR", "de-DE", "it-IT", "pt-BR"
    };

    public Array LocationMethods => Enum.GetValues<LocationMethod>();
    public Array NotificationLevels => Enum.GetValues<NotificationLevel>();
    public Array LogLevels => Enum.GetValues<LogLevel>();

    public SettingsViewModel(
        ILogger<SettingsViewModel> logger,
        IProfileService profileService,
        IConfigurationService configurationService,
        ILocationService locationService)
    {
        _logger = logger;
        _profileService = profileService;
        _configurationService = configurationService;
        _locationService = locationService;

        // Subscribe to events
        _profileService.ProfilesChanged += OnProfilesChanged;

        // Load data
        _ = Task.Run(LoadDataAsync);
    }

    /// <summary>
    /// Saves all settings
    /// </summary>
    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            // Update configuration object
            Configuration.General.AutoStart = AutoStart;
            Configuration.General.MinimizeToTray = MinimizeToTray;
            Configuration.General.CheckForUpdates = CheckForUpdates;
            Configuration.General.Language = SelectedLanguage;

            Configuration.Location.Method = LocationMethod;
            Configuration.Location.ManualLatitude = ManualLatitude;
            Configuration.Location.ManualLongitude = ManualLongitude;
            Configuration.Location.ManualCity = ManualCity;
            Configuration.Location.AllowIpLocation = AllowIpLocation;

            Configuration.Notifications.Enabled = NotificationsEnabled;
            Configuration.Notifications.Level = NotificationLevel;
            Configuration.Notifications.QuietHoursStart = QuietHoursStart;
            Configuration.Notifications.QuietHoursEnd = QuietHoursEnd;
            Configuration.Notifications.ShowTransitionNotifications = ShowTransitionNotifications;
            Configuration.Notifications.ShowSleepReminders = ShowSleepReminders;

            Configuration.Advanced.ExcludedApplications = ExcludedApplications.ToList();
            Configuration.Advanced.MultiMonitorSupport = MultiMonitorSupport;
            Configuration.Advanced.UseHardwareAcceleration = UseHardwareAcceleration;
            Configuration.Advanced.LogLevel = LogLevel;

            // Save configuration
            await _configurationService.SaveConfigurationAsync(Configuration);

            // Set active profile if selected
            if (SelectedProfile != null)
            {
                await _profileService.SetActiveProfileAsync(SelectedProfile.Id);
            }

            _logger.LogInformation("Settings saved successfully");
            MessageBox.Show("Configuración guardada correctamente.", "ChronoGuard", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings");
            MessageBox.Show($"Error al guardar configuración:\n{ex.Message}", "ChronoGuard - Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Resets settings to defaults
    /// </summary>
    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        var result = MessageBox.Show(
            "¿Está seguro de que desea restablecer todas las configuraciones a los valores predeterminados?",
            "Confirmar restablecimiento",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _configurationService.ResetToDefaultsAsync();
                await LoadDataAsync();
                
                _logger.LogInformation("Settings reset to defaults");
                MessageBox.Show("Configuración restablecida a valores predeterminados.", "ChronoGuard", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting settings to defaults");
                MessageBox.Show($"Error al restablecer configuración:\n{ex.Message}", "ChronoGuard - Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Creates a new custom profile
    /// </summary>
    [RelayCommand]
    private async Task CreateProfileAsync()
    {
        try
        {
            var newProfile = new ColorProfile("Perfil personalizado", "Creado por el usuario", 6500, 3000);
            var profileId = await _profileService.SaveProfileAsync(newProfile);
            
            await LoadProfilesAsync();
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == profileId);
            
            _logger.LogInformation("New profile created: {ProfileName}", newProfile.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new profile");
            MessageBox.Show($"Error al crear perfil:\n{ex.Message}", "ChronoGuard - Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Duplicates the selected profile
    /// </summary>
    [RelayCommand]
    private async Task DuplicateProfileAsync()
    {
        if (SelectedProfile == null) return;

        try
        {
            var clonedProfile = SelectedProfile.Clone();
            var profileId = await _profileService.SaveProfileAsync(clonedProfile);
            
            await LoadProfilesAsync();
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == profileId);
            
            _logger.LogInformation("Profile duplicated: {ProfileName}", clonedProfile.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating profile");
            MessageBox.Show($"Error al duplicar perfil:\n{ex.Message}", "ChronoGuard - Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Deletes the selected profile
    /// </summary>
    [RelayCommand]
    private async Task DeleteProfileAsync()
    {
        if (SelectedProfile == null || SelectedProfile.IsBuiltIn) return;

        var result = MessageBox.Show(
            $"¿Está seguro de que desea eliminar el perfil '{SelectedProfile.Name}'?",
            "Confirmar eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _profileService.DeleteProfileAsync(SelectedProfile.Id);
                await LoadProfilesAsync();
                SelectedProfile = Profiles.FirstOrDefault();
                
                _logger.LogInformation("Profile deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting profile");
                MessageBox.Show($"Error al eliminar perfil:\n{ex.Message}", "ChronoGuard - Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Tests the current location settings
    /// </summary>
    [RelayCommand]
    private async Task TestLocationAsync()
    {
        try
        {
            var location = await _locationService.GetCurrentLocationAsync();
            if (location != null)
            {
                var message = $"Ubicación detectada:\n" +
                            $"Coordenadas: {location.Latitude:F2}°, {location.Longitude:F2}°\n" +
                            $"Ciudad: {location.City ?? "No disponible"}\n" +
                            $"País: {location.Country ?? "No disponible"}";
                
                MessageBox.Show(message, "Prueba de ubicación", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("No se pudo detectar la ubicación.", "Prueba de ubicación", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing location");
            MessageBox.Show($"Error al probar ubicación:\n{ex.Message}", "ChronoGuard - Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Adds an application to the exclusion list
    /// </summary>
    [RelayCommand]
    private void AddExcludedApplication()
    {
        if (string.IsNullOrWhiteSpace(NewExcludedApp)) return;

        if (!ExcludedApplications.Contains(NewExcludedApp, StringComparer.OrdinalIgnoreCase))
        {
            ExcludedApplications.Add(NewExcludedApp);
            NewExcludedApp = "";
        }
    }

    /// <summary>
    /// Removes an application from the exclusion list
    /// </summary>
    [RelayCommand]
    private void RemoveExcludedApplication(string appName)
    {
        ExcludedApplications.Remove(appName);
    }

    private async Task LoadDataAsync()
    {
        await LoadProfilesAsync();
        await LoadConfigurationAsync();
    }

    private async Task LoadProfilesAsync()
    {
        try
        {
            var profiles = await _profileService.GetProfilesAsync();
            var activeProfile = await _profileService.GetActiveProfileAsync();

            Application.Current.Dispatcher.Invoke(() =>
            {
                Profiles.Clear();
                foreach (var profile in profiles)
                {
                    Profiles.Add(profile);
                }

                SelectedProfile = activeProfile;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading profiles");
        }
    }

    private async Task LoadConfigurationAsync()
    {
        try
        {
            var config = await _configurationService.GetConfigurationAsync();

            Application.Current.Dispatcher.Invoke(() =>
            {
                Configuration = config;

                // General settings
                AutoStart = config.General.AutoStart;
                MinimizeToTray = config.General.MinimizeToTray;
                CheckForUpdates = config.General.CheckForUpdates;
                SelectedLanguage = config.General.Language;

                // Location settings
                LocationMethod = config.Location.Method;
                ManualLatitude = config.Location.ManualLatitude;
                ManualLongitude = config.Location.ManualLongitude;
                ManualCity = config.Location.ManualCity;
                AllowIpLocation = config.Location.AllowIpLocation;

                // Notification settings
                NotificationsEnabled = config.Notifications.Enabled;
                NotificationLevel = config.Notifications.Level;
                QuietHoursStart = config.Notifications.QuietHoursStart;
                QuietHoursEnd = config.Notifications.QuietHoursEnd;
                ShowTransitionNotifications = config.Notifications.ShowTransitionNotifications;
                ShowSleepReminders = config.Notifications.ShowSleepReminders;

                // Advanced settings
                ExcludedApplications.Clear();
                foreach (var app in config.Advanced.ExcludedApplications)
                {
                    ExcludedApplications.Add(app);
                }
                MultiMonitorSupport = config.Advanced.MultiMonitorSupport;
                UseHardwareAcceleration = config.Advanced.UseHardwareAcceleration;
                LogLevel = config.Advanced.LogLevel;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration");
        }
    }

    private void OnProfilesChanged(object? sender, EventArgs e)
    {
        _ = Task.Run(LoadProfilesAsync);
    }
}
