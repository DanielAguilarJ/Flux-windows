using ChronoGuard.Domain.Interfaces;
using ChronoGuard.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using System.IO;
using System.Text.RegularExpressions;

namespace ChronoGuard.Infrastructure.Services;

/// <summary>
/// Service for managing application whitelist with intelligent detection capabilities
/// Automatically detects design software, games, and color-critical applications
/// </summary>
public class ApplicationWhitelistService : IApplicationWhitelistService
{
    private readonly ILogger<ApplicationWhitelistService> _logger;
    private readonly IForegroundApplicationService _foregroundAppService;
    private readonly string _whitelistFilePath;
    private readonly Dictionary<string, WhitelistedApplication> _whitelist = new();
    private readonly Dictionary<string, DateTime> _temporaryDisables = new();
    private readonly object _whitelistLock = new();

    // Built-in application patterns for smart detection
    private readonly Dictionary<string, (ApplicationCategory category, WhitelistMode mode, string reason)> _knownApplications = new()
    {
        // Design Software
        { "photoshop", (ApplicationCategory.DesignSoftware, WhitelistMode.CompleteDisable, "Adobe Photoshop - Color accuracy critical") },
        { "illustrator", (ApplicationCategory.DesignSoftware, WhitelistMode.CompleteDisable, "Adobe Illustrator - Vector design requires accurate colors") },
        { "indesign", (ApplicationCategory.DesignSoftware, WhitelistMode.CompleteDisable, "Adobe InDesign - Layout design requires color accuracy") },
        { "figma", (ApplicationCategory.DesignSoftware, WhitelistMode.CompleteDisable, "Figma - UI/UX design tool") },
        { "sketch", (ApplicationCategory.DesignSoftware, WhitelistMode.CompleteDisable, "Sketch - Interface design tool") },
        { "xd", (ApplicationCategory.DesignSoftware, WhitelistMode.CompleteDisable, "Adobe XD - User experience design") },
        { "canva", (ApplicationCategory.DesignSoftware, WhitelistMode.CompleteDisable, "Canva - Graphic design platform") },
        { "gimp", (ApplicationCategory.DesignSoftware, WhitelistMode.CompleteDisable, "GIMP - Image editor") },
        { "krita", (ApplicationCategory.DesignSoftware, WhitelistMode.CompleteDisable, "Krita - Digital painting") },
        { "paintshoppro", (ApplicationCategory.DesignSoftware, WhitelistMode.CompleteDisable, "Paint Shop Pro - Photo editing") },

        // Video Editing
        { "premiere", (ApplicationCategory.VideoEditing, WhitelistMode.CompleteDisable, "Adobe Premiere Pro - Video editing") },
        { "aftereffects", (ApplicationCategory.VideoEditing, WhitelistMode.CompleteDisable, "Adobe After Effects - Motion graphics") },
        { "davinciresolve", (ApplicationCategory.VideoEditing, WhitelistMode.CompleteDisable, "DaVinci Resolve - Professional video editing") },
        { "resolve", (ApplicationCategory.VideoEditing, WhitelistMode.CompleteDisable, "DaVinci Resolve - Professional video editing") },
        { "vegas", (ApplicationCategory.VideoEditing, WhitelistMode.CompleteDisable, "Sony Vegas - Video editing") },
        { "avid", (ApplicationCategory.VideoEditing, WhitelistMode.CompleteDisable, "Avid Media Composer - Professional editing") },
        { "finalcut", (ApplicationCategory.VideoEditing, WhitelistMode.CompleteDisable, "Final Cut Pro - Video editing") },

        // CAD/3D Software
        { "autocad", (ApplicationCategory.CAD3D, WhitelistMode.CompleteDisable, "AutoCAD - Computer-aided design") },
        { "solidworks", (ApplicationCategory.CAD3D, WhitelistMode.CompleteDisable, "SolidWorks - 3D CAD software") },
        { "fusion360", (ApplicationCategory.CAD3D, WhitelistMode.CompleteDisable, "Fusion 360 - 3D design and manufacturing") },
        { "blender", (ApplicationCategory.CAD3D, WhitelistMode.ReducedEffect, "Blender - 3D creation suite") },
        { "maya", (ApplicationCategory.CAD3D, WhitelistMode.CompleteDisable, "Autodesk Maya - 3D modeling and animation") },
        { "3dsmax", (ApplicationCategory.CAD3D, WhitelistMode.CompleteDisable, "3ds Max - 3D modeling and rendering") },
        { "cinema4d", (ApplicationCategory.CAD3D, WhitelistMode.CompleteDisable, "Cinema 4D - 3D modeling and animation") },
        { "rhino", (ApplicationCategory.CAD3D, WhitelistMode.CompleteDisable, "Rhinoceros 3D - NURBS modeling") },

        // Gaming (selective - major launchers and known color-sensitive games)
        { "steam", (ApplicationCategory.Gaming, WhitelistMode.FullscreenOnly, "Steam - Gaming platform") },
        { "epic", (ApplicationCategory.Gaming, WhitelistMode.FullscreenOnly, "Epic Games Launcher") },
        { "battlenet", (ApplicationCategory.Gaming, WhitelistMode.FullscreenOnly, "Battle.net Launcher") },
        { "origin", (ApplicationCategory.Gaming, WhitelistMode.FullscreenOnly, "EA Origin Launcher") },
        { "uplay", (ApplicationCategory.Gaming, WhitelistMode.FullscreenOnly, "Ubisoft Connect") },
        { "gog", (ApplicationCategory.Gaming, WhitelistMode.FullscreenOnly, "GOG Galaxy") },

        // Photography
        { "lightroom", (ApplicationCategory.Photography, WhitelistMode.CompleteDisable, "Adobe Lightroom - Photo editing") },
        { "capture1", (ApplicationCategory.Photography, WhitelistMode.CompleteDisable, "Capture One - RAW processing") },
        { "luminar", (ApplicationCategory.Photography, WhitelistMode.CompleteDisable, "Luminar - Photo editing") },
        { "on1", (ApplicationCategory.Photography, WhitelistMode.CompleteDisable, "ON1 PhotoRAW - Photo editing") },

        // Development (IDEs that might have color-coded syntax)
        { "visualstudio", (ApplicationCategory.Development, WhitelistMode.ReducedEffect, "Visual Studio - IDE") },
        { "code", (ApplicationCategory.Development, WhitelistMode.ReducedEffect, "Visual Studio Code - Code editor") },
        { "intellij", (ApplicationCategory.Development, WhitelistMode.ReducedEffect, "IntelliJ IDEA - IDE") },
        { "pycharm", (ApplicationCategory.Development, WhitelistMode.ReducedEffect, "PyCharm - Python IDE") },
        { "webstorm", (ApplicationCategory.Development, WhitelistMode.ReducedEffect, "WebStorm - JavaScript IDE") },

        // Multimedia
        { "vlc", (ApplicationCategory.Multimedia, WhitelistMode.FullscreenOnly, "VLC Media Player") },
        { "mpc", (ApplicationCategory.Multimedia, WhitelistMode.FullscreenOnly, "Media Player Classic") },
        { "potplayer", (ApplicationCategory.Multimedia, WhitelistMode.FullscreenOnly, "PotPlayer - Media player") },
        { "netflix", (ApplicationCategory.Multimedia, WhitelistMode.ReducedEffect, "Netflix - Video streaming") },
        { "obs", (ApplicationCategory.Multimedia, WhitelistMode.CompleteDisable, "OBS Studio - Broadcasting software") },

        // Color-critical applications
        { "colorchecker", (ApplicationCategory.ColorCritical, WhitelistMode.CompleteDisable, "Color calibration software") },
        { "spyder", (ApplicationCategory.ColorCritical, WhitelistMode.CompleteDisable, "Spyder color calibration") },
        { "colormunki", (ApplicationCategory.ColorCritical, WhitelistMode.CompleteDisable, "ColorMunki calibration") },
        { "displaycal", (ApplicationCategory.ColorCritical, WhitelistMode.CompleteDisable, "DisplayCAL calibration") }
    };

