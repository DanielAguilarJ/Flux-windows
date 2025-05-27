using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Management;
using ChronoGuard.Domain.Interfaces;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Configuration;
using System.Collections.Concurrent;
using Timer = System.Threading.Timer;

namespace ChronoGuard.Infrastructure.Services;

/// <summary>
/// Advanced real-time performance monitoring and optimization service
/// Monitors system performance, memory usage, GPU load, and color adjustment efficiency
/// </summary>
public class PerformanceMonitoringService : IPerformanceMonitoringService
{
    private readonly ILogger<PerformanceMonitoringService> _logger;
    private readonly IConfigurationService _configurationService;
    
    private readonly Timer _monitoringTimer;
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memoryCounter;
    private readonly PerformanceCounter _diskCounter;
    private readonly ConcurrentQueue<PerformanceSnapshot> _performanceHistory;
    
    private readonly object _lock = new();
    private bool _disposed = false;
    private bool _isMonitoring = false;
    
    // GPU monitoring (if available)
    private PerformanceCounter? _gpuUsageCounter;
    private PerformanceCounter? _gpuMemoryCounter;
    private PerformanceCounter? _gpuEngineCounter;
    
    // Process-specific monitoring
    private Process? _currentProcess;
    private readonly Dictionary<string, PerformanceCounter> _processCounters = new();
    
    // Adaptive performance settings
    private PerformanceLevel _currentPerformanceLevel = PerformanceLevel.Balanced;
    private DateTime _lastPerformanceAdjustment = DateTime.MinValue;
    private readonly TimeSpan _performanceAdjustmentCooldown = TimeSpan.FromMinutes(2);
    
    // Performance statistics
    private double _averageCpuUsage = 0;
    private double _averageMemoryUsage = 0;
    private double _averageColorAdjustmentTime = 0;
    private int _colorAdjustmentCount = 0;
    
    // Interface implementation properties and events
    public PerformanceLevel CurrentPerformanceLevel => _currentPerformanceLevel;
    
    public event EventHandler<PerformanceLevel>? PerformanceLevelChanged;
    public event EventHandler<PerformanceAlert>? PerformanceAlertTriggered;
    public event EventHandler<SystemPerformanceMetrics>? MetricsUpdated;
    
    // Existing events (renamed for consistency)
    public event EventHandler<PerformanceAlert>? PerformanceAlertRaised;
    public event EventHandler<PerformanceOptimization>? OptimizationApplied;

