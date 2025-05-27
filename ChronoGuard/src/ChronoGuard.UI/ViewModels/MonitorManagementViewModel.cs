using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using ChronoGuard.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ChronoGuard.UI.ViewModels;

/// <summary>
/// ViewModel for advanced monitor management interface
/// Provides comprehensive monitor detection, configuration, and calibration capabilities
/// </summary>
public class MonitorManagementViewModel : INotifyPropertyChanged
{
    private readonly IMonitorDetectionService _monitorDetectionService;
    private readonly IColorTemperatureService _colorTemperatureService;
    private readonly ICCProfileService _iccProfileService;
    private readonly ILogger<MonitorManagementViewModel> _logger;

    private bool _isDetecting;
    private bool _isRefreshing;
    private MonitorHardwareInfo? _selectedMonitor;
    private string _statusMessage = "Ready";
    private bool _showAdvancedInfo;
    private bool _showCapabilities;
    private bool _showEDIDInfo;

    public MonitorManagementViewModel(
        IMonitorDetectionService monitorDetectionService,
        IColorTemperatureService colorTemperatureService,
        ICCProfileService iccProfileService,
        ILogger<MonitorManagementViewModel> logger)
    {
        _monitorDetectionService = monitorDetectionService;
        _colorTemperatureService = colorTemperatureService;
        _iccProfileService = iccProfileService;
        _logger = logger;

        DetectedMonitors = new ObservableCollection<MonitorHardwareInfo>();
        
        // Initialize commands
        RefreshMonitorsCommand = new RelayCommand(async () => await RefreshMonitorsAsync(), () => !IsRefreshing);
        DetectMonitorsCommand = new RelayCommand(async () => await DetectMonitorsAsync(), () => !IsDetecting);
        CalibrateMonitorCommand = new RelayCommand<MonitorHardwareInfo>(async (monitor) => await CalibrateMonitorAsync(monitor), (monitor) => monitor != null);
        TestColorTemperatureCommand = new RelayCommand<string>(async (temp) => await TestColorTemperatureAsync(temp), (temp) => SelectedMonitor != null);
        ResetMonitorCommand = new RelayCommand<MonitorHardwareInfo>(async (monitor) => await ResetMonitorAsync(monitor), (monitor) => monitor != null);
        ExportMonitorInfoCommand = new RelayCommand<MonitorHardwareInfo>(async (monitor) => await ExportMonitorInfoAsync(monitor), (monitor) => monitor != null);
        ViewEDIDCommand = new RelayCommand<MonitorHardwareInfo>(async (monitor) => await ViewEDIDInfoAsync(monitor), (monitor) => monitor != null);

        // Auto-detect monitors on startup
        _ = Task.Run(async () => await DetectMonitorsAsync());
    }

    #region Properties

    /// <summary>
    /// Collection of detected monitors
    /// </summary>
    public ObservableCollection<MonitorHardwareInfo> DetectedMonitors { get; }

