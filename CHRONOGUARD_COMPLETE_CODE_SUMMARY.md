# ChronoGuard - Complete Project Code Summary

*Generated on June 3, 2025*

## Table of Contents
1. [Project Overview](#project-overview)
2. [Architecture & Design Patterns](#architecture--design-patterns)
3. [Project Structure](#project-structure)
4. [Core Domain Layer](#core-domain-layer)
5. [Application Services Layer](#application-services-layer)
6. [Infrastructure Layer](#infrastructure-layer)
7. [Presentation Layer (WPF App)](#presentation-layer-wpf-app)
8. [Configuration System](#configuration-system)
9. [Testing Infrastructure](#testing-infrastructure)
10. [Key Technologies & Dependencies](#key-technologies--dependencies)
11. [Performance & Optimization Features](#performance--optimization-features)
12. [Security & Data Management](#security--data-management)

---

## Project Overview

**ChronoGuard** is a sophisticated blue light filtering application for Windows that automatically adjusts screen color temperature based on solar times and user preferences. The application features advanced color science, astronomical calculations, multi-monitor support, and modern UI design.

### Key Features
- **Automatic Color Temperature Adjustment**: Based on sunrise/sunset times
- **Advanced Color Science**: Perceptual color space interpolation and gamma ramp manipulation
- **Multi-Monitor Support**: Independent color profiles per monitor
- **Solar Calculations**: High-precision VSOP87 and NREL SPA algorithms
- **Performance Optimization**: SIMD acceleration, caching, and resource pooling
- **Modern UI**: WPF with MVVM pattern and Fluent Design elements
- **Configuration Management**: Secure settings persistence with backup/restore

---

## Architecture & Design Patterns

### Clean Architecture Implementation
```
┌─────────────────────────────────────────┐
│            Presentation Layer           │
│         (ChronoGuard.App)              │
│    WPF, ViewModels, Views, Controls    │
├─────────────────────────────────────────┤
│           Application Layer             │
│       (ChronoGuard.Application)        │
│  Background Services, Use Cases, DTOs   │
├─────────────────────────────────────────┤
│             Domain Layer                │
│         (ChronoGuard.Domain)           │
│   Entities, Interfaces, Business Logic  │
├─────────────────────────────────────────┤
│          Infrastructure Layer           │
│      (ChronoGuard.Infrastructure)      │
│  Services, Data Access, External APIs   │
└─────────────────────────────────────────┘
```

### Design Patterns Used
- **Clean Architecture**: Clear separation of concerns across layers
- **MVVM (Model-View-ViewModel)**: For WPF presentation layer
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Command Pattern**: RelayCommand for UI interactions
- **Repository Pattern**: Abstracted data access through interfaces
- **Service Layer Pattern**: Business logic separation
- **Background Service Pattern**: Long-running background tasks
- **Factory Pattern**: Object creation and configuration
- **Strategy Pattern**: Different color temperature algorithms

---

## Project Structure

### Solution Structure (91 C# Files)
```
ChronoGuard.sln
├── ChronoGuard.App/                 (Presentation Layer - 25 files)
│   ├── Program.cs                   (Application entry point)
│   ├── App.xaml.cs                  (WPF application lifecycle)
│   ├── MainWindow.xaml.cs           (Main UI window)
│   ├── ViewModels/                  (MVVM ViewModels)
│   ├── Views/                       (WPF Views and UserControls)
│   ├── Services/                    (UI-specific services)
│   └── Controls/                    (Custom WPF controls)
├── ChronoGuard.Application/         (Application Services - 8 files)
│   └── Services/
│       └── ChronoGuardBackgroundService.cs
├── ChronoGuard.Domain/              (Domain Entities - 15 files)
│   ├── Entities/                    (Core business entities)
│   ├── Interfaces/                  (Service contracts)
│   ├── Configuration/               (Configuration models)
│   └── Models/                      (Domain models)
├── ChronoGuard.Infrastructure/      (Infrastructure - 18 files)
│   └── Services/                    (Service implementations)
├── ChronoGuard.Tests/               (Unit Tests - 15 files)
├── ChronoGuard.Tests.Core/          (Core Test Utilities - 5 files)
├── ChronoGuard.TestApp/             (Development Testing - 1 file)
└── ChronoGuard.UI/                  (Additional UI Components - 4 files)
```

---

## Core Domain Layer

### Key Entities

#### 1. ColorTemperature.cs
**Purpose**: Represents color temperature values with RGB conversion
```csharp
public record ColorTemperature
{
    public const int MinKelvin = 1000;
    public const int MaxKelvin = 10000;
    public const int DefaultDayKelvin = 6500;
    public const int DefaultNightKelvin = 2700;

    public int Kelvin { get; init; }
    public (byte R, byte G, byte B) RGB { get; init; }
    
    // Tanner Helland's algorithm for RGB calculation
    private static (byte R, byte G, byte B) CalculateRGB(int kelvin)
    // Descriptive names for temperature ranges
    public string GetDescription()
}
```

**Key Features**:
- Immutable record type for value semantics
- Automatic RGB calculation using Tanner Helland's algorithm
- Validation for supported temperature range (1000K-10000K)
- Descriptive names in Spanish for temperature ranges

#### 2. AppState.cs
**Purpose**: Manages application state and transition logic
```csharp
public class AppState
{
    public bool IsEnabled { get; set; } = true;
    public bool IsPaused { get; set; } = false;
    public string ActiveProfileId { get; set; } = "classic";
    public DateTime? PausedUntil { get; set; }
    public Location? CurrentLocation { get; set; }
    public SolarTimes? TodaySolarTimes { get; set; }
    public ColorTemperature? CurrentTemperature { get; set; }
    public TransitionState? ActiveTransition { get; set; }
    public List<string> ExcludedApplications { get; set; } = new();

    // Smart state checking methods
    public bool IsActive => IsEnabled && !IsPaused && (!PausedUntil.HasValue || DateTime.UtcNow > PausedUntil);
    public bool ShouldApplyFiltering(string? currentApp = null)
    public void PauseFor(TimeSpan duration)
    public void PauseUntilSunrise()
}
```

**Key Features**:
- Comprehensive state management for the application
- Smart filtering logic based on excluded applications
- Flexible pause functionality (duration-based or until sunrise)
- Real-time state validation

#### 3. ColorTransition.cs
**Purpose**: Advanced color temperature transition engine
```csharp
public class ColorTransition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public TransitionEasingType EasingType { get; set; } = TransitionEasingType.SigmoidSmooth;
    public bool UsePerceptualInterpolation { get; set; } = true;
    public double AdaptiveSpeedFactor { get; set; } = 1.0;

    // Advanced interpolation methods
    private static ColorTemperature InterpolatePerceptual(ColorTemperature from, ColorTemperature to, double progress)
    private static (double l, double a, double b) KelvinToLab(int kelvin)
    
    // Sophisticated easing functions
    private static double SigmoidSmooth(double t)
    private static double CircadianAdaptive(double t)
}
```

**Supported Easing Types**:
- `Linear`: Constant speed transition
- `EaseInQuad/EaseOutQuad/EaseInOutQuad`: Quadratic easing
- `EaseInCubic/EaseOutCubic/EaseInOutCubic`: Cubic easing
- `SigmoidSmooth`: Perceptually optimal sigmoid curve
- `CircadianAdaptive`: Adaptive based on time of day
- `ExponentialDecay`: Natural exponential transition

**Key Features**:
- Perceptual color space interpolation using CIE L*a*b*
- Multiple easing algorithms for natural transitions
- Adaptive timing based on circadian rhythms
- Multi-monitor support with per-monitor transitions

### Core Interfaces

#### Service Contracts
```csharp
// Core color temperature management
public interface IColorTemperatureService
{
    Task<ColorTemperature> GetCurrentTemperatureAsync();
    Task SetTemperatureAsync(ColorTemperature temperature);
    Task SetTemperatureAsync(ColorTemperature temperature, string? monitorId);
    Task StartTransitionAsync(ColorTransition transition);
    event EventHandler<ColorTemperature>? TemperatureChanged;
}

// Solar time calculations
public interface ISolarCalculatorService
{
    Task<SolarTimes> CalculateSolarTimesAsync(Location location, DateTime date);
    Task<SolarTimes?> GetTodaySolarTimesAsync();
    bool IsPolarRegion(Location location, DateTime date);
}

// Geographic location services
public interface ILocationService
{
    Task<Location?> GetCurrentLocationAsync();
    Task<Location?> GetLocationByIpAsync();
    Task SetLocationAsync(Location location);
}

// Configuration management
public interface IConfigurationService
{
    Task<AppConfiguration> GetConfigurationAsync();
    Task SaveConfigurationAsync(AppConfiguration configuration);
    Task<T?> GetValueAsync<T>(string key);
    Task SetValueAsync<T>(string key, T value);
}
```

---

## Application Services Layer

### ChronoGuardBackgroundService.cs
**Purpose**: High-performance background service with intelligent optimization

**Key Features**:
- **Predictive Color Temperature Calculations**: Pre-calculates future temperatures
- **Dynamic Update Intervals**: Adjusts frequency based on system load
- **Resource Optimization**: Semaphore-based concurrency control
- **Error Recovery**: Exponential backoff for failed operations
- **Performance Monitoring**: Tracks operation timings and system impact

**Core Methods**:
```csharp
public class ChronoGuardBackgroundService : BackgroundService
{
    // Main execution loop with adaptive timing
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    
    // Optimized periodic updates with intelligent scheduling
    private async Task StartOptimizedPeriodicUpdatesAsync(CancellationToken cancellationToken)
    
    // Predictive calculation cache for smooth performance
    private async Task UpdatePredictiveCacheAsync()
    
    // Smart color temperature calculation with caching
    private async Task<ColorTemperature> CalculateOptimalTemperatureAsync()
    
    // Adaptive interval calculation based on system state
    private TimeSpan CalculateOptimalUpdateInterval()
}
```

**Performance Optimizations**:
- **Predictive Caching**: Pre-calculates color temperatures for next 24 hours
- **Adaptive Intervals**: Adjusts update frequency (10s to 5min) based on conditions
- **Resource Pooling**: Reuses expensive objects and handles
- **Concurrent Control**: Semaphore limits concurrent operations
- **Error Tracking**: Exponential backoff for consecutive failures

---

## Infrastructure Layer

### 1. WindowsColorTemperatureService.cs (2517 lines)
**Purpose**: Core color temperature manipulation with hardware gamma ramp control

**Key Features**:
- **Hardware Gamma Ramp Manipulation**: Direct GPU gamma table modification
- **Multi-Monitor Support**: Independent control of each display
- **ICC Profile Integration**: Fallback to ICC profiles when hardware fails
- **Performance Optimization**: Device context pooling and caching
- **SIMD Acceleration**: Vectorized gamma calculations

**Core Components**:
```csharp
public class WindowsColorTemperatureService : IColorTemperatureService
{
    // Gamma ramp structure for hardware manipulation
    [StructLayout(LayoutKind.Sequential)]
    private struct RAMP
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Red, Green, Blue;
    }
    
    // Intelligent caching system
    private readonly Dictionary<string, RAMP> _gammaRampCache = new();
    private readonly Dictionary<string, DateTime> _cacheTimestamps = new();
    
    // Device context pooling for performance
    private readonly Queue<IntPtr> _availableDeviceContexts = new();
    private readonly HashSet<IntPtr> _activeDeviceContexts = new();
    
    // Core gamma manipulation methods
    private async Task<bool> SetGammaRampAsync(string monitorId, RAMP ramp)
    private RAMP CalculateGammaRamp(ColorTemperature temperature, MonitorColorProfile profile)
    private async Task<IntPtr> AcquireDeviceContextAsync(string monitorId)
}
```

**Windows API Integration**:
```csharp
// Core GDI32 functions for gamma manipulation
[DllImport("gdi32.dll")]
private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

[DllImport("gdi32.dll")]
private static extern bool GetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

// Monitor detection and management
[DllImport("user32.dll")]
private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

// Physical monitor access for DDC/CI
[DllImport("dxva2.dll")]
private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);
```

### 2. SolarCalculatorService.cs
**Purpose**: High-precision solar calculations with VSOP87 and NREL SPA algorithms

**Key Features**:
- **Sub-minute Accuracy**: NREL Solar Position Algorithm (SPA) implementation
- **Atmospheric Corrections**: Refraction and elevation adjustments
- **Polar Region Support**: Handles midnight sun and polar night
- **Multiple Twilight Types**: Civil, nautical, and astronomical twilight

**Core Algorithm**:
```csharp
private (DateTime sunrise, DateTime sunset) CalculateSunriseSunset(double latitude, double longitude, DateTime date)
{
    // Convert to Julian day for precision
    var julianDay = DateTimeToJulianDay(date);
    var deltaT = CalculateDeltaT(date.Year); // Earth rotation correction
    
    // Iterative method for precise calculation
    var sunrise = FindSolarEvent(latitude, longitude, julianDay, deltaT, true, CIVIL_TWILIGHT_ANGLE);
    var sunset = FindSolarEvent(latitude, longitude, julianDay, deltaT, false, CIVIL_TWILIGHT_ANGLE);
    
    // Apply atmospheric corrections
    sunrise = ApplyAtmosphericCorrections(sunrise, latitude, longitude, true);
    sunset = ApplyAtmosphericCorrections(sunset, latitude, longitude, false);
    
    return (JulianDayToDateTime(sunrise).ToLocalTime(), JulianDayToDateTime(sunset).ToLocalTime());
}
```

### 3. ConfigurationService.cs
**Purpose**: Comprehensive configuration management with persistence

**Key Features**:
- **JSON-based Storage**: Human-readable configuration files
- **Default Value Merging**: Ensures all settings have valid defaults
- **Atomic Operations**: Thread-safe configuration updates
- **Change Notifications**: Event-driven configuration updates

**Configuration Structure**:
```csharp
public class AppConfiguration
{
    public GeneralSettings General { get; set; } = new();
    public LocationSettings Location { get; set; } = new();
    public NotificationSettings Notifications { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
```

### 4. Additional Infrastructure Services

#### LocationService.cs
- **Automatic Location Detection**: Windows Location API integration
- **IP-based Geolocation**: Fallback location detection
- **Manual Location Override**: User-specified coordinates
- **Privacy Protection**: Configurable location sharing

#### NotificationService.cs
- **Windows Toast Notifications**: Native Windows 10/11 notifications
- **Quiet Hours Support**: Automatic notification suppression
- **Adaptive Notifications**: Context-aware notification levels
- **Sleep Reminders**: Health-focused features

#### UpdateService.cs
- **Automatic Updates**: Background update checking
- **Secure Downloads**: Cryptographic signature verification
- **Silent Installation**: Non-disruptive update process
- **Rollback Support**: Version rollback capabilities

#### PerformanceMonitoringService.cs
- **System Impact Tracking**: CPU, memory, and GPU usage monitoring
- **Adaptive Optimization**: Dynamic performance adjustments
- **Telemetry Collection**: Anonymous usage statistics
- **Health Monitoring**: Application performance metrics

---

## Presentation Layer (WPF App)

### Application Architecture

#### Program.cs - Dependency Injection Setup
```csharp
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                // Register core services
                services.AddSingleton<IColorTemperatureService, WindowsColorTemperatureService>();
                services.AddSingleton<ISolarCalculatorService, SolarCalculatorService>();
                services.AddSingleton<ILocationService, LocationService>();
                services.AddSingleton<IConfigurationService, ConfigurationService>();
                
                // Register application services
                services.AddSingleton<ChronoGuardBackgroundService>();
                services.AddSingleton<SystemTrayService>();
                
                // Register ViewModels
                services.AddTransient<MainWindowViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<PerformanceMonitoringViewModel>();
                
                // Register Windows
                services.AddTransient<MainWindow>();
            })
            .Build();

        var app = new App();
        app.InitializeComponent();
        
        var mainWindow = host.Services.GetRequiredService<MainWindow>();
        app.MainWindow = mainWindow;
        
        app.Run();
    }
}
```

#### App.xaml.cs - Application Lifecycle
```csharp
public partial class App : Application
{
    private IHost? _host;
    private SystemTrayService? _systemTrayService;
    private ChronoGuardBackgroundService? _backgroundService;
    
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Initialize dependency injection
        _host = CreateHostBuilder(e.Args).Build();
        await _host.StartAsync();
        
        // Start background services
        _backgroundService = _host.Services.GetRequiredService<ChronoGuardBackgroundService>();
        _systemTrayService = _host.Services.GetRequiredService<SystemTrayService>();
        
        // Initialize system tray
        _systemTrayService.Initialize();
        
        // Show main window or start minimized
        var settings = await _host.Services.GetRequiredService<IConfigurationService>().GetConfigurationAsync();
        if (!settings.General.StartMinimized)
        {
            MainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }
    }
}
```

### Key ViewModels

#### MainWindowViewModel.cs
**Purpose**: Main application UI logic with MVVM binding
```csharp
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private bool isEnabled = true;
    [ObservableProperty] private int currentTemperature = 6500;
    [ObservableProperty] private string currentProfile = "Classic";
    [ObservableProperty] private bool isTransitioning = false;
    [ObservableProperty] private double transitionProgress = 0.0;
    
    // Commands for UI interactions
    [RelayCommand] private async Task ToggleEnabledAsync()
    [RelayCommand] private async Task PauseForHourAsync()
    [RelayCommand] private async Task PauseUntilSunriseAsync()
    [RelayCommand] private void OpenSettings()
    [RelayCommand] private void ShowAbout()
    
    // Real-time updates from background service
    private void OnBackgroundServiceStateChanged(object? sender, AppState newState)
    {
        CurrentTemperature = newState.CurrentColorTemperature;
        IsEnabled = newState.IsActive;
        IsTransitioning = newState.IsTransitioning;
        // Update UI properties...
    }
}
```

#### SettingsViewModel.cs
**Purpose**: Comprehensive settings management UI
```csharp
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty] private AppConfiguration configuration = new();
    [ObservableProperty] private bool isDirty = false;
    [ObservableProperty] private List<string> availableLanguages = new();
    [ObservableProperty] private List<MonitorInfo> availableMonitors = new();
    
    // Setting categories
    [RelayCommand] private void SelectGeneralTab()
    [RelayCommand] private void SelectScheduleTab()
    [RelayCommand] private void SelectAdvancedTab()
    [RelayCommand] private void SelectAboutTab()
    
    // Configuration management
    [RelayCommand] private async Task SaveConfigurationAsync()
    [RelayCommand] private async Task ResetToDefaultsAsync()
    [RelayCommand] private async Task ImportConfigurationAsync()
    [RelayCommand] private async Task ExportConfigurationAsync()
}
```

### Modern UI Components

#### Custom Controls
- **ColorTemperatureSlider**: Custom slider with temperature visualization
- **TransitionProgressBar**: Animated progress indicator for transitions
- **MonitorConfigurationPanel**: Multi-monitor setup interface
- **ProfileSelector**: Visual profile selection with previews
- **ScheduleViewer**: Timeline visualization of daily schedule

#### Views Structure
```
Views/
├── MainView.xaml                    (Primary application interface)
├── SettingsView.xaml               (Comprehensive settings panel)
├── PerformanceMonitoringView.xaml  (Performance metrics display)
├── MonitorManagementView.xaml      (Multi-monitor configuration)
├── Onboarding/                     (First-run setup wizard)
│   ├── Step1View.xaml              (Welcome and permissions)
│   ├── Step2View.xaml              (Location setup)
│   ├── Step3View.xaml              (Schedule configuration)
│   └── Step4View.xaml              (Completion and summary)
└── Tutorial/                       (Interactive help system)
    ├── TutorialStep1.xaml          (Basic features)
    ├── TutorialStep2.xaml          (Advanced settings)
    └── TutorialStep3.xaml          (Tips and tricks)
```

---

## Configuration System

### Hierarchical Configuration Architecture

#### ConfigurationPersistenceService.cs (950+ lines)
**Purpose**: Secure configuration storage with backup and encryption

**Key Features**:
- **Encrypted Storage**: AES-256 encryption for sensitive settings
- **Automatic Backups**: Rolling backup system with versioning
- **Validation**: Schema validation and data integrity checks
- **Migration**: Automatic configuration format upgrades

**Core Configuration Classes**:
```csharp
public class ChronoGuardConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public int DayColorTemperature { get; set; } = 6500;
    public int NightColorTemperature { get; set; } = 3000;
    public int TransitionDurationMinutes { get; set; } = 60;
    public TimeSpan SunsetOffset { get; set; } = TimeSpan.Zero;
    public TimeSpan SunriseOffset { get; set; } = TimeSpan.Zero;
    public bool EnableAutomaticLocation { get; set; } = true;
    public bool EnableTransitions { get; set; } = true;
    public bool EnableAtStartup { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool EnableHotkeys { get; set; } = true;
    
    // Advanced color management
    public AdvancedColorManagementConfig ColorManagement { get; set; } = new();
    public List<ColorProfile> ColorProfiles { get; set; } = new();
    public Dictionary<string, MonitorConfiguration> MonitorConfigurations { get; set; } = new();
}

public class AdvancedColorManagementConfig
{
    public bool UseAdvancedGammaAlgorithm { get; set; } = true;
    public bool EnableHardwareAcceleration { get; set; } = true;
    public bool EnableICCProfileIntegration { get; set; } = false;
    public bool UsePerceptualColorInterpolation { get; set; } = true;
    public TransitionEasingType DefaultEasingType { get; set; } = TransitionEasingType.SigmoidSmooth;
    public int GammaRampCacheSize { get; set; } = 200;
    public TimeSpan GammaRampCacheExpiry { get; set; } = TimeSpan.FromMinutes(30);
}
```

### Configuration Storage Locations
```
%APPDATA%\ChronoGuard\
├── config.json                     (Main configuration file)
├── config.json.bak                 (Automatic backup)
├── profiles\                       (Color profiles)
│   ├── classic.json
│   ├── gaming.json
│   └── reading.json
├── monitors\                       (Monitor-specific settings)
│   ├── monitor_1.json
│   └── monitor_2.json
├── cache\                          (Performance caches)
│   ├── gamma_ramps.cache
│   └── solar_times.cache
└── logs\                           (Application logs)
    ├── chronoguard.log
    └── performance.log
```

---

## Testing Infrastructure

### Test Structure
```
ChronoGuard.Tests/                  (Unit Tests - 15 files)
├── Services/
│   ├── ChronoGuardBackgroundServiceTests.cs
│   ├── WindowsColorTemperatureServiceTests.cs
│   ├── WindowsColorTemperatureServicePerformanceTests.cs
│   ├── SolarCalculatorServiceTests.cs
│   └── UpdateServiceTests.cs
├── Entities/
│   └── ColorTransitionAdvancedTests.cs
└── ChronoGuardCoreTests.cs

ChronoGuard.Tests.Core/             (Core Test Utilities - 5 files)
└── ChronoGuardCoreTests.cs
```

### Testing Framework Configuration
```xml
<!-- ChronoGuard.Tests.csproj -->
<PackageReference Include="xunit" Version="2.9.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
<PackageReference Include="Moq" Version="4.20.69" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
```

### Key Test Classes

#### ChronoGuardBackgroundServiceTests.cs
```csharp
[TestClass]
public class ChronoGuardBackgroundServiceTests
{
    private Mock<IColorTemperatureService> _mockColorService;
    private Mock<ISolarCalculatorService> _mockSolarCalculator;
    private ChronoGuardBackgroundService _service;
    
    [TestInitialize]
    public void Setup()
    {
        _mockColorService = new Mock<IColorTemperatureService>();
        _mockSolarCalculator = new Mock<ISolarCalculatorService>();
        _service = new ChronoGuardBackgroundService(_mockColorService.Object, _mockSolarCalculator.Object);
    }
    
    [TestMethod]
    public async Task ExecuteAsync_ShouldStartPeriodicUpdates()
    [TestMethod]
    public async Task CalculateOptimalTemperature_ShouldReturnCorrectTemperature()
    [TestMethod]
    public async Task HandleTransition_ShouldCompleteSuccessfully()
}
```

#### WindowsColorTemperatureServicePerformanceTests.cs
```csharp
[TestClass]
public class WindowsColorTemperatureServicePerformanceTests
{
    [TestMethod]
    public async Task SetTemperatureAsync_Performance_ShouldCompleteWithin100ms()
    {
        // Performance benchmark tests
        var stopwatch = Stopwatch.StartNew();
        await _service.SetTemperatureAsync(new ColorTemperature(4000));
        stopwatch.Stop();
        
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 100, $"Operation took {stopwatch.ElapsedMilliseconds}ms");
    }
    
    [TestMethod]
    public async Task GammaRampCache_ShouldImprovePerformance()
    [TestMethod]
    public async Task DeviceContextPooling_ShouldReduceResourceUsage()
}
```

---

## Key Technologies & Dependencies

### Core Framework
```xml
<!-- Target Framework -->
<TargetFramework>net8.0-windows10.0.22621.0</TargetFramework>
<UseWPF>true</UseWPF>
<UseWindowsForms>true</UseWindowsForms>

<!-- Core Microsoft Extensions -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />

<!-- MVVM Framework -->
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />

<!-- Graphics and Visualization -->
<PackageReference Include="SkiaSharp" Version="3.116.1" />
<PackageReference Include="SkiaSharp.Views.WPF" Version="3.116.1" />
<PackageReference Include="LiveChartsCore.SkiaSharpView.WPF" Version="2.0.0-rc2" />
<PackageReference Include="OpenTK.Compute" Version="5.0.0" />
<PackageReference Include="OpenTK.GLWpfControl" Version="4.2.3" />

<!-- System Integration -->
<PackageReference Include="System.Management" Version="8.0.0" />
<PackageReference Include="System.Drawing.Common" Version="8.0.7" />
<PackageReference Include="System.Text.Json" Version="8.0.5" />
```

### Testing Dependencies
```xml
<!-- Testing Framework -->
<PackageReference Include="xunit" Version="2.9.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />

<!-- Mocking and Test Utilities -->
<PackageReference Include="Moq" Version="4.20.69" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
```

---

## Performance & Optimization Features

### 1. SIMD Acceleration (SimdOptimizedGammaService.cs)
```csharp
public class SimdOptimizedGammaService : IGammaService
{
    // Vector operations for gamma ramp calculations
    public unsafe void CalculateGammaRampVectorized(ColorTemperature temperature, Span<ushort> red, Span<ushort> green, Span<ushort> blue)
    {
        var tempVector = Vector256.Create((float)temperature.Kelvin);
        var redMultiplier = Vector256.Create(temperature.RGB.R / 255.0f);
        var greenMultiplier = Vector256.Create(temperature.RGB.G / 255.0f);
        var blueMultiplier = Vector256.Create(temperature.RGB.B / 255.0f);
        
        // Process 8 values at once using AVX2
        for (int i = 0; i < 256; i += 8)
        {
            var indices = Vector256.Create(i, i+1, i+2, i+3, i+4, i+5, i+6, i+7);
            var normalizedIndices = Vector256.Divide(indices, Vector256.Create(255.0f));
            
            // Vectorized gamma calculation
            var redValues = Vector256.Multiply(normalizedIndices, redMultiplier);
            var greenValues = Vector256.Multiply(normalizedIndices, greenMultiplier);
            var blueValues = Vector256.Multiply(normalizedIndices, blueMultiplier);
            
            // Store results
            StoreVectorizedResults(redValues, greenValues, blueValues, red, green, blue, i);
        }
    }
}
```

### 2. Intelligent Caching System
```csharp
// Gamma ramp caching for WindowsColorTemperatureService
private readonly Dictionary<string, RAMP> _gammaRampCache = new();
private readonly Dictionary<string, DateTime> _cacheTimestamps = new();
private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);
private readonly int _maxCacheSize = 200;

private bool TryGetCachedGammaRamp(string cacheKey, out RAMP ramp)
{
    if (_gammaRampCache.TryGetValue(cacheKey, out ramp) &&
        _cacheTimestamps.TryGetValue(cacheKey, out var timestamp) &&
        DateTime.UtcNow - timestamp < _cacheExpiry)
    {
        return true;
    }
    
    return false;
}
```

### 3. Device Context Pooling
```csharp
// Resource pooling for optimal performance
private readonly Queue<IntPtr> _availableDeviceContexts = new();
private readonly HashSet<IntPtr> _activeDeviceContexts = new();
private readonly Dictionary<IntPtr, DateTime> _deviceContextTimestamps = new();

private async Task<IntPtr> AcquireDeviceContextAsync(string monitorId)
{
    if (_availableDeviceContexts.TryDequeue(out var context))
    {
        _activeDeviceContexts.Add(context);
        return context;
    }
    
    // Create new context if pool is empty
    var newContext = GetDC(GetMonitorHandle(monitorId));
    _activeDeviceContexts.Add(newContext);
    return newContext;
}
```

### 4. Adaptive Update Intervals
```csharp
// Dynamic performance optimization in ChronoGuardBackgroundService
private TimeSpan CalculateOptimalUpdateInterval()
{
    var baseInterval = TimeSpan.FromSeconds(30);
    
    // Adjust based on system load
    if (_systemLoad > 80) return TimeSpan.FromMinutes(2);
    if (_activeTransition != null) return TimeSpan.FromSeconds(10);
    if (_recentErrors > 3) return TimeSpan.FromMinutes(5);
    
    return baseInterval;
}
```

### 5. Performance Monitoring
```csharp
public class PerformanceMonitoringService : IPerformanceMonitoringService
{
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memoryCounter;
    private readonly Dictionary<string, List<double>> _operationTimings = new();
    
    public async Task<PerformanceMetrics> GetCurrentMetricsAsync()
    {
        return new PerformanceMetrics
        {
            CpuUsage = _cpuCounter.NextValue(),
            MemoryUsage = GC.GetTotalMemory(false),
            GpuUsage = await GetGpuUsageAsync(),
            AverageOperationTime = CalculateAverageOperationTime(),
            CacheHitRatio = CalculateCacheHitRatio()
        };
    }
}
```

---

## Security & Data Management

### 1. Configuration Encryption
```csharp
public class ConfigurationPersistenceService : IConfigurationPersistenceService
{
    private readonly AesCryptoServiceProvider _encryptionProvider;
    private readonly byte[] _encryptionKey;
    
    public async Task SaveEncryptedConfigurationAsync(ChronoGuardConfiguration config)
    {
        var json = JsonSerializer.Serialize(config);
        var encryptedData = _encryptionProvider.CreateEncryptor(_encryptionKey, _initVector)
            .TransformFinalBlock(Encoding.UTF8.GetBytes(json), 0, json.Length);
        
        await File.WriteAllBytesAsync(_configFilePath, encryptedData);
    }
}
```

### 2. Secure Update System
```csharp
public class UpdateService : IUpdateService
{
    public async Task<bool> VerifyUpdateSignatureAsync(string filePath, string signatureBase64)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(Convert.FromBase64String(_publicKey), out _);
        
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var signature = Convert.FromBase64String(signatureBase64);
        
        return rsa.VerifyData(fileBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}
```

### 3. Privacy Protection
```csharp
public class LocationService : ILocationService
{
    public async Task<Location?> GetCurrentLocationAsync()
    {
        // Check privacy settings
        var config = await _configService.GetConfigurationAsync();
        if (!config.Location.AllowLocationAccess)
        {
            return config.Location.ManualLocation;
        }
        
        // Use most private method first
        switch (config.Location.Method)
        {
            case LocationMethod.Manual:
                return config.Location.ManualLocation;
            case LocationMethod.WindowsLocationApi:
                return await GetLocationFromWindowsApiAsync();
            case LocationMethod.IpAddress:
                return await GetLocationByIpAsync();
            default:
                return await GetBestAvailableLocationAsync();
        }
    }
}
```

---

## Summary

ChronoGuard represents a sophisticated, production-ready blue light filtering application with:

### **Technical Excellence**
- **91 C# files** across 8 distinct project modules
- **Clean Architecture** with clear separation of concerns
- **Advanced Color Science** with perceptual interpolation
- **High-Performance Optimizations** including SIMD acceleration
- **Comprehensive Testing** with unit and performance tests

### **Core Capabilities**
- **Automatic Color Temperature Adjustment** based on astronomical calculations
- **Multi-Monitor Support** with independent color profiles
- **Advanced Transition Algorithms** with multiple easing functions
- **Performance Monitoring** and adaptive optimization
- **Secure Configuration Management** with encryption and backups

### **Modern Architecture**
- **.NET 8.0** with WPF and modern C# features
- **Dependency Injection** throughout the application
- **MVVM Pattern** for clean UI separation
- **Background Services** for continuous operation
- **Event-Driven Architecture** for real-time updates

### **Production Features**
- **Automatic Updates** with cryptographic verification
- **Privacy Protection** with configurable location access
- **Resource Optimization** through caching and pooling
- **Error Recovery** with exponential backoff
- **Comprehensive Logging** for diagnostics and support

The project demonstrates enterprise-level software engineering practices while maintaining focus on user experience and system performance. Every component is designed for extensibility, testability, and maintainability, making it suitable for both individual use and commercial distribution.
