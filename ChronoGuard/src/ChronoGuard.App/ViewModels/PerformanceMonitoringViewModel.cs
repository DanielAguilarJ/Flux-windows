using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using ChronoGuard.App.Commands;
using Microsoft.Extensions.Logging;

namespace ChronoGuard.App.ViewModels;

/// <summary>
/// ViewModel for the Performance Monitoring view with real-time charts and controls
/// </summary>
public class PerformanceMonitoringViewModel : INotifyPropertyChanged
{
    private readonly IPerformanceMonitoringService _performanceService;
    private readonly ILogger<PerformanceMonitoringViewModel> _logger;
    private readonly DispatcherTimer _updateTimer;
    
    private SystemPerformanceMetrics? _currentMetrics;
    private bool _isMonitoring;
    private PerformanceLevel _currentPerformanceLevel;
    private string _statusMessage = "Initializing...";
    
    public event PropertyChangedEventHandler? PropertyChanged;

    public PerformanceMonitoringViewModel(
        IPerformanceMonitoringService performanceService,
        ILogger<PerformanceMonitoringViewModel> logger)
    {
        _performanceService = performanceService ?? throw new ArgumentNullException(nameof(performanceService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize collections
        MetricsHistory = new ObservableCollection<SystemPerformanceMetrics>();
        RecentAlerts = new ObservableCollection<PerformanceAlert>();
        
        // Initialize commands
        StartMonitoringCommand = new RelayCommand(async () => await StartMonitoringAsync(), () => !IsMonitoring);
        StopMonitoringCommand = new RelayCommand(async () => await StopMonitoringAsync(), () => IsMonitoring);
        SetPerformanceLevelCommand = new RelayCommand<PerformanceLevel>(async level => await SetPerformanceLevelAsync(level));
        ClearHistoryCommand = new RelayCommand(ClearHistory);
        ClearAlertsCommand = new RelayCommand(ClearAlerts);
        
        // Setup update timer for UI refresh
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500) // Update UI every 500ms
        };
        _updateTimer.Tick += UpdateTimerTick;
        
        // Subscribe to performance service events
        _performanceService.PerformanceLevelChanged += OnPerformanceLevelChanged;
        _performanceService.PerformanceAlertTriggered += OnPerformanceAlert;
        
        _ = Task.Run(InitializeAsync);
    }

    #region Properties

