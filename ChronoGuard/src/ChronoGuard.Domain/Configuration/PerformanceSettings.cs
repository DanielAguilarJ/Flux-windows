using ChronoGuard.Domain.Entities;

namespace ChronoGuard.Domain.Configuration;

/// <summary>
/// Performance monitoring and optimization configuration
/// </summary>
public class PerformanceSettings
{
    /// <summary>
    /// Current performance level
    /// </summary>
    public PerformanceLevel CurrentPerformanceLevel { get; set; } = PerformanceLevel.Balanced;
    
    /// <summary>
    /// Update interval in milliseconds
    /// </summary>
    public int UpdateIntervalMs { get; set; } = 15000;
    
    /// <summary>
    /// Transition smoothness factor (0.0 to 1.0)
    /// </summary>
    public float TransitionSmoothness { get; set; } = 0.7f;
    
    /// <summary>
    /// Enable automatic performance optimization
    /// </summary>
    public bool EnableAutoOptimization { get; set; } = true;
    
    /// <summary>
    /// Enable performance monitoring
    /// </summary>
    public bool EnableMonitoring { get; set; } = true;
    
    /// <summary>
    /// Monitoring interval in seconds
    /// </summary>
    public int MonitoringIntervalSeconds { get; set; } = 5;
    
    /// <summary>
    /// CPU usage threshold for performance alerts (percentage)
    /// </summary>
    public double CpuAlertThreshold { get; set; } = 90.0;
    
    /// <summary>
    /// Memory usage threshold for performance alerts (percentage)
    /// </summary>
    public double MemoryAlertThreshold { get; set; } = 85.0;
    
    /// <summary>
    /// GPU usage threshold for performance alerts (percentage)
    /// </summary>
    public double GpuAlertThreshold { get; set; } = 95.0;
    
    /// <summary>
    /// Color adjustment time threshold for alerts (milliseconds)
    /// </summary>
    public double ColorAdjustmentThreshold { get; set; } = 50.0;
    
    /// <summary>
    /// Maximum number of performance history entries to keep
    /// </summary>
    public int MaxHistoryEntries { get; set; } = 720; // 1 hour at 5-second intervals
    
    /// <summary>
    /// Performance level adjustment cooldown in minutes
    /// </summary>
    public int AdjustmentCooldownMinutes { get; set; } = 2;
    
    /// <summary>
    /// Enable GPU monitoring (if available)
    /// </summary>
    public bool EnableGpuMonitoring { get; set; } = true;
    
    /// <summary>
    /// Enable process-specific monitoring
    /// </summary>
    public bool EnableProcessMonitoring { get; set; } = true;
    
    /// <summary>
    /// Enable performance alerts
    /// </summary>
    public bool EnableAlerts { get; set; } = true;
}
