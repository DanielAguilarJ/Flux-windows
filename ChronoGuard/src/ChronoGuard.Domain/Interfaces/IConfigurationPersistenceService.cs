namespace ChronoGuard.Domain.Interfaces;

/// <summary>
/// Service for persisting configuration data
/// </summary>
public interface IConfigurationPersistenceService
{
    /// <summary>
    /// Loads configuration from storage
    /// </summary>
    Task<T?> LoadConfigurationAsync<T>() where T : class;

    /// <summary>
    /// Saves configuration to storage
    /// </summary>
    Task SaveConfigurationAsync<T>(T configuration) where T : class;

    /// <summary>
    /// Checks if configuration exists
    /// </summary>
    Task<bool> ConfigurationExistsAsync<T>() where T : class;

    /// <summary>
    /// Deletes configuration from storage
    /// </summary>
    Task DeleteConfigurationAsync<T>() where T : class;

    /// <summary>
    /// Creates backup of current configuration
    /// </summary>
    Task CreateBackupAsync();

    /// <summary>
    /// Restores configuration from backup
    /// </summary>
    Task RestoreFromBackupAsync(string backupPath);

    /// <summary>
    /// Gets available backup files
    /// </summary>
    Task<IEnumerable<string>> GetAvailableBackupsAsync();
}