    /// <summary>
    /// Current system performance metrics
    /// </summary>
    public SystemPerformanceMetrics? CurrentMetrics
    {
        get => _currentMetrics;
        private set
        {
            _currentMetrics = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CpuUsageText));
            OnPropertyChanged(nameof(MemoryUsageText));
            OnPropertyChanged(nameof(GpuUsageText));
            OnPropertyChanged(nameof(ColorAdjustmentTimeText));
        }
    }

    /// <summary>
    /// Whether performance monitoring is currently active
    /// </summary>
    public bool IsMonitoring
    {
        get => _isMonitoring;
        private set
        {
            _isMonitoring = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MonitoringStatusText));
            (StartMonitoringCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (StopMonitoringCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Current performance level
    /// </summary>
    public PerformanceLevel CurrentPerformanceLevel
    {
        get => _currentPerformanceLevel;
        private set
        {
            _currentPerformanceLevel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PerformanceLevelText));
        }
    }

    /// <summary>
    /// Status message for the UI
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Historical performance metrics for charting
    /// </summary>
    public ObservableCollection<SystemPerformanceMetrics> MetricsHistory { get; }

    /// <summary>
    /// Recent performance alerts
    /// </summary>
    public ObservableCollection<PerformanceAlert> RecentAlerts { get; }

    #endregion

    #region Computed Properties

    public string CpuUsageText => CurrentMetrics?.CpuUsagePercent.ToString("F1") + "%" ?? "N/A";
    public string MemoryUsageText => CurrentMetrics?.MemoryUsagePercent.ToString("F1") + "%" ?? "N/A";
    public string GpuUsageText => CurrentMetrics?.GpuUsagePercent.ToString("F1") + "%" ?? "N/A";
    public string ColorAdjustmentTimeText => CurrentMetrics?.AverageColorAdjustmentTime.ToString("F1") + "ms" ?? "N/A";
    public string MonitoringStatusText => IsMonitoring ? "Active" : "Stopped";
    public string PerformanceLevelText => CurrentPerformanceLevel.ToString();

    #endregion

    #region Commands

    public ICommand StartMonitoringCommand { get; }
    public ICommand StopMonitoringCommand { get; }
    public ICommand SetPerformanceLevelCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand ClearAlertsCommand { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Initialize the view model
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            CurrentPerformanceLevel = _performanceService.CurrentPerformanceLevel;
            IsMonitoring = true; // Default to monitoring
            
            StatusMessage = "Ready";
            
            if (IsMonitoring)
            {
                _updateTimer.Start();
            }
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize performance monitoring view model");
            StatusMessage = "Initialization failed";
        }
    }

    /// <summary>
    /// Start performance monitoring
    /// </summary>
    private async Task StartMonitoringAsync()
    {
        try
        {
            StatusMessage = "Starting monitoring...";
            await _performanceService.StartMonitoringAsync();
            _updateTimer.Start();
            StatusMessage = "Monitoring started";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start performance monitoring");
            StatusMessage = "Failed to start monitoring";
        }
    }

    /// <summary>
    /// Stop performance monitoring
    /// </summary>
    private async Task StopMonitoringAsync()
    {
        try
        {
            StatusMessage = "Stopping monitoring...";
            await _performanceService.StopMonitoringAsync();
            _updateTimer.Stop();
            StatusMessage = "Monitoring stopped";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop performance monitoring");
            StatusMessage = "Failed to stop monitoring";
        }
    }

    /// <summary>
    /// Set the performance level
    /// </summary>
    private async Task SetPerformanceLevelAsync(PerformanceLevel level)
    {
        try
        {
            StatusMessage = $"Setting performance level to {level}...";
            await _performanceService.SetPerformanceLevelAsync(level);
            StatusMessage = $"Performance level set to {level}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set performance level to {Level}", level);
            StatusMessage = "Failed to set performance level";
        }
    }

    /// <summary>
    /// Clear metrics history
    /// </summary>
    private void ClearHistory()
    {
        MetricsHistory.Clear();
        StatusMessage = "History cleared";
    }

    /// <summary>
    /// Clear performance alerts
    /// </summary>
    private void ClearAlerts()
    {
        RecentAlerts.Clear();
        StatusMessage = "Alerts cleared";
    }

    /// <summary>
    /// Timer tick handler for updating UI
    /// </summary>
    private async void UpdateTimerTick(object? sender, EventArgs e)
    {
        try
        {
            var metrics = await _performanceService.GetCurrentMetricsAsync();
            
            // Update current metrics on UI thread
            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                CurrentMetrics = metrics;
                
                // Add to history (keep last 100 entries for performance)
                MetricsHistory.Add(metrics);
                while (MetricsHistory.Count > 100)
                {
                    MetricsHistory.RemoveAt(0);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update performance metrics");
        }
    }

    /// <summary>
    /// Handle performance level changed event
    /// </summary>
    private void OnPerformanceLevelChanged(object? sender, PerformanceLevel level)
    {
        App.Current.Dispatcher.InvokeAsync(() =>
        {
            CurrentPerformanceLevel = level;
            StatusMessage = $"Performance level changed to {level}";
        });
    }

    /// <summary>
    /// Handle performance alert event
    /// </summary>
    private void OnPerformanceAlert(object? sender, PerformanceAlert alert)
    {
        App.Current.Dispatcher.InvokeAsync(() =>
        {
            RecentAlerts.Insert(0, alert); // Add to beginning
            
            // Keep only last 20 alerts
            while (RecentAlerts.Count > 20)
            {
                RecentAlerts.RemoveAt(RecentAlerts.Count - 1);
            }
            
            StatusMessage = $"Alert: {alert.Message}";
        });
    }

    #endregion

    #region INotifyPropertyChanged

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _updateTimer?.Stop();
        // DispatcherTimer doesn't implement IDisposable, just stop it
        
        if (_performanceService != null)
        {
            _performanceService.PerformanceLevelChanged -= OnPerformanceLevelChanged;
            _performanceService.PerformanceAlertTriggered -= OnPerformanceAlert;
        }
    }

    #endregion
}
