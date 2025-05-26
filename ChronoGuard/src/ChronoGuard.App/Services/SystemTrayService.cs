using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Application.Services;
using ChronoGuard.Domain.Interfaces;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace ChronoGuard.App.Services;

/// <summary>
/// Service for managing system tray icon and interactions
/// </summary>
public class SystemTrayService : IDisposable
{
    private readonly ILogger<SystemTrayService> _logger;
    private readonly ChronoGuardBackgroundService _backgroundService;
    private readonly IProfileService _profileService;
    private readonly ILocationService _locationService;
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private bool _disposed = false;

    public SystemTrayService(
        ILogger<SystemTrayService> logger,
        ChronoGuardBackgroundService backgroundService,
        IProfileService profileService,
        ILocationService locationService)
    {
        _logger = logger;
        _backgroundService = backgroundService;
        _profileService = profileService;
        _locationService = locationService;

        InitializeSystemTray();
        SubscribeToEvents();
    }

    private void InitializeSystemTray()
    {
        try
        {
            _notifyIcon = new NotifyIcon
            {
                Text = "ChronoGuard - Cargando...",
                Visible = true
            };

            // Set initial icon
            UpdateIcon(GetCurrentAppState());

            // Create context menu
            CreateContextMenu();

            // Set up event handlers
            _notifyIcon.Click += OnTrayIconClick;
            _notifyIcon.DoubleClick += OnTrayIconDoubleClick;

            _logger.LogInformation("System tray icon initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize system tray icon");
        }
    }

    private void CreateContextMenu()
    {
        _contextMenu = new ContextMenuStrip();

        // Header with current status
        var headerItem = new ToolStripLabel("ChronoGuard")
        {
            Font = new Font("Segoe UI", 9, System.Drawing.FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 62, 80)
        };
        _contextMenu.Items.Add(headerItem);

        var statusItem = new ToolStripLabel("● 6500K - Activo")
        {
            Font = new Font("Segoe UI", 8),
            ForeColor = Color.FromArgb(52, 152, 219)
        };
        _contextMenu.Items.Add(statusItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Profiles submenu
        var profilesItem = new ToolStripMenuItem("📋 Perfiles");
        _contextMenu.Items.Add(profilesItem);

        // Add default profiles
        var classicProfile = new ToolStripMenuItem("Clásico");
        classicProfile.Click += (s, e) => SwitchToProfile("Clásico");
        profilesItem.DropDownItems.Add(classicProfile);

        var nightWorkProfile = new ToolStripMenuItem("Trabajo Nocturno");
        nightWorkProfile.Click += (s, e) => SwitchToProfile("Trabajo Nocturno");
        profilesItem.DropDownItems.Add(nightWorkProfile);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Quick actions
        var pauseItem = new ToolStripMenuItem("⏸ Pausar por 1 hora");
        pauseItem.Click += OnPauseForHour;
        _contextMenu.Items.Add(pauseItem);

        var disableItem = new ToolStripMenuItem("🚫 Desactivar hasta mañana");
        disableItem.Click += OnDisableUntilTomorrow;
        _contextMenu.Items.Add(disableItem);

        var pauseAppItem = new ToolStripMenuItem("📱 Pausar para esta app");
        pauseAppItem.Click += OnPauseForCurrentApp;
        _contextMenu.Items.Add(pauseAppItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Settings and help
        var settingsItem = new ToolStripMenuItem("⚙️ Configuración...");
        settingsItem.Click += OnOpenSettings;
        _contextMenu.Items.Add(settingsItem);

        var helpItem = new ToolStripMenuItem("❓ Ayuda y soporte");
        helpItem.Click += OnOpenHelp;
        _contextMenu.Items.Add(helpItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Exit
        var exitItem = new ToolStripMenuItem("🚪 Salir de ChronoGuard");
        exitItem.Click += OnExit;
        _contextMenu.Items.Add(exitItem);

        _notifyIcon!.ContextMenuStrip = _contextMenu;
    }

    private void SubscribeToEvents()
    {
        _backgroundService.StateChanged += OnBackgroundServiceStateChanged;
        _profileService.ActiveProfileChanged += OnActiveProfileChanged;
        _locationService.LocationChanged += OnLocationChanged;
    }

    private void OnBackgroundServiceStateChanged(object? sender, AppState state)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            UpdateIcon(state);
            UpdateTooltip(state);
            UpdateContextMenuStatus(state);
        });
    }