    public event EventHandler<WhitelistChangedEventArgs>? WhitelistChanged;

    public ApplicationWhitelistService(
        ILogger<ApplicationWhitelistService> logger,
        IForegroundApplicationService foregroundAppService)
    {
        _logger = logger;
        _foregroundAppService = foregroundAppService;

        // Set up whitelist file path
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var chronoGuardPath = Path.Combine(appDataPath, "ChronoGuard");
        _whitelistFilePath = Path.Combine(chronoGuardPath, "application-whitelist.json");

        // Ensure directory exists
        Directory.CreateDirectory(chronoGuardPath);

        // Load existing whitelist
        _ = Task.Run(LoadWhitelistAsync);
    }

    public async Task<bool> IsApplicationWhitelistedAsync(string processName)
    {
        var status = await GetWhitelistStatusAsync(processName);
        return status.IsWhitelisted && (!status.IsTemporary || status.ExpiresAt > DateTime.UtcNow);
    }

    public async Task<WhitelistStatus> GetWhitelistStatusAsync(string processName)
    {
        await EnsureWhitelistLoadedAsync();

        lock (_whitelistLock)
        {
            var normalizedName = NormalizeProcessName(processName);

            // Check for temporary disables first
            if (_temporaryDisables.TryGetValue(normalizedName, out var expiry))
            {
                if (expiry > DateTime.UtcNow)
                {
                    return new WhitelistStatus
                    {
                        IsWhitelisted = true,
                        Mode = WhitelistMode.CompleteDisable,
                        Reason = "Temporary disable for current session",
                        IsTemporary = true,
                        ExpiresAt = expiry
                    };
                }
                else
                {
                    _temporaryDisables.Remove(normalizedName);
                }
            }

            if (_whitelist.TryGetValue(normalizedName, out var app))
            {
                return new WhitelistStatus
                {
                    IsWhitelisted = true,
                    Mode = app.Mode,
                    Reason = app.Reason,
                    AddedAt = app.AddedAt,
                    IsAutoDetected = app.IsAutoDetected
                };
            }

            return new WhitelistStatus { IsWhitelisted = false };
        }
    }