    /// <summary>
    /// Currently selected monitor
    /// </summary>
    public MonitorHardwareInfo? SelectedMonitor
    {
        get => _selectedMonitor;
        set
        {
            if (SetProperty(ref _selectedMonitor, value))
            {
                OnPropertyChanged(nameof(HasSelectedMonitor));
                OnPropertyChanged(nameof(SelectedMonitorSummary));
                OnPropertyChanged(nameof(SelectedMonitorCapabilities));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>
    /// Whether a monitor is currently selected
    /// </summary>
    public bool HasSelectedMonitor => SelectedMonitor != null;

    /// <summary>
    /// Summary of selected monitor
    /// </summary>
    public string SelectedMonitorSummary => SelectedMonitor?.GetDisplaySummary() ?? "No monitor selected";

    /// <summary>
    /// Capabilities of selected monitor
    /// </summary>
    public string SelectedMonitorCapabilities => SelectedMonitor?.GetCapabilitiesSummary() ?? "";

    /// <summary>
    /// Whether monitor detection is in progress
    /// </summary>
    public bool IsDetecting
    {
        get => _isDetecting;
        set => SetProperty(ref _isDetecting, value);
    }

    /// <summary>
    /// Whether monitor refresh is in progress
    /// </summary>
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    /// <summary>
    /// Current status message
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Whether to show advanced monitor information
    /// </summary>
    public bool ShowAdvancedInfo
    {
        get => _showAdvancedInfo;
        set => SetProperty(ref _showAdvancedInfo, value);
    }

    /// <summary>
    /// Whether to show monitor capabilities
    /// </summary>
    public bool ShowCapabilities
    {
        get => _showCapabilities;
        set => SetProperty(ref _showCapabilities, value);
    }

    /// <summary>
    /// Whether to show EDID information
    /// </summary>
    public bool ShowEDIDInfo
    {
        get => _showEDIDInfo;
        set => SetProperty(ref _showEDIDInfo, value);
    }

    /// <summary>
    /// Number of detected monitors
    /// </summary>
    public int MonitorCount => DetectedMonitors.Count;

    /// <summary>
    /// Primary monitor information
    /// </summary>
    public MonitorHardwareInfo? PrimaryMonitor => DetectedMonitors.FirstOrDefault(m => m.IsPrimary);

    /// <summary>
    /// Total screen resolution (all monitors combined)
    /// </summary>
    public string TotalResolution
    {
        get
        {
            if (!DetectedMonitors.Any()) return "No monitors detected";
            
            var totalWidth = DetectedMonitors.Sum(m => m.CurrentWidth);
            var totalHeight = DetectedMonitors.Max(m => m.CurrentHeight);
            return $"{totalWidth} x {totalHeight} (Combined)";
        }
    }

    #endregion

    #region Commands

    /// <summary>
    /// Command to refresh monitor list
    /// </summary>
    public ICommand RefreshMonitorsCommand { get; }

    /// <summary>
    /// Command to detect monitors
    /// </summary>
    public ICommand DetectMonitorsCommand { get; }

    /// <summary>
    /// Command to calibrate a monitor
    /// </summary>
    public ICommand CalibrateMonitorCommand { get; }

    /// <summary>
    /// Command to test color temperature
    /// </summary>
    public ICommand TestColorTemperatureCommand { get; }

    /// <summary>
    /// Command to reset monitor settings
    /// </summary>
    public ICommand ResetMonitorCommand { get; }

    /// <summary>
    /// Command to export monitor information
    /// </summary>
    public ICommand ExportMonitorInfoCommand { get; }

    /// <summary>
    /// Command to view EDID information
    /// </summary>
    public ICommand ViewEDIDCommand { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Detect all monitors with comprehensive information
    /// </summary>
    private async Task DetectMonitorsAsync()
    {
        if (IsDetecting) return;

        IsDetecting = true;
        StatusMessage = "Detecting monitors...";

        try
        {
            _logger.LogInformation("Starting monitor detection");
            
            var monitors = await _monitorDetectionService.DetectMonitorsAsync();
            
            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                DetectedMonitors.Clear();
                foreach (var monitor in monitors)
                {
                    monitor.PerformHealthCheck();
                    DetectedMonitors.Add(monitor);
                }

                // Select primary monitor by default
                SelectedMonitor = PrimaryMonitor ?? DetectedMonitors.FirstOrDefault();
                
                OnPropertyChanged(nameof(MonitorCount));
                OnPropertyChanged(nameof(PrimaryMonitor));
                OnPropertyChanged(nameof(TotalResolution));
            });

            StatusMessage = $"Detected {monitors.Count()} monitors";
            _logger.LogInformation("Successfully detected {Count} monitors", monitors.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect monitors");
            StatusMessage = "Failed to detect monitors";
        }
        finally
        {
            IsDetecting = false;
        }
    }

    /// <summary>
    /// Refresh monitor information
    /// </summary>
    private async Task RefreshMonitorsAsync()
    {
        if (IsRefreshing) return;

        IsRefreshing = true;
        StatusMessage = "Refreshing monitor information...";

        try
        {
            // Clear cache to force re-detection
            _monitorDetectionService.ClearCache();
            
            await DetectMonitorsAsync();
            
            StatusMessage = "Monitor information refreshed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh monitors");
            StatusMessage = "Failed to refresh monitor information";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// Calibrate selected monitor
    /// </summary>
    private async Task CalibrateMonitorAsync(MonitorHardwareInfo? monitor)
    {
        if (monitor == null) return;

        StatusMessage = $"Calibrating {monitor.MonitorName}...";

        try
        {
            _logger.LogInformation("Starting calibration for monitor {MonitorName}", monitor.MonitorName);
            
            // Use device path as monitor identifier for calibration
            var success = await _colorTemperatureService.CalibrateMonitorAsync(monitor.DevicePath);
            
            if (success)
            {
                StatusMessage = $"Successfully calibrated {monitor.MonitorName}";
                _logger.LogInformation("Successfully calibrated monitor {MonitorName}", monitor.MonitorName);
            }
            else
            {
                StatusMessage = $"Calibration failed for {monitor.MonitorName}";
                _logger.LogWarning("Calibration failed for monitor {MonitorName}", monitor.MonitorName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calibrating monitor {MonitorName}", monitor.MonitorName);
            StatusMessage = $"Error calibrating {monitor.MonitorName}";
        }
    }

    /// <summary>
    /// Test color temperature on selected monitor
    /// </summary>
    private async Task TestColorTemperatureAsync(string temperatureString)
    {
        if (SelectedMonitor == null || !int.TryParse(temperatureString, out var temperature))
            return;

        StatusMessage = $"Applying {temperature}K to {SelectedMonitor.MonitorName}...";

        try
        {
            var colorTemp = new ColorTemperature(temperature);
            var success = await _colorTemperatureService.ApplyTemperatureToMonitorAsync(SelectedMonitor.DevicePath, colorTemp);
            
            if (success)
            {
                StatusMessage = $"Applied {temperature}K to {SelectedMonitor.MonitorName}";
            }
            else
            {
                StatusMessage = $"Failed to apply temperature to {SelectedMonitor.MonitorName}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying temperature to monitor {MonitorName}", SelectedMonitor.MonitorName);
            StatusMessage = $"Error applying temperature to {SelectedMonitor.MonitorName}";
        }
    }

    /// <summary>
    /// Reset monitor to default settings
    /// </summary>
    private async Task ResetMonitorAsync(MonitorHardwareInfo? monitor)
    {
        if (monitor == null) return;

        StatusMessage = $"Resetting {monitor.MonitorName}...";

        try
        {
            await _colorTemperatureService.ResetToDefaultAsync();
            StatusMessage = $"Reset {monitor.MonitorName} to default settings";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting monitor {MonitorName}", monitor.MonitorName);
            StatusMessage = $"Error resetting {monitor.MonitorName}";
        }
    }

    /// <summary>
    /// Export monitor information to file
    /// </summary>
    private async Task ExportMonitorInfoAsync(MonitorHardwareInfo? monitor)
    {
        if (monitor == null) return;

        try
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "json",
                FileName = $"MonitorInfo_{monitor.ManufacturerName}_{monitor.ModelName}_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(monitor, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                await File.WriteAllTextAsync(saveDialog.FileName, json);
                StatusMessage = $"Exported monitor information to {saveDialog.FileName}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting monitor information");
            StatusMessage = "Error exporting monitor information";
        }
    }

    /// <summary>
    /// View detailed EDID information
    /// </summary>
    private async Task ViewEDIDInfoAsync(MonitorHardwareInfo? monitor)
    {
        if (monitor == null) return;

        try
        {
            var edidInfo = await _monitorDetectionService.GetMonitorEDIDAsync(monitor.DevicePath);
            
            if (edidInfo != null)
            {
                // Here you would typically show a dialog with EDID information
                // For now, we'll just update the status
                StatusMessage = $"EDID: {edidInfo.ManufacturerID} {edidInfo.ProductCode} ({edidInfo.YearOfManufacture})";
            }
            else
            {
                StatusMessage = "EDID information not available for this monitor";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EDID information");
            StatusMessage = "Error retrieving EDID information";
        }
    }

    /// <summary>
    /// Get health status color for UI binding
    /// </summary>
    public string GetHealthStatusColor(MonitorHealthStatus status)
    {
        return status switch
        {
            MonitorHealthStatus.Healthy => "Green",
            MonitorHealthStatus.Info => "Blue",
            MonitorHealthStatus.Warning => "Orange",
            MonitorHealthStatus.Error => "Red",
            MonitorHealthStatus.Critical => "DarkRed",
            _ => "Gray"
        };
    }

    /// <summary>
    /// Get formatted resolution string
    /// </summary>
    public string GetResolutionString(MonitorHardwareInfo monitor)
    {
        if (monitor.CurrentWidth == monitor.NativeWidth && monitor.CurrentHeight == monitor.NativeHeight)
        {
            return $"{monitor.CurrentWidth} x {monitor.CurrentHeight} (Native)";
        }
        
        return $"{monitor.CurrentWidth} x {monitor.CurrentHeight} (Scaled from {monitor.NativeWidth} x {monitor.NativeHeight})";
    }

    /// <summary>
    /// Get connection type with icon
    /// </summary>
    public string GetConnectionTypeIcon(string connectionType)
    {
        return connectionType.ToLowerInvariant() switch
        {
            "hdmi" => "🔌",
            "displayport" => "🖥️",
            "dvi" => "📺",
            "vga" => "📻",
            "usb-c" => "🔗",
            "thunderbolt" => "⚡",
            _ => "🖱️"
        };
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}

/// <summary>
/// Simple relay command implementation
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Func<Task> _executeAsync;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        await _executeAsync();
    }
}

/// <summary>
/// Generic relay command implementation
/// </summary>
public class RelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _executeAsync;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Func<T?, Task> executeAsync, Func<T?, bool>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

    public async void Execute(object? parameter)
    {
        await _executeAsync((T?)parameter);
    }
}