    private void OnActiveProfileChanged(object? sender, ColorProfile profile)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            UpdateContextMenuStatus(GetCurrentAppState());
        });
    }

    private void OnLocationChanged(object? sender, Location location)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            UpdateTooltip(GetCurrentAppState());
        });
    }

    private void UpdateIcon(AppState state)
    {
        if (_notifyIcon == null) return;

        try
        {
            // Load the appropriate icon based on state
            var iconPath = GetIconPath(state);
            if (File.Exists(iconPath))
            {
                _notifyIcon.Icon = new Icon(iconPath);
            }
            else
            {
                // Fallback to embedded icon
                _notifyIcon.Icon = LoadEmbeddedIcon();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update tray icon");
        }
    }

    private string GetIconPath(AppState state)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var assetsDir = Path.Combine(baseDir, "Assets");

        if (state.IsPaused)
            return Path.Combine(assetsDir, "chronoguard-paused.ico");

        // Based on temperature, choose appropriate icon
        if (state.CurrentColorTemperature >= 5500)
            return Path.Combine(assetsDir, "chronoguard-day.ico");
        else if (state.CurrentColorTemperature >= 3500)
            return Path.Combine(assetsDir, "chronoguard-transition.ico");
        else
            return Path.Combine(assetsDir, "chronoguard-night.ico");
    }

    private Icon LoadEmbeddedIcon()
    {
        // For now, use the default icon
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "chronoguard.ico");
        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        // Create a simple default icon if file is missing
        var bitmap = new Bitmap(16, 16);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.FillEllipse(Brushes.Orange, 2, 2, 12, 12);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void UpdateTooltip(AppState state)
    {
        if (_notifyIcon == null) return;

        try
        {
            var status = state.IsPaused ? "Pausado" : "Activo";
            var temperature = $"{state.CurrentColorTemperature}K";
            
            var nextTransition = "";
            if (state.NextTransitionTime.HasValue && !state.IsPaused)
            {
                var timeUntil = state.NextTransitionTime.Value - DateTime.Now;
                if (timeUntil.TotalMinutes > 60)
                    nextTransition = $"\nPróxima transición: {timeUntil.Hours}h {timeUntil.Minutes}m";
                else if (timeUntil.TotalMinutes > 0)
                    nextTransition = $"\nPróxima transición: {timeUntil.Minutes}m";
            }

            _notifyIcon.Text = $"ChronoGuard - {status}\n{temperature}{nextTransition}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update tooltip");
            _notifyIcon.Text = "ChronoGuard";
        }
    }

    private void UpdateContextMenuStatus(AppState state)
    {
        if (_contextMenu?.Items.Count < 2) return;

        try
        {
            if (_contextMenu == null || _contextMenu.Items.Count < 2) return;
            if (!(_contextMenu.Items[1] is ToolStripLabel statusItem)) return;
            string stateIndicator = state.IsPaused ? "⏸" : "●";
            string temperature = $"{state.CurrentColorTemperature}K";
            string statusText = state.IsPaused ? "Pausado" : "Activo";
            statusItem.Text = $"{stateIndicator} {temperature} - {statusText}";
            statusItem.ForeColor = state.IsPaused ? 
                Color.FromArgb(231, 76, 60) : 
                Color.FromArgb(39, 174, 96);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update context menu status");
        }
    }

    private AppState GetCurrentAppState()
    {
        return _backgroundService.CurrentState ?? new AppState
        {
            IsEnabled = true,
            IsPaused = false,
            ActiveProfileId = "classic",
            CurrentTemperature = new ColorTemperature(6500),
            LastUpdate = DateTime.UtcNow
        };
    }

    // Event handlers
    private void OnTrayIconClick(object? sender, EventArgs e)
    {
        if (e is MouseEventArgs mouseArgs && mouseArgs.Button == MouseButtons.Left)
        {
            // Left click - Quick toggle
            _ = Task.Run(async () =>
            {
                try
                {
                    var state = GetCurrentAppState();
                    if (state.IsPaused)
                        await _backgroundService.ResumeAsync();
                    else
                        await _backgroundService.PauseAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to toggle ChronoGuard from tray");
                }
            });
        }
    }

    private void OnTrayIconDoubleClick(object? sender, EventArgs e)
    {
        // Double click - Show main window
        try
        {
            var mainWindow = System.Windows.Application.Current?.MainWindow as MainWindow;
            mainWindow?.ShowAndActivate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show main window from tray");
        }
    }

    private async void SwitchToProfile(string profileName)
    {
        try
        {
            var profiles = await _profileService.GetProfilesAsync();
            var profile = profiles.FirstOrDefault(p => p.Name == profileName);
            if (profile != null)
            {
                await _profileService.SetActiveProfileAsync(profile.Id);
                _logger.LogInformation("Switched to profile: {ProfileName}", profileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch to profile: {ProfileName}", profileName);
        }
    }

    private async void OnPauseForHour(object? sender, EventArgs e)
    {
        try
        {
            await _backgroundService.PauseAsync();
            // TODO: Implement timed resume after 1 hour
            _logger.LogInformation("Paused ChronoGuard for 1 hour");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause for 1 hour");
        }
    }

    private async void OnDisableUntilTomorrow(object? sender, EventArgs e)
    {
        try
        {
            await _backgroundService.PauseAsync();
            // TODO: Implement resume at sunrise tomorrow
            _logger.LogInformation("Disabled ChronoGuard until tomorrow");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable until tomorrow");
        }
    }

    private void OnPauseForCurrentApp(object? sender, EventArgs e)
    {
        try
        {
            // TODO: Implement app-specific pause functionality
            _logger.LogInformation("Pause for current app requested (not implemented yet)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause for current app");
        }
    }

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        try
        {
            var settingsWindow = App.ServiceProvider?.GetService(typeof(SettingsWindow)) as SettingsWindow;
            settingsWindow?.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open settings from tray");
        }
    }

    private void OnOpenHelp(object? sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/chronoguard/chronoguard/wiki",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open help");
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        try
        {
            System.Windows.Application.Current?.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exit application");
        }
    }

    public void ShowNotification(string title, string message, int timeoutMs = 3000)
    {
        try
        {
            _notifyIcon?.ShowBalloonTip(timeoutMs, title, message, ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show notification");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _backgroundService.StateChanged -= OnBackgroundServiceStateChanged;
            _profileService.ActiveProfileChanged -= OnActiveProfileChanged;
            _locationService.LocationChanged -= OnLocationChanged;

            _notifyIcon?.Dispose();
            _contextMenu?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing SystemTrayService");
        }

        _disposed = true;
    }
}

// NOTA: Para la inicialización de AppState, asegúrate de no asignar a propiedades de solo lectura. Si AppState es un record o clase con propiedades init, usa el constructor adecuado o un método de inicialización.
