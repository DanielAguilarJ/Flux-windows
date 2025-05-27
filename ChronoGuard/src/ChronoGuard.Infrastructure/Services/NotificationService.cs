using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;

namespace ChronoGuard.Infrastructure.Services;

/// <summary>
/// Service for showing Windows notifications and toast messages
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly IConfigurationService _configurationService;
    private DateTime _lastNotificationTime = DateTime.MinValue;
    private const int MIN_NOTIFICATION_INTERVAL_SECONDS = 30;

    public NotificationService(
        ILogger<NotificationService> logger, 
        IConfigurationService configurationService)
    {
        _logger = logger;
        _configurationService = configurationService;
    }

    /// <summary>
    /// Shows a notification with title and message
    /// </summary>
    public async Task ShowNotificationAsync(string title, string message, NotificationType type = NotificationType.Info)
    {
        try
        {
            var config = await _configurationService.GetConfigurationAsync();
            
            if (!config.Notifications.Enabled)
            {
                _logger.LogDebug("Notifications are disabled, skipping notification");
                return;
            }

            if (IsInQuietHours(config))
            {
                _logger.LogDebug("In quiet hours, skipping notification");
                return;
            }

            if (!ShouldShowNotification(type, config.Notifications.Level))
            {
                _logger.LogDebug("Notification type {Type} not allowed for level {Level}", type, config.Notifications.Level);
                return;
            }

            // Prevent spam by limiting notification frequency
            if (DateTime.Now - _lastNotificationTime < TimeSpan.FromSeconds(MIN_NOTIFICATION_INTERVAL_SECONDS))
            {
                _logger.LogDebug("Notification rate limited");
                return;
            }

            await ShowToastNotificationAsync(title, message, type);
            _lastNotificationTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show notification: {Title} - {Message}", title, message);
        }
    }

    /// <summary>
    /// Shows a transition notification
    /// </summary>
    public async Task ShowTransitionNotificationAsync(ColorTemperature from, ColorTemperature to, string reason)
    {
        var config = await _configurationService.GetConfigurationAsync();
        if (!config.Notifications.ShowTransitionNotifications)
        {
            return;
        }

        var message = $"Cambiando de {from.Kelvin}K a {to.Kelvin}K";
        if (!string.IsNullOrEmpty(reason))
        {
            message += $" - {reason}";
        }

        await ShowNotificationAsync("ChronoGuard - Transición", message, NotificationType.Transition);
    }

    /// <summary>
    /// Shows a sleep reminder notification
    /// </summary>
    public async Task ShowSleepReminderAsync(DateTime bedtime)
    {
        var config = await _configurationService.GetConfigurationAsync();
        if (!config.Notifications.ShowSleepReminders)
        {
            return;
        }

        var timeUntilBed = bedtime - DateTime.Now;
        var message = timeUntilBed.TotalMinutes > 60 
            ? $"Se recomienda dormir en {timeUntilBed.Hours} hora(s)"
            : $"Se recomienda dormir en {timeUntilBed.Minutes} minutos";

        await ShowNotificationAsync("ChronoGuard - Recordatorio", message, NotificationType.SleepReminder);
    }

    /// <summary>
    /// Shows an error notification
    /// </summary>
    public async Task ShowErrorNotificationAsync(string error, string? details = null)
    {
        var message = string.IsNullOrEmpty(details) ? error : $"{error}\n{details}";
        await ShowNotificationAsync("ChronoGuard - Error", message, NotificationType.Error);
    }

    /// <summary>
    /// Shows application pause notification
    /// </summary>
    public async Task ShowPauseNotificationAsync(TimeSpan? duration = null)
    {
        var message = duration.HasValue 
            ? $"Pausado por {FormatDuration(duration.Value)}"
            : "Pausado indefinidamente";
            
        await ShowNotificationAsync("ChronoGuard - Pausado", message, NotificationType.Info);
    }

    /// <summary>
    /// Shows application resume notification
    /// </summary>
    public async Task ShowResumeNotificationAsync()
    {
        await ShowNotificationAsync("ChronoGuard - Reanudado", "Filtro de luz azul reactivado", NotificationType.Info);
    }

    /// <summary>
    /// Checks if current time is within quiet hours
    /// </summary>
    private bool IsInQuietHours(AppConfiguration config)
    {
        var now = DateTime.Now.TimeOfDay;
        var start = config.Notifications.QuietHoursStart;
        var end = config.Notifications.QuietHoursEnd;

        // Handle overnight quiet hours (e.g., 22:00 to 08:00)
        if (start > end)
        {
            return now >= start || now <= end;
        }
        else
        {
            return now >= start && now <= end;
        }
    }

    /// <summary>
    /// Determines if a notification should be shown based on type and level
    /// </summary>
    private static bool ShouldShowNotification(NotificationType type, NotificationLevel level)
    {
        return level switch
        {
            NotificationLevel.Silent => false,
            NotificationLevel.Basic => type == NotificationType.Error || type == NotificationType.Critical,
            NotificationLevel.Detailed => true,
            _ => false
        };
    }

    /// <summary>
    /// Shows a Windows Toast notification
    /// </summary>
    private async Task ShowToastNotificationAsync(string title, string message, NotificationType type)
    {
        try
        {
            // For Windows 10/11, use PowerShell to show toast notifications
            if (Environment.OSVersion.Version.Major >= 10)
            {
                await ShowWindowsToastAsync(title, message, type);
            }
            else
            {
                // Fallback for older Windows versions - use balloon tip
                ShowBalloonTip(title, message, type);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show toast notification, falling back to balloon tip");
            ShowBalloonTip(title, message, type);
        }
    }

    /// <summary>
    /// Shows Windows 10/11 toast notification using PowerShell
    /// </summary>
    private async Task ShowWindowsToastAsync(string title, string message, NotificationType type)
    {
        try
        {
            var iconPath = GetIconPathForType(type);
            var script = $@"
                $app = '{{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}}\WindowsPowerShell\v1.0\powershell.exe'
                [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
                [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null

                $template = @'
                <toast>
                    <visual>
                        <binding template=""ToastGeneric"">
                            <text>{title}</text>
                            <text>{message}</text>
                            <image placement=""appLogoOverride"" src=""{iconPath}"" />
                        </binding>
                    </visual>
                </toast>
'@

                $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
                $xml.LoadXml($template)
                $toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
                [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($app).Show($toast)
            ";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogWarning("PowerShell toast notification failed: {Error}", error);
                ShowBalloonTip(title, message, type);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show Windows toast notification");
            ShowBalloonTip(title, message, type);
        }
    }

    /// <summary>
    /// Shows a balloon tip notification (fallback method)
    /// </summary>
    private void ShowBalloonTip(string title, string message, NotificationType type)
    {
        try
        {
            // This would typically be called through the SystemTrayService
            // For now, log the notification
            _logger.LogInformation("Notification: {Title} - {Message} (Type: {Type})", title, message, type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show balloon tip notification");
        }
    }

    /// <summary>
    /// Gets the icon path for a notification type
    /// </summary>
    private string GetIconPathForType(NotificationType type)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var assetsDir = Path.Combine(baseDir, "Assets");

        return type switch
        {
            NotificationType.Error or NotificationType.Critical => Path.Combine(assetsDir, "chronoguard-error.ico"),
            NotificationType.Warning => Path.Combine(assetsDir, "chronoguard-warning.ico"),
            NotificationType.Transition => Path.Combine(assetsDir, "chronoguard-transition.ico"),
            NotificationType.SleepReminder => Path.Combine(assetsDir, "chronoguard-sleep.ico"),
            _ => Path.Combine(assetsDir, "chronoguard.ico")
        };
    }

    /// <summary>
    /// Formats a time duration for display
    /// </summary>
    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{(int)duration.TotalDays} día(s)";
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours} hora(s)";
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes} minuto(s)";
        
        return "unos segundos";
    }

    // INotificationService interface implementation
    public async Task ShowInfoAsync(string title, string message)
    {
        await ShowNotificationAsync(title, message, NotificationType.Info);
    }

    public async Task ShowWarningAsync(string title, string message)
    {
        await ShowNotificationAsync(title, message, NotificationType.Warning);
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        await ShowNotificationAsync(title, message, NotificationType.Error);
    }

    public async Task ShowTransitionAsync(int fromTemperature, int toTemperature)
    {
        await ShowTransitionNotificationAsync(
            new ColorTemperature(fromTemperature), 
            new ColorTemperature(toTemperature), 
            "Transición automática");
    }

    public async Task ShowSleepReminderAsync(TimeSpan timeUntilOptimalSleep)
    {
        var bedtime = DateTime.Now.Add(timeUntilOptimalSleep);
        await ShowSleepReminderAsync(bedtime);
    }

    public bool AreNotificationsAllowed()
    {
        try
        {
            var config = _configurationService.GetConfigurationAsync().Result;
            return config.Notifications.Enabled && !IsInQuietHours(config);
        }
        catch
        {
            return false;
        }
    }

    public async Task SetNotificationLevelAsync(NotificationLevel level)
    {
        var config = await _configurationService.GetConfigurationAsync();
        config.Notifications.Level = level;
        await _configurationService.SaveConfigurationAsync(config);
    }
}

/// <summary>
/// Types of notifications
/// </summary>
public enum NotificationType
{
    Info,
    Warning,
    Error,
    Critical,
    Transition,
    SleepReminder
}