    public PerformanceMonitoringService(
        ILogger<PerformanceMonitoringService> logger,
        IConfigurationService configurationService)
    {
        _logger = logger;
        _configurationService = configurationService;
        _performanceHistory = new ConcurrentQueue<PerformanceSnapshot>();
        
        // Initialize performance counters
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
        _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
        
        _currentProcess = Process.GetCurrentProcess();
        
        InitializeGpuCounters();
        InitializeProcessCounters();
        
        // Start monitoring timer (every 5 seconds)
        _monitoringTimer = new Timer(MonitorPerformance, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        
        _logger.LogInformation("Performance monitoring service initialized");
    }

    /// <summary>
    /// Initializes GPU performance counters if available
    /// </summary>
    private void InitializeGpuCounters()
    {
        try
        {
            // Try to initialize GPU performance counters
            var categoryNames = PerformanceCounterCategory.GetCategories()
                .Select(cat => cat.CategoryName)
                .ToList();

            // NVIDIA GPU monitoring
            if (categoryNames.Contains("GPU Engine"))
            {
                var instances = new PerformanceCounterCategory("GPU Engine").GetInstanceNames();
                var gpuInstance = instances.FirstOrDefault(i => i.Contains("engtype_3D"));
                
                if (!string.IsNullOrEmpty(gpuInstance))
                {
                    _gpuEngineCounter = new PerformanceCounter("GPU Engine", "Utilization Percentage", gpuInstance);
                    _logger.LogInformation("NVIDIA GPU performance counter initialized: {Instance}", gpuInstance);
                }
            }

            // AMD GPU monitoring
            if (categoryNames.Contains("GPU Process Memory"))
            {
                _gpuMemoryCounter = new PerformanceCounter("GPU Process Memory", "Local Usage", "_Total");
                _logger.LogInformation("AMD GPU memory counter initialized");
            }

            // Intel integrated graphics
            if (categoryNames.Contains("Intel(R) Graphics Driver"))
            {
                _gpuUsageCounter = new PerformanceCounter("Intel(R) Graphics Driver", "3D Usage", "");
                _logger.LogInformation("Intel GPU performance counter initialized");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not initialize GPU performance counters - GPU monitoring will be limited");
        }
    }

    /// <summary>
    /// Initializes process-specific performance counters
    /// </summary>
    private void InitializeProcessCounters()
    {
        try
        {
            if (_currentProcess != null)
            {
                var processName = _currentProcess.ProcessName;
                
                _processCounters["CPU"] = new PerformanceCounter("Process", "% Processor Time", processName);
                _processCounters["Memory"] = new PerformanceCounter("Process", "Working Set - Private", processName);
                _processCounters["Handles"] = new PerformanceCounter("Process", "Handle Count", processName);
                _processCounters["Threads"] = new PerformanceCounter("Process", "Thread Count", processName);
                
                _logger.LogInformation("Process-specific performance counters initialized for: {ProcessName}", processName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not initialize process-specific performance counters");
        }
    }

    /// <summary>
    /// Main monitoring method called by timer
    /// </summary>
    private void MonitorPerformance(object? state)
    {
        if (_disposed || _isMonitoring) return;

        lock (_lock)
        {
            if (_isMonitoring) return;
            _isMonitoring = true;
        }

        try
        {
            var snapshot = CollectPerformanceSnapshot();
            ProcessPerformanceSnapshot(snapshot);
            
            // Keep only last 720 snapshots (1 hour at 5-second intervals)
            while (_performanceHistory.Count > 720)
            {
                _performanceHistory.TryDequeue(out _);
            }
            
            _performanceHistory.Enqueue(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during performance monitoring");
        }
        finally
        {
            lock (_lock)
            {
                _isMonitoring = false;
            }
        }
    }

    /// <summary>
    /// Collects a comprehensive performance snapshot
    /// </summary>
    private PerformanceSnapshot CollectPerformanceSnapshot()
    {
        var snapshot = new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            CpuUsagePercent = GetCounterValue(_cpuCounter),
            AvailableMemoryMB = GetCounterValue(_memoryCounter),
            DiskUsagePercent = GetCounterValue(_diskCounter)
        };

        // Calculate total memory and memory usage percentage
        var totalMemoryMB = GetTotalPhysicalMemoryMB();
        if (totalMemoryMB > 0)
        {
            snapshot.TotalMemoryMB = totalMemoryMB;
            snapshot.MemoryUsagePercent = Math.Max(0, (totalMemoryMB - snapshot.AvailableMemoryMB) / totalMemoryMB * 100);
        }

        // GPU metrics
        snapshot.GpuUsagePercent = GetCounterValue(_gpuEngineCounter ?? _gpuUsageCounter);
        snapshot.GpuMemoryUsageMB = GetCounterValue(_gpuMemoryCounter) / (1024 * 1024); // Convert to MB

        // Process-specific metrics
        if (_processCounters.TryGetValue("CPU", out var processCpuCounter))
            snapshot.ProcessCpuPercent = GetCounterValue(processCpuCounter);
        
        if (_processCounters.TryGetValue("Memory", out var processMemoryCounter))
            snapshot.ProcessMemoryMB = GetCounterValue(processMemoryCounter) / (1024 * 1024); // Convert to MB
        
        if (_processCounters.TryGetValue("Handles", out var handlesCounter))
            snapshot.ProcessHandleCount = (int)GetCounterValue(handlesCounter);
        
        if (_processCounters.TryGetValue("Threads", out var threadsCounter))
            snapshot.ProcessThreadCount = (int)GetCounterValue(threadsCounter);

        // Color adjustment performance
        snapshot.AverageColorAdjustmentTime = _averageColorAdjustmentTime;
        snapshot.ColorAdjustmentCount = _colorAdjustmentCount;

        // System load assessment
        snapshot.SystemLoadLevel = CalculateSystemLoadLevel(snapshot);

        return snapshot;
    }

    /// <summary>
    /// Safely gets performance counter value
    /// </summary>
    private float GetCounterValue(PerformanceCounter? counter)
    {
        if (counter == null) return 0f;

        try
        {
            return counter.NextValue();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get performance counter value for {CounterName}", counter.CounterName);
            return 0f;
        }
    }

    /// <summary>
    /// Gets total physical memory in MB
    /// </summary>
    private double GetTotalPhysicalMemoryMB()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            using var collection = searcher.Get();
            
            foreach (ManagementObject obj in collection)
            {
                if (obj["TotalPhysicalMemory"] is ulong totalBytes)
                {
                    return totalBytes / (1024.0 * 1024.0); // Convert to MB
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not determine total physical memory");
        }

        return 0;
    }

    /// <summary>
    /// Calculates overall system load level
    /// </summary>
    private SystemLoadLevel CalculateSystemLoadLevel(PerformanceSnapshot snapshot)
    {
        var loadScore = 0;
        
        // CPU load (40% weight)
        if (snapshot.CpuUsagePercent > 80) loadScore += 40;
        else if (snapshot.CpuUsagePercent > 60) loadScore += 30;
        else if (snapshot.CpuUsagePercent > 40) loadScore += 20;
        else if (snapshot.CpuUsagePercent > 20) loadScore += 10;

        // Memory load (30% weight)
        if (snapshot.MemoryUsagePercent > 90) loadScore += 30;
        else if (snapshot.MemoryUsagePercent > 80) loadScore += 25;
        else if (snapshot.MemoryUsagePercent > 70) loadScore += 20;
        else if (snapshot.MemoryUsagePercent > 60) loadScore += 15;
        else if (snapshot.MemoryUsagePercent > 50) loadScore += 10;

        // GPU load (20% weight)
        if (snapshot.GpuUsagePercent > 90) loadScore += 20;
        else if (snapshot.GpuUsagePercent > 70) loadScore += 15;
        else if (snapshot.GpuUsagePercent > 50) loadScore += 10;
        else if (snapshot.GpuUsagePercent > 30) loadScore += 5;

        // Disk load (10% weight)
        if (snapshot.DiskUsagePercent > 90) loadScore += 10;
        else if (snapshot.DiskUsagePercent > 70) loadScore += 8;
        else if (snapshot.DiskUsagePercent > 50) loadScore += 5;

        return loadScore switch
        {
            >= 80 => SystemLoadLevel.Critical,
            >= 60 => SystemLoadLevel.High,
            >= 40 => SystemLoadLevel.Medium,
            >= 20 => SystemLoadLevel.Low,
            _ => SystemLoadLevel.Minimal
        };
    }

    /// <summary>
    /// Processes performance snapshot and applies optimizations
    /// </summary>
    private void ProcessPerformanceSnapshot(PerformanceSnapshot snapshot)
    {
        // Update running averages
        UpdateRunningAverages(snapshot);

        // Check for performance alerts
        CheckPerformanceAlerts(snapshot);

        // Apply adaptive optimizations
        ApplyAdaptiveOptimizations(snapshot);

        _logger.LogDebug("Performance snapshot processed - CPU: {Cpu:F1}%, Memory: {Memory:F1}%, Load: {Load}",
            snapshot.CpuUsagePercent, snapshot.MemoryUsagePercent, snapshot.SystemLoadLevel);
    }

    /// <summary>
    /// Updates running averages for performance metrics
    /// </summary>
    private void UpdateRunningAverages(PerformanceSnapshot snapshot)
    {
        const int maxSamples = 60; // 5 minutes of samples
        var recentSnapshots = _performanceHistory.TakeLast(maxSamples).ToList();
        
        if (recentSnapshots.Any())
        {
            _averageCpuUsage = recentSnapshots.Average(s => s.CpuUsagePercent);
            _averageMemoryUsage = recentSnapshots.Average(s => s.MemoryUsagePercent);
            
            var adjustmentTimes = recentSnapshots.Where(s => s.AverageColorAdjustmentTime > 0);
            if (adjustmentTimes.Any())
            {
                _averageColorAdjustmentTime = adjustmentTimes.Average(s => s.AverageColorAdjustmentTime);
            }
        }
    }

    /// <summary>
    /// Checks for performance issues and raises alerts
    /// </summary>
    private void CheckPerformanceAlerts(PerformanceSnapshot snapshot)
    {
        var alerts = new List<PerformanceAlert>();

        // High CPU usage
        if (snapshot.CpuUsagePercent > 90 && _averageCpuUsage > 80)
        {
            alerts.Add(new PerformanceAlert
            {
                Type = AlertType.HighCpuUsage,
                Severity = AlertSeverity.Warning,
                Message = $"CPU usage is consistently high: {snapshot.CpuUsagePercent:F1}% (avg: {_averageCpuUsage:F1}%)",
                Timestamp = DateTime.UtcNow,
                Value = snapshot.CpuUsagePercent,
                Threshold = 90
            });
        }

        // High memory usage
        if (snapshot.MemoryUsagePercent > 85 && _averageMemoryUsage > 80)
        {
            alerts.Add(new PerformanceAlert
            {
                Type = AlertType.HighMemoryUsage,
                Severity = AlertSeverity.Warning,
                Message = $"Memory usage is critically high: {snapshot.MemoryUsagePercent:F1}% (avg: {_averageMemoryUsage:F1}%)",
                Timestamp = DateTime.UtcNow,
                Value = snapshot.MemoryUsagePercent,
                Threshold = 85
            });
        }

        // Slow color adjustments
        if (snapshot.AverageColorAdjustmentTime > 50) // 50ms threshold
        {
            alerts.Add(new PerformanceAlert
            {
                Type = AlertType.SlowColorAdjustment,
                Severity = AlertSeverity.Information,
                Message = $"Color adjustments are taking longer than expected: {snapshot.AverageColorAdjustmentTime:F1}ms",
                Timestamp = DateTime.UtcNow,
                Value = snapshot.AverageColorAdjustmentTime,
                Threshold = 50
            });
        }

        // GPU overload (if monitoring available)
        if (snapshot.GpuUsagePercent > 95)
        {
            alerts.Add(new PerformanceAlert
            {
                Type = AlertType.HighGpuUsage,
                Severity = AlertSeverity.Warning,
                Message = $"GPU usage is critically high: {snapshot.GpuUsagePercent:F1}%",
                Timestamp = DateTime.UtcNow,
                Value = snapshot.GpuUsagePercent,
                Threshold = 95
            });
        }

        // Raise alerts
        foreach (var alert in alerts)
        {
            PerformanceAlertRaised?.Invoke(this, alert);
            _logger.LogWarning("Performance alert: {AlertType} - {Message}", alert.Type, alert.Message);
        }
    }

    /// <summary>
    /// Applies adaptive performance optimizations based on system load
    /// </summary>
    private void ApplyAdaptiveOptimizations(PerformanceSnapshot snapshot)
    {
        if (DateTime.UtcNow - _lastPerformanceAdjustment < _performanceAdjustmentCooldown)
            return;

        var targetLevel = DetermineOptimalPerformanceLevel(snapshot);
        
        if (targetLevel != _currentPerformanceLevel)
        {
            var optimization = new PerformanceOptimization
            {
                Type = OptimizationType.PerformanceLevelAdjustment,
                FromLevel = _currentPerformanceLevel,
                ToLevel = targetLevel,
                Timestamp = DateTime.UtcNow,
                Reason = $"System load: {snapshot.SystemLoadLevel}, CPU: {snapshot.CpuUsagePercent:F1}%, Memory: {snapshot.MemoryUsagePercent:F1}%"
            };

            ApplyPerformanceLevel(targetLevel);
            
            OptimizationApplied?.Invoke(this, optimization);
            _logger.LogInformation("Applied performance optimization: {FromLevel} → {ToLevel} ({Reason})", 
                optimization.FromLevel, optimization.ToLevel, optimization.Reason);
            
            _lastPerformanceAdjustment = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Determines optimal performance level based on system state
    /// </summary>
    private PerformanceLevel DetermineOptimalPerformanceLevel(PerformanceSnapshot snapshot)
    {
        return snapshot.SystemLoadLevel switch
        {
            SystemLoadLevel.Critical => PerformanceLevel.PowerSaver,
            SystemLoadLevel.High => PerformanceLevel.Efficient,
            SystemLoadLevel.Medium => PerformanceLevel.Balanced,
            SystemLoadLevel.Low => PerformanceLevel.HighQuality,
            SystemLoadLevel.Minimal => PerformanceLevel.Maximum,
            _ => PerformanceLevel.Balanced
        };
    }

    /// <summary>
    /// Applies the specified performance level settings
    /// </summary>
    private async void ApplyPerformanceLevel(PerformanceLevel level)
    {
        try
        {
            var config = await _configurationService.GetConfigurationAsync();
            var oldLevel = _currentPerformanceLevel;
            _currentPerformanceLevel = level;

            // Adjust update intervals based on performance level
            var updateInterval = level switch
            {
                PerformanceLevel.PowerSaver => TimeSpan.FromMinutes(2),
                PerformanceLevel.Efficient => TimeSpan.FromSeconds(30),
                PerformanceLevel.Balanced => TimeSpan.FromSeconds(15),
                PerformanceLevel.HighQuality => TimeSpan.FromSeconds(5),
                PerformanceLevel.Maximum => TimeSpan.FromSeconds(1),
                _ => TimeSpan.FromSeconds(15)
            };

            // Update configuration
            config.Performance.UpdateIntervalMs = (int)updateInterval.TotalMilliseconds;
            config.Performance.CurrentPerformanceLevel = level;
            
            // Adjust transition smoothness
            config.Performance.TransitionSmoothness = level switch
            {
                PerformanceLevel.PowerSaver => 0.3f,
                PerformanceLevel.Efficient => 0.5f,
                PerformanceLevel.Balanced => 0.7f,
                PerformanceLevel.HighQuality => 0.9f,
                PerformanceLevel.Maximum => 1.0f,
                _ => 0.7f
            };

            await _configurationService.SaveConfigurationAsync(config);
            
            _logger.LogInformation("Performance level changed: {OldLevel} → {NewLevel} (Update interval: {Interval}ms)", 
                oldLevel, level, updateInterval.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply performance level: {Level}", level);
        }
    }

    /// <summary>
    /// Records color adjustment performance metrics
    /// </summary>
    public void RecordColorAdjustmentPerformance(TimeSpan adjustmentTime, bool successful)
    {
        if (successful)
        {
            var ms = adjustmentTime.TotalMilliseconds;
            _averageColorAdjustmentTime = (_averageColorAdjustmentTime * _colorAdjustmentCount + ms) / (_colorAdjustmentCount + 1);
            _colorAdjustmentCount++;
        }
    }

    /// <summary>
    /// Gets current performance statistics
    /// </summary>
    public PerformanceStatistics GetCurrentStatistics()
    {
        var recentSnapshots = _performanceHistory.TakeLast(60).ToList(); // Last 5 minutes
        
        return new PerformanceStatistics
        {
            CurrentLevel = _currentPerformanceLevel,
            AverageCpuUsage = _averageCpuUsage,
            AverageMemoryUsage = _averageMemoryUsage,
            AverageColorAdjustmentTime = _averageColorAdjustmentTime,
            TotalColorAdjustments = _colorAdjustmentCount,
            UptimeHours = (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalHours,
            RecentSnapshots = recentSnapshots.Select(ConvertToSystemMetrics).ToList()
        };
    }

    /// <summary>
    /// Forces a performance optimization cycle
    /// </summary>
    public void OptimizeNow()
    {
        _lastPerformanceAdjustment = DateTime.MinValue; // Reset cooldown
        MonitorPerformance(null); // Trigger immediate monitoring
    }

    #region IPerformanceMonitoringService Implementation

    /// <summary>
    /// Gets the current system performance metrics
    /// </summary>
    public async Task<SystemPerformanceMetrics> GetCurrentMetricsAsync()
    {
        await Task.CompletedTask;
        var snapshot = CollectPerformanceSnapshot();
        return ConvertToSystemMetrics(snapshot);
    }

    /// <summary>
    /// Sets the performance level manually
    /// </summary>
    public async Task SetPerformanceLevelAsync(PerformanceLevel level)
    {
        var oldLevel = _currentPerformanceLevel;
        ApplyPerformanceLevel(level);
        
        if (oldLevel != level)
        {
            PerformanceLevelChanged?.Invoke(this, level);
            _logger.LogInformation("Performance level manually set to: {Level}", level);
        }
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets performance history for the specified duration
    /// </summary>
    public async Task<IEnumerable<SystemPerformanceMetrics>> GetPerformanceHistoryAsync(TimeSpan duration)
    {
        await Task.CompletedTask;
        var cutoff = DateTime.UtcNow - duration;
        return _performanceHistory
            .Where(s => s.Timestamp >= cutoff)
            .Select(ConvertToSystemMetrics)
            .ToList();
    }

    /// <summary>
    /// Gets performance statistics
    /// </summary>
    public async Task<PerformanceStatistics> GetStatisticsAsync()
    {
        await Task.CompletedTask;
        return GetCurrentStatistics();
    }

    /// <summary>
    /// Starts performance monitoring
    /// </summary>
    public async Task StartMonitoringAsync()
    {
        // Monitoring starts automatically in constructor, this is for manual control
        _logger.LogInformation("Performance monitoring start requested");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops performance monitoring
    /// </summary>
    public async Task StopMonitoringAsync()
    {
        _monitoringTimer?.Dispose();
        _logger.LogInformation("Performance monitoring stopped");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Converts internal snapshot to external metrics format
    /// </summary>
    private SystemPerformanceMetrics ConvertToSystemMetrics(PerformanceSnapshot snapshot)
    {
        return new SystemPerformanceMetrics
        {
            Timestamp = snapshot.Timestamp,
            CpuUsagePercent = snapshot.CpuUsagePercent,
            MemoryUsagePercent = snapshot.MemoryUsagePercent,
            AvailableMemoryMB = snapshot.AvailableMemoryMB,
            TotalMemoryMB = snapshot.TotalMemoryMB,
            DiskUsagePercent = snapshot.DiskUsagePercent,
            GpuUsagePercent = snapshot.GpuUsagePercent,
            GpuMemoryUsageMB = snapshot.GpuMemoryUsageMB,
            ProcessCpuPercent = snapshot.ProcessCpuPercent,
            ProcessMemoryMB = snapshot.ProcessMemoryMB,
            ProcessHandleCount = snapshot.ProcessHandleCount,
            ProcessThreadCount = snapshot.ProcessThreadCount,
            AverageColorAdjustmentTime = snapshot.AverageColorAdjustmentTime,
            ColorAdjustmentCount = snapshot.ColorAdjustmentCount,
            SystemLoadLevel = snapshot.SystemLoadLevel,
            CurrentPerformanceLevel = _currentPerformanceLevel
        };
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _monitoringTimer?.Dispose();
            
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
            _diskCounter?.Dispose();
            _gpuUsageCounter?.Dispose();
            _gpuMemoryCounter?.Dispose();
            _gpuEngineCounter?.Dispose();
            
            foreach (var counter in _processCounters.Values)
            {
                counter?.Dispose();
            }
            _processCounters.Clear();
            
            _logger.LogInformation("Performance monitoring service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing performance monitoring service");
        }

        _disposed = true;
    }

    #endregion
}

/// <summary>
/// Internal performance monitoring snapshot
/// </summary>
public class PerformanceSnapshot
{
    public DateTime Timestamp { get; set; }
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double AvailableMemoryMB { get; set; }
    public double TotalMemoryMB { get; set; }
    public double DiskUsagePercent { get; set; }
    public double GpuUsagePercent { get; set; }
    public double GpuMemoryUsageMB { get; set; }
    public double ProcessCpuPercent { get; set; }
    public double ProcessMemoryMB { get; set; }
    public int ProcessHandleCount { get; set; }
    public int ProcessThreadCount { get; set; }
    public double AverageColorAdjustmentTime { get; set; }
    public int ColorAdjustmentCount { get; set; }
    public SystemLoadLevel SystemLoadLevel { get; set; }
}

/// <summary>
/// Internal performance optimization information
/// </summary>
public class PerformanceOptimization
{
    public OptimizationType Type { get; set; }
    public PerformanceLevel FromLevel { get; set; }
    public PerformanceLevel ToLevel { get; set; }
    public DateTime Timestamp { get; set; }
    public string Reason { get; set; } = string.Empty;
}
