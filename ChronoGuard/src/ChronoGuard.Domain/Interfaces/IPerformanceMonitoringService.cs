using ChronoGuard.Domain.Entities;

namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Service for monitoring system and application performance with automatic optimization
/// </summary>
public interface IPerformanceMonitoringService : IDisposable
{
    /// <summary>
    /// Gets the current system performance metrics
    /// </summary>
    Task<SystemPerformanceMetrics> GetCurrentMetricsAsync();

    /// <summary>
    /// Gets the current performance level
    /// </summary>
    PerformanceLevel CurrentPerformanceLevel { get; }

    /// <summary>
    /// Sets the performance level manually
    /// </summary>
    Task SetPerformanceLevelAsync(PerformanceLevel level);

    /// <summary>
    /// Gets performance history for the specified duration
    /// </summary>
    Task<IEnumerable<SystemPerformanceMetrics>> GetPerformanceHistoryAsync(TimeSpan duration);

    /// <summary>
    /// Gets performance statistics
    /// </summary>
    Task<PerformanceStatistics> GetStatisticsAsync();

    /// <summary>
    /// Starts performance monitoring
    /// </summary>
    Task StartMonitoringAsync();

    /// <summary>
    /// Stops performance monitoring
    /// </summary>
    Task StopMonitoringAsync();

    /// <summary>
    /// Event raised when performance level changes
    /// </summary>
    event EventHandler<PerformanceLevel>? PerformanceLevelChanged;

    /// <summary>
    /// Event raised when performance alert is triggered
    /// </summary>
    event EventHandler<PerformanceAlert>? PerformanceAlertTriggered;

    /// <summary>
    /// Event raised when new performance metrics are available
    /// </summary>
    event EventHandler<SystemPerformanceMetrics>? MetricsUpdated;
}
