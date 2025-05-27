using ChronoGuard.Domain.Entities;

namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Service for managing application notifications
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows an informational notification
    /// </summary>
    Task ShowInfoAsync(string title, string message);

    /// <summary>
    /// Shows a warning notification
    /// </summary>
    Task ShowWarningAsync(string title, string message);

    /// <summary>
    /// Shows an error notification
    /// </summary>
    Task ShowErrorAsync(string title, string message);

    /// <summary>
    /// Shows a color temperature transition notification
    /// </summary>
    Task ShowTransitionAsync(int fromTemperature, int toTemperature);

    /// <summary>
    /// Shows a sleep reminder notification
    /// </summary>
    Task ShowSleepReminderAsync(TimeSpan timeUntilOptimalSleep);

    /// <summary>
    /// Checks if notifications are currently allowed (respects quiet hours)
    /// </summary>
    bool AreNotificationsAllowed();

    /// <summary>
    /// Sets the notification level
    /// </summary>
    Task SetNotificationLevelAsync(NotificationLevel level);

    /// <summary>
    /// Shows a transition notification between color temperatures
    /// </summary>
    Task ShowTransitionNotificationAsync(ColorTemperature from, ColorTemperature to, string reason);

    /// <summary>
    /// Shows a sleep reminder notification for a specific bedtime
    /// </summary>
    Task ShowSleepReminderAsync(DateTime bedtime);

    /// <summary>
    /// Shows an error notification with optional details
    /// </summary>
    Task ShowErrorNotificationAsync(string error, string? details = null);

    /// <summary>
    /// Shows application pause notification
    /// </summary>
    Task ShowPauseNotificationAsync(TimeSpan? duration = null);

    /// <summary>
    /// Shows application resume notification
    /// </summary>
    Task ShowResumeNotificationAsync();
}
