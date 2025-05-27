namespace ChronoGuard.Domain.Entities;

/// <summary>
/// Comprehensive system performance metrics
/// </summary>
public class SystemPerformanceMetrics
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
    public PerformanceLevel CurrentPerformanceLevel { get; set; }
}

/// <summary>
/// Performance statistics and summary information
/// </summary>
public class PerformanceStatistics
{
    public PerformanceLevel CurrentLevel { get; set; }
    public double AverageCpuUsage { get; set; }
    public double AverageMemoryUsage { get; set; }
    public double AverageGpuUsage { get; set; }
    public double AverageColorAdjustmentTime { get; set; }
    public int TotalColorAdjustments { get; set; }
    public double UptimeHours { get; set; }
    public DateTime LastOptimization { get; set; }
    public int OptimizationCount { get; set; }
    public List<SystemPerformanceMetrics> RecentMetrics { get; set; } = new();
    public List<SystemPerformanceMetrics> RecentSnapshots { get; set; } = new();
}

/// <summary>
/// Performance alert information
/// </summary>
public class PerformanceAlert
{
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public double Threshold { get; set; }
    public string? RecommendedAction { get; set; }
}

/// <summary>
/// System load levels for performance assessment
/// </summary>
public enum SystemLoadLevel
{
    Minimal,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Performance levels for adaptive optimization
/// </summary>
public enum PerformanceLevel
{
    PowerSaver,
    Efficient, 
    Balanced,
    HighQuality,
    Maximum
}

/// <summary>
/// Performance alert types
/// </summary>
public enum AlertType
{
    HighCpuUsage,
    HighMemoryUsage,
    HighGpuUsage,
    LowAvailableMemory,
    SlowColorAdjustment,
    SystemOverload,
    ProcessUnresponsive,
    ThermalThrottling
}

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Information,
    Warning,
    Critical
}

/// <summary>
/// Performance optimization types
/// </summary>
public enum OptimizationType
{
    PerformanceLevelAdjustment,
    UpdateIntervalAdjustment,
    TransitionOptimization,
    MemoryOptimization,
    GpuOptimization
}