    public async Task AddToWhitelistAsync(string processName, WhitelistMode mode = WhitelistMode.CompleteDisable, string? reason = null)
    {
        await EnsureWhitelistLoadedAsync();

        var normalizedName = NormalizeProcessName(processName);
        var displayName = GetApplicationDisplayName(normalizedName);

        var app = new WhitelistedApplication
        {
            ProcessName = normalizedName,
            DisplayName = displayName,
            Mode = mode,
            Reason = reason,
            AddedAt = DateTime.UtcNow,
            IsBuiltIn = false,
            IsAutoDetected = false,
            Category = DetermineApplicationCategory(normalizedName)
        };

        lock (_whitelistLock)
        {
            _whitelist[normalizedName] = app;
        }

        await SaveWhitelistAsync();

        WhitelistChanged?.Invoke(this, new WhitelistChangedEventArgs
        {
            ProcessName = normalizedName,
            IsAdded = true,
            Mode = mode,
            Reason = reason
        });

        _logger.LogInformation("Added application to whitelist: {ProcessName} (Mode: {Mode})", normalizedName, mode);
    }

    public async Task RemoveFromWhitelistAsync(string processName)
    {
        await EnsureWhitelistLoadedAsync();

        var normalizedName = NormalizeProcessName(processName);

        lock (_whitelistLock)
        {
            if (_whitelist.Remove(normalizedName))
            {
                _temporaryDisables.Remove(normalizedName);

                WhitelistChanged?.Invoke(this, new WhitelistChangedEventArgs
                {
                    ProcessName = normalizedName,
                    IsAdded = false
                });

                _logger.LogInformation("Removed application from whitelist: {ProcessName}", normalizedName);
            }
        }

        await SaveWhitelistAsync();
    }

    public async Task<IEnumerable<WhitelistedApplication>> GetWhitelistedApplicationsAsync()
    {
        await EnsureWhitelistLoadedAsync();

        lock (_whitelistLock)
        {
            return _whitelist.Values.ToList();
        }
    }

