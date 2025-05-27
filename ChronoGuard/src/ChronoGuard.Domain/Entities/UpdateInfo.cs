namespace ChronoGuard.Domain.Entities;

/// <summary>
/// Information about an available update
/// </summary>
public class UpdateInfo
{
    public bool IsUpdateAvailable { get; set; }
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
    public long FileSize { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public bool IsSecurityUpdate { get; set; }
    public bool IsMandatory { get; set; }
    public UpdateType Type { get; set; } = UpdateType.Minor;
}

/// <summary>
/// Type of update
/// </summary>
public enum UpdateType
{
    Patch,
    Minor,
    Major,
    Security
}
