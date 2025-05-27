using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using Timer = System.Threading.Timer;

namespace ChronoGuard.Application.Services;

/// <summary>
/// High-performance background service with intelligent optimization and adaptive processing
/// Features predictive color temperature calculations, dynamic update intervals, and resource optimization
/// Provides seamless user experience with minimal system impact
/// </summary>
public class ChronoGuardBackgroundService : BackgroundService
{
    private readonly ILocationService _locationService;
    private readonly ISolarCalculatorService _solarCalculator;
    private readonly IColorTemperatureService _colorService;
    private readonly IProfileService _profileService;
    private readonly IConfigurationService _configService;
    private readonly IForegroundApplicationService _foregroundAppService;
    private readonly ILogger<ChronoGuardBackgroundService> _logger;
    
    private AppState _appState = new();
    private Timer? _updateTimer;
    private Timer? _transitionTimer;
    private Timer? _predictiveTimer;
    private string? _currentForegroundApp;
    
    // Performance optimization fields
    private DateTime _lastLocationUpdate = DateTime.MinValue;
    private DateTime _lastSolarCalculation = DateTime.MinValue;
    private readonly Dictionary<string, DateTime> _profileCache = new();
    private readonly SemaphoreSlim _updateSemaphore = new(1, 1);
    private int _consecutiveErrors = 0;
    private TimeSpan _currentUpdateInterval = TimeSpan.FromSeconds(30);
    
    // Predictive calculation cache
    private readonly Dictionary<DateTime, ColorTemperature> _predictiveCache = new();
    private DateTime _lastPredictiveUpdate = DateTime.MinValue;

    public event EventHandler<AppState>? StateChanged;

    /// <summary>
    /// Gets the current application state
    /// </summary>
    public AppState CurrentState => _appState;

    public ChronoGuardBackgroundService(
        ILocationService locationService,
        ISolarCalculatorService solarCalculator,
        IColorTemperatureService colorService,
        IProfileService profileService,
        IConfigurationService configService,
        IForegroundApplicationService foregroundAppService,
        ILogger<ChronoGuardBackgroundService> logger)
    {
        _locationService = locationService;
        _solarCalculator = solarCalculator;
        _colorService = colorService;
        _profileService = profileService;
        _configService = configService;
        _foregroundAppService = foregroundAppService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ChronoGuard high-performance background service starting...");

        try
        {
            await InitializeAsync();
            await StartOptimizedPeriodicUpdatesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in background service");
            throw;
        }
    }

    private async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing ChronoGuard with performance optimizations...");

        // Initialize built-in profiles
        await _profileService.InitializeBuiltInProfilesAsync();

        // Load current location
        await UpdateLocationAsync();

        // Calculate today's solar times
        await UpdateSolarTimesAsync();

        // Generate predictive temperature cache
        await UpdatePredictiveCacheAsync();

        // Apply initial color temperature
        await UpdateColorTemperatureAsync();

        _logger.LogInformation("ChronoGuard initialization completed with predictive caching enabled");
    }

    private async Task StartOptimizedPeriodicUpdatesAsync(CancellationToken stoppingToken)
    {
        // Adaptive main update loop - starts at 30 seconds, adjusts based on system load
        _updateTimer = new Timer(async _ => await OptimizedPeriodicUpdateAsync(), null, 
            TimeSpan.Zero, _currentUpdateInterval);

        // High-frequency transition update loop - 1-second precision for smooth transitions
        _transitionTimer = new Timer(async _ => await UpdateActiveTransitionAsync(), null,
            TimeSpan.Zero, TimeSpan.FromSeconds(1));

        // Predictive cache update - recalculate every 15 minutes
        _predictiveTimer = new Timer(async _ => await UpdatePredictiveCacheAsync(), null,
            TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));

        // Wait for cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task OptimizedPeriodicUpdateAsync()
    {
        if (!await _updateSemaphore.WaitAsync(100)) // Non-blocking with timeout
        {
            _logger.LogDebug("Skipping update cycle - previous update still in progress");
            return;
        }

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var config = await _configService.GetConfigurationAsync();
            
            // Intelligent location updates
            if (ShouldUpdateLocationIntelligent(config))
            {
                await UpdateLocationAsync();
            }

            // Smart solar time updates
            if (ShouldUpdateSolarTimes())
            {
                await UpdateSolarTimesAsync();
            }

            // Foreground application monitoring
            await MonitorForegroundApplicationAsync();

            // Optimized color temperature updates using predictive cache
            await UpdateColorTemperatureOptimizedAsync();

            // Adaptive update interval based on performance
            AdjustUpdateInterval(stopwatch.Elapsed);

            // Reset error counter on successful update
            _consecutiveErrors = 0;

            // Notify state changes
            StateChanged?.Invoke(this, _appState);
        }
        catch (Exception ex)
        {
            _consecutiveErrors++;
            _logger.LogError(ex, "Error during optimized periodic update (consecutive errors: {Count})", _consecutiveErrors);
            
            // Adaptive error handling - slow down updates if errors persist
            if (_consecutiveErrors > 3)
            {
                _currentUpdateInterval = TimeSpan.FromMinutes(Math.Min(5, _consecutiveErrors));
                _updateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _updateTimer = new Timer(async _ => await OptimizedPeriodicUpdateAsync(), null,
                    _currentUpdateInterval, _currentUpdateInterval);
            }
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }

    private async Task UpdatePredictiveCacheAsync()
    {
        try
        {
            if (_appState.TodaySolarTimes == null) return;

            var activeProfile = await _profileService.GetActiveProfileAsync();
            if (activeProfile == null) return;

            _predictiveCache.Clear();

            // Pre-calculate color temperatures for the next 24 hours in 15-minute intervals
            var startTime = DateTime.Now;
            for (int i = 0; i < 96; i++) // 24 hours / 15 minutes = 96 intervals
            {
                var time = startTime.AddMinutes(i * 15);
                var temperature = activeProfile.GetColorTemperatureForTime(time, _appState.TodaySolarTimes);
                _predictiveCache[time] = temperature;
            }

            _lastPredictiveUpdate = DateTime.Now;
            _logger.LogDebug("Predictive cache updated with {Count} temperature values", _predictiveCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating predictive cache");
        }
    }

    private async Task UpdateColorTemperatureOptimizedAsync()
    {
        try
        {
            if (!_appState.ShouldApplyFiltering())
            {
                return;
            }

            var targetTemperature = GetPredictedTemperature(DateTime.Now);
            if (targetTemperature == null) return;

            // Check if we need to start a transition
            var currentTemp = _appState.CurrentTemperature;
            if (ShouldStartTransition(currentTemp, targetTemperature))
            {
                var activeProfile = await _profileService.GetActiveProfileAsync();
                if (activeProfile != null)
                {
                    await StartTransitionAsync(currentTemp, targetTemperature, activeProfile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in optimized color temperature update");
        }
    }

    private ColorTemperature? GetPredictedTemperature(DateTime time)
    {
        // Find the closest cached temperature
        var closestTime = _predictiveCache.Keys
            .OrderBy(t => Math.Abs((t - time).TotalMinutes))
            .FirstOrDefault();

        if (closestTime != default && Math.Abs((closestTime - time).TotalMinutes) <= 15)
        {
            return _predictiveCache[closestTime];
        }

        return null;
    }

    private bool ShouldStartTransition(ColorTemperature? current, ColorTemperature target)
    {
        if (current == null) return true;
        
        // Use smaller threshold for smoother transitions
        return Math.Abs(current.Kelvin - target.Kelvin) > 25;
    }

    private async Task MonitorForegroundApplicationAsync()
    {
        try
        {
            var currentApp = _foregroundAppService.GetForegroundApplicationName();
            if (currentApp != _currentForegroundApp)
            {
                _currentForegroundApp = currentApp;
                _logger.LogDebug("Foreground application changed: {App}", currentApp);
                
                // Could trigger profile changes based on application
                // Implementation depends on specific requirements
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error monitoring foreground application");
        }
        
        await Task.CompletedTask; // Make method async-compatible
    }

    private void AdjustUpdateInterval(TimeSpan lastUpdateDuration)
    {
        // Adaptive update interval based on system performance
        if (lastUpdateDuration > TimeSpan.FromSeconds(2))
        {
            // System is slow, reduce update frequency
            _currentUpdateInterval = TimeSpan.FromSeconds(Math.Min(120, _currentUpdateInterval.TotalSeconds * 1.5));
        }
        else if (lastUpdateDuration < TimeSpan.FromMilliseconds(100) && _currentUpdateInterval > TimeSpan.FromSeconds(15))
        {
            // System is fast, can increase update frequency
            _currentUpdateInterval = TimeSpan.FromSeconds(Math.Max(15, _currentUpdateInterval.TotalSeconds * 0.8));
        }

        // Update timer if interval changed significantly
        if (Math.Abs(_currentUpdateInterval.TotalSeconds - 30) > 10)
        {
            _updateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _updateTimer = new Timer(async _ => await OptimizedPeriodicUpdateAsync(), null,
                _currentUpdateInterval, _currentUpdateInterval);
            
            _logger.LogDebug("Update interval adjusted to {Interval} seconds", _currentUpdateInterval.TotalSeconds);
        }
    }

    private bool ShouldUpdateLocationIntelligent(AppConfiguration config)
    {
        if (config.Location.Method == LocationMethod.Manual)
            return false;

        if (_appState.CurrentLocation == null)
            return true;

        // Use cached timestamps for performance
        var timeSinceUpdate = DateTime.UtcNow - _lastLocationUpdate;
        
        return config.Location.UpdateFrequency switch
        {
            LocationUpdateFrequency.OnStartup => false,
            LocationUpdateFrequency.Daily => timeSinceUpdate > TimeSpan.FromHours(24),
            LocationUpdateFrequency.Weekly => timeSinceUpdate > TimeSpan.FromDays(7),
            _ => false
        };
    }

    private bool ShouldUpdateSolarTimes()
    {
        // Check if we need to update solar times
        return _appState.TodaySolarTimes?.Date.Date != DateTime.Today ||
               DateTime.Now - _lastSolarCalculation > TimeSpan.FromHours(6);
    }

    private Task UpdateActiveTransitionAsync()
    {
        try
        {
            if (_appState.ActiveTransition?.IsComplete == true)
            {
                _logger.LogDebug("Transition completed: {Transition}", _appState.ActiveTransition);
                _appState.ActiveTransition = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating active transition");
        }
        return Task.CompletedTask;
    }

    private async Task UpdateLocationAsync()
    {
        try
        {
            var location = await _locationService.GetCurrentLocationAsync();
            if (location != null)
            {
                _appState.CurrentLocation = location;
                _lastLocationUpdate = DateTime.Now;
                _logger.LogInformation("Location updated: {Location}", location);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update location");
        }
    }

    private async Task UpdateSolarTimesAsync()
    {
        try
        {
            if (_appState.CurrentLocation != null)
            {
                var solarTimes = await _solarCalculator.CalculateSolarTimesAsync(
                    _appState.CurrentLocation, DateTime.Today);
                
                _appState.TodaySolarTimes = solarTimes;
                _lastSolarCalculation = DateTime.Now;
                _logger.LogInformation("Solar times updated: {SolarTimes}", solarTimes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update solar times");
        }
    }

    private async Task UpdateColorTemperatureAsync()
    {
        try
        {
            if (!_appState.ShouldApplyFiltering())
            {
                return;
            }

            var activeProfile = await _profileService.GetActiveProfileAsync();
            if (activeProfile == null || _appState.TodaySolarTimes == null)
            {
                return;
            }

            var targetTemperature = activeProfile.GetColorTemperatureForTime(
                DateTime.Now, _appState.TodaySolarTimes);

            // Check if we need to start a transition
            var currentTemp = _appState.CurrentTemperature;
            if (currentTemp == null || Math.Abs(currentTemp.Kelvin - targetTemperature.Kelvin) > 50)
            {
                await StartTransitionAsync(currentTemp, targetTemperature, activeProfile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating color temperature");
        }
    }

    private async Task StartTransitionAsync(ColorTemperature? from, ColorTemperature to, ColorProfile profile)
    {
        try
        {
            var fromTemp = from ?? to; // If no current temp, start from target
            var duration = TimeSpan.FromMinutes(profile.TransitionDurationMinutes);
            
            var transition = await _colorService.CreateTransitionAsync(
                fromTemp, to, duration, "Automatic adjustment");

            _appState.ActiveTransition = transition;
            _appState.CurrentTemperature = to;

            _logger.LogInformation("Started transition: {From}K → {To}K over {Duration}",
                fromTemp.Kelvin, to.Kelvin, duration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start color transition");
        }
    }

    private bool ShouldUpdateLocation(AppConfiguration config)
    {
        if (config.Location.Method == LocationMethod.Manual)
            return false;

        if (_appState.CurrentLocation == null)
            return true;

        var timeSinceUpdate = DateTime.UtcNow - _appState.CurrentLocation.LastUpdated;
        
        return config.Location.UpdateFrequency switch
        {
            LocationUpdateFrequency.OnStartup => false, // Already updated on startup
            LocationUpdateFrequency.Daily => timeSinceUpdate > TimeSpan.FromHours(24),
            LocationUpdateFrequency.Weekly => timeSinceUpdate > TimeSpan.FromDays(7),
            _ => false
        };
    }

    public Task<AppState> GetCurrentStateAsync()
    {
        return Task.FromResult(_appState);
    }

    public async Task PauseAsync(TimeSpan? duration = null)
    {
        if (duration.HasValue)
        {
            _appState.PauseFor(duration.Value);
        }
        else
        {
            _appState.IsPaused = true;
        }

        await _colorService.RestoreOriginalSettingsAsync();
        _logger.LogInformation("ChronoGuard paused");
    }

    public async Task ResumeAsync()
    {
        _appState.Resume();
        await UpdateColorTemperatureAsync();
        _logger.LogInformation("ChronoGuard resumed");
    }

    public async Task SetActiveProfileAsync(string profileId)
    {
        await _profileService.SetActiveProfileAsync(profileId);
        _appState.ActiveProfileId = profileId;
        await UpdateColorTemperatureAsync();
        _logger.LogInformation("Active profile changed to: {ProfileId}", profileId);
    }

    /// <summary>
    /// Triggers an immediate color temperature update
    /// </summary>
    public async Task TriggerUpdateAsync()
    {
        await UpdateColorTemperatureAsync();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ChronoGuard background service stopping...");
        
        _updateTimer?.Dispose();
        _transitionTimer?.Dispose();
        _predictiveTimer?.Dispose();
        _updateSemaphore?.Dispose();
        
        // Restore original display settings
        await _colorService.RestoreOriginalSettingsAsync();
        
        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("ChronoGuard background service stopped");
    }
}