    public async Task<IEnumerable<DetectedApplication>> ScanForApplicationsToWhitelistAsync()
    {
        var detectedApps = new List<DetectedApplication>();

        try
        {
            var processes = Process.GetProcesses();
            var processedNames = new HashSet<string>();

            foreach (var process in processes)
            {
                try
                {
                    if (string.IsNullOrEmpty(process.ProcessName) || processedNames.Contains(process.ProcessName))
                        continue;

                    processedNames.Add(process.ProcessName);

                    var detection = AnalyzeApplication(process.ProcessName, process.MainWindowTitle, GetProcessPath(process));
                    if (detection != null)
                    {
                        detectedApps.Add(detection);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error analyzing process {ProcessName}", process.ProcessName);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning for applications to whitelist");
        }

        return detectedApps.OrderByDescending(a => a.ConfidenceScore);
    }

    public async Task<WhitelistAction> ProcessForegroundApplicationAsync(string processName, string windowTitle, string executablePath)
    {
        var normalizedName = NormalizeProcessName(processName);

        // Check if already whitelisted
        var status = await GetWhitelistStatusAsync(normalizedName);
        if (status.IsWhitelisted)
        {
            return new WhitelistAction
            {
                ShouldDisableColorAdjustment = true,
                Mode = status.Mode,
                Reason = status.Reason
            };
        }

        // Check if should be auto-detected
        var detection = AnalyzeApplication(normalizedName, windowTitle, executablePath);
        if (detection != null && detection.ConfidenceScore >= 0.8) // High confidence threshold
        {
            // Auto-add to whitelist if confidence is very high
            await AddToWhitelistAsync(normalizedName, detection.RecommendedMode, $"Auto-detected: {detection.DetectionReason}");

            return new WhitelistAction
            {
                ShouldDisableColorAdjustment = true,
                Mode = detection.RecommendedMode,
                Reason = detection.DetectionReason,
                WasAutoDetected = true
            };
        }
        else if (detection != null && detection.ConfidenceScore >= 0.6) // Medium confidence
        {
            return new WhitelistAction
            {
                ShouldDisableColorAdjustment = false,
                Mode = detection.RecommendedMode,
                Reason = detection.DetectionReason,
                WasAutoDetected = true,
                RequiresUserConfirmation = true
            };
        }

        return new WhitelistAction { ShouldDisableColorAdjustment = false };
    }

    public async Task<bool> TemporaryDisableForCurrentAppAsync(TimeSpan duration)
    {
        var foregroundApp = _foregroundAppService.GetForegroundApplicationName();
        if (string.IsNullOrEmpty(foregroundApp))
            return false;

        var normalizedName = NormalizeProcessName(foregroundApp);
        var expiry = DateTime.UtcNow.Add(duration);

        lock (_whitelistLock)
        {
            _temporaryDisables[normalizedName] = expiry;
        }

        _logger.LogInformation("Temporarily disabled color adjustment for {ProcessName} until {Expiry}", normalizedName, expiry);
        return true;
    }

    private DetectedApplication? AnalyzeApplication(string processName, string windowTitle, string executablePath)
    {
        var normalizedName = NormalizeProcessName(processName);

        // Check known applications first
        if (_knownApplications.TryGetValue(normalizedName, out var knownApp))
        {
            return new DetectedApplication
            {
                ProcessName = normalizedName,
                DisplayName = GetApplicationDisplayName(normalizedName),
                Category = knownApp.category,
                DetectionReason = knownApp.reason,
                RecommendedMode = knownApp.mode,
                ConfidenceScore = 0.95 // Very high confidence for known apps
            };
        }

        // Pattern-based detection
        var patterns = new[]
        {
            // Design software patterns
            (pattern: @"(photoshop|illustrator|indesign|sketch|figma|canva)", category: ApplicationCategory.DesignSoftware, mode: WhitelistMode.CompleteDisable, reason: "Design software detected", confidence: 0.9),
            (pattern: @"(premiere|aftereffects|davinci|resolve|vegas|avid)", category: ApplicationCategory.VideoEditing, mode: WhitelistMode.CompleteDisable, reason: "Video editing software detected", confidence: 0.9),
            (pattern: @"(autocad|solidworks|fusion|blender|maya|3dsmax|cinema4d)", category: ApplicationCategory.CAD3D, mode: WhitelistMode.CompleteDisable, reason: "CAD/3D software detected", confidence: 0.9),
            (pattern: @"(lightroom|capture1|luminar|rawtherapee)", category: ApplicationCategory.Photography, mode: WhitelistMode.CompleteDisable, reason: "Photo editing software detected", confidence: 0.9),
            (pattern: @"(game|gaming)", category: ApplicationCategory.Gaming, mode: WhitelistMode.FullscreenOnly, reason: "Gaming application detected", confidence: 0.7),
        };

        foreach (var (pattern, category, mode, reason, confidence) in patterns)
        {
            if (Regex.IsMatch(normalizedName, pattern, RegexOptions.IgnoreCase))
            {
                return new DetectedApplication
                {
                    ProcessName = normalizedName,
                    DisplayName = GetApplicationDisplayName(normalizedName),
                    Category = category,
                    DetectionReason = reason,
                    RecommendedMode = mode,
                    ConfidenceScore = confidence
                };
            }
        }

        // Check executable path for additional clues
        if (!string.IsNullOrEmpty(executablePath))
        {
            var pathLower = executablePath.ToLowerInvariant();
            if (pathLower.Contains("adobe") || pathLower.Contains("autodesk") || pathLower.Contains("corel"))
            {
                return new DetectedApplication
                {
                    ProcessName = normalizedName,
                    DisplayName = GetApplicationDisplayName(normalizedName),
                    Category = ApplicationCategory.DesignSoftware,
                    DetectionReason = "Professional software vendor detected",
                    RecommendedMode = WhitelistMode.CompleteDisable,
                    ConfidenceScore = 0.8
                };
            }
        }

        return null;
    }

    private string NormalizeProcessName(string processName)
    {
        return processName.ToLowerInvariant().Replace(".exe", "");
    }

    private string GetApplicationDisplayName(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length > 0)
            {
                var process = processes[0];
                var displayName = process.MainModule?.FileVersionInfo?.ProductName;
                if (!string.IsNullOrEmpty(displayName))
                {
                    return displayName;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not get display name for {ProcessName}", processName);
        }

        // Fallback to formatted process name
        return char.ToUpper(processName[0]) + processName[1..];
    }

    private ApplicationCategory DetermineApplicationCategory(string processName)
    {
        if (_knownApplications.TryGetValue(processName, out var known))
        {
            return known.category;
        }

        return ApplicationCategory.Unknown;
    }

    private string GetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task LoadWhitelistAsync()
    {
        try
        {
            if (File.Exists(_whitelistFilePath))
            {
                var json = await File.ReadAllTextAsync(_whitelistFilePath);
                var apps = JsonSerializer.Deserialize<List<WhitelistedApplication>>(json);
                
                if (apps != null)
                {
                    lock (_whitelistLock)
                    {
                        _whitelist.Clear();
                        foreach (var app in apps)
                        {
                            _whitelist[app.ProcessName] = app;
                        }
                    }
                }
            }

            await InitializeBuiltInWhitelistAsync();
            _logger.LogInformation("Loaded {Count} applications from whitelist", _whitelist.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load application whitelist");
        }
    }

    private async Task InitializeBuiltInWhitelistAsync()
    {
        // Add some essential built-in applications if they don't exist
        var builtInApps = new[]
        {
            ("photoshop", WhitelistMode.CompleteDisable, "Adobe Photoshop - Color accuracy critical"),
            ("lightroom", WhitelistMode.CompleteDisable, "Adobe Lightroom - Photo editing"),
            ("illustrator", WhitelistMode.CompleteDisable, "Adobe Illustrator - Vector design"),
            ("premiere", WhitelistMode.CompleteDisable, "Adobe Premiere Pro - Video editing"),
            ("figma", WhitelistMode.CompleteDisable, "Figma - UI/UX design tool")
        };

        foreach (var (processName, mode, reason) in builtInApps)
        {
            lock (_whitelistLock)
            {
                if (!_whitelist.ContainsKey(processName))
                {
                    _whitelist[processName] = new WhitelistedApplication
                    {
                        ProcessName = processName,
                        DisplayName = GetApplicationDisplayName(processName),
                        Mode = mode,
                        Reason = reason,
                        IsBuiltIn = true,
                        IsAutoDetected = false,
                        Category = DetermineApplicationCategory(processName)
                    };
                }
            }
        }
    }

    private async Task SaveWhitelistAsync()
    {
        try
        {
            List<WhitelistedApplication> apps;
            lock (_whitelistLock)
            {
                apps = _whitelist.Values.ToList();
            }

            var json = JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_whitelistFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save application whitelist");
        }
    }

    private async Task EnsureWhitelistLoadedAsync()
    {
        // This is a simple check - in a more complex scenario you might use a proper initialization flag
        if (_whitelist.Count == 0)
        {
            await LoadWhitelistAsync();
        }
    }
}