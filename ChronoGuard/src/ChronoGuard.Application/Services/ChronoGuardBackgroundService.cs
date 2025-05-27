using ChronoGuard.Domain.Entities;
using ChronoGuard.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using Timer = System.Threading.Timer;

namespace ChronoGuard.Application.Services;

/// <summary>
/// Core background service that manages the automatic color temperature adjustments
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
    private string? _currentForegroundApp;

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
        _logger.LogInformation("ChronoGuard background service starting...");

        try
        {
            await InitializeAsync();
            await StartPeriodicUpdatesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in background service");
            throw;
        }
    }

    private async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing ChronoGuard...");

        // Initialize built-in profiles
        await _profileService.InitializeBuiltInProfilesAsync();

        // Load current location
        await UpdateLocationAsync();

        // Calculate today's solar times
        await UpdateSolarTimesAsync();

        // Apply initial color temperature
        await UpdateColorTemperatureAsync();

        _logger.LogInformation("ChronoGuard initialization completed");
    }

    private async Task StartPeriodicUpdatesAsync(CancellationToken stoppingToken)
    {
        // Main update loop - check every 30 seconds
        _updateTimer = new Timer(async _ => await PeriodicUpdateAsync(), null, 
            TimeSpan.Zero, TimeSpan.FromSeconds(30));

        // Transition update loop - update active transitions every 5 seconds
        _transitionTimer = new Timer(async _ => await UpdateActiveTransitionAsync(), null,
            TimeSpan.Zero, TimeSpan.FromSeconds(5));

        // Wait for cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task PeriodicUpdateAsync()
    {
        try
        {
            var config = await _configService.GetConfigurationAsync();
            
            // Update location if needed
            if (ShouldUpdateLocation(config))
            {
                await UpdateLocationAsync();
            }

            // Update solar times if it's a new day
            if (_appState.TodaySolarTimes?.Date.Date != DateTime.Today)
            {
                await UpdateSolarTimesAsync();
            }

            // Update color temperature
            await UpdateColorTemperatureAsync();

            // Notify state changes
            StateChanged?.Invoke(this, _appState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic update");
        }
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
        
        // Restore original display settings
        await _colorService.RestoreOriginalSettingsAsync();
        
        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("ChronoGuard background service stopped");
    }
}
