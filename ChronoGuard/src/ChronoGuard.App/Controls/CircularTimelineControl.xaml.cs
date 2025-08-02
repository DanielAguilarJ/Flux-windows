using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ChronoGuard.Domain.Entities;

namespace ChronoGuard.App.Controls;

/// <summary>
/// Interactive circular timeline control showing 24-hour color temperature cycle
/// Features real-time updates, solar event markers, and smooth animations
/// </summary>
public partial class CircularTimelineControl : UserControl
{
    private DispatcherTimer _updateTimer;
    private SolarTimes? _solarTimes;
    private ColorProfile? _colorProfile;

    // Dependency properties for data binding
    public static readonly DependencyProperty CurrentTemperatureProperty =
        DependencyProperty.Register(nameof(CurrentTemperature), typeof(ColorTemperature), typeof(CircularTimelineControl),
            new PropertyMetadata(new ColorTemperature(6500), OnCurrentTemperatureChanged));

    public static readonly DependencyProperty SolarTimesProperty =
        DependencyProperty.Register(nameof(SolarTimes), typeof(SolarTimes), typeof(CircularTimelineControl),
            new PropertyMetadata(null, OnSolarTimesChanged));

    public static readonly DependencyProperty ColorProfileProperty =
        DependencyProperty.Register(nameof(ColorProfile), typeof(ColorProfile), typeof(CircularTimelineControl),
            new PropertyMetadata(null, OnColorProfileChanged));

    public ColorTemperature CurrentTemperature
    {
        get => (ColorTemperature)GetValue(CurrentTemperatureProperty);
        set => SetValue(CurrentTemperatureProperty, value);
    }

    public SolarTimes? SolarTimes
    {
        get => (SolarTimes?)GetValue(SolarTimesProperty);
        set => SetValue(SolarTimesProperty, value);
    }

    public ColorProfile? ColorProfile
    {
        get => (ColorProfile?)GetValue(ColorProfileProperty);
        set => SetValue(ColorProfileProperty, value);
    }

    public CircularTimelineControl()
    {
        InitializeComponent();
        InitializeTimer();
        UpdateCurrentTimeIndicator();
    }

    private void InitializeTimer()
    {
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1) // Update every minute
        };
        _updateTimer.Tick += (s, e) => UpdateCurrentTimeIndicator();
        _updateTimer.Start();
    }

    private static void OnCurrentTemperatureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CircularTimelineControl control && e.NewValue is ColorTemperature temperature)
        {
            control.UpdateCurrentTemperatureDisplay(temperature);
        }
    }

    private static void OnSolarTimesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CircularTimelineControl control && e.NewValue is SolarTimes solarTimes)
        {
            control._solarTimes = solarTimes;
            control.UpdateSolarEventIndicators();
            control.UpdateTemperatureZones();
        }
    }

    private static void OnColorProfileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CircularTimelineControl control && e.NewValue is ColorProfile profile)
        {
            control._colorProfile = profile;
            control.UpdateTemperatureZones();
        }
    }

    private void UpdateCurrentTimeIndicator()
    {
        var now = DateTime.Now;
        var angle = (now.Hour % 12) * 30 + now.Minute * 0.5; // 12-hour format for better readability
        
        CurrentTimeRotation.Angle = angle;
        
        // Update next event text
        UpdateNextEventText(now);
        
        // Update current phase
        UpdateCurrentPhaseText(now);
    }

    private void UpdateCurrentTemperatureDisplay(ColorTemperature temperature)
    {
        CurrentTemperatureText.Text = $"{temperature.Kelvin}K";
        
        // Update indicator color based on temperature
        var brush = GetTemperatureBrush(temperature.Kelvin);
        CurrentTimeIndicator.Fill = brush;
        
        // Update glow effect color
        if (CurrentTimeIndicator.Effect is DropShadowEffect effect)
        {
            effect.Color = GetTemperatureColor(temperature.Kelvin);
        }
    }

    private void UpdateSolarEventIndicators()
    {
        if (_solarTimes == null) return;

        // Calculate angles for sunrise and sunset
        var sunriseAngle = TimeToAngle(_solarTimes.Sunrise);
        var sunsetAngle = TimeToAngle(_solarTimes.Sunset);

        SunriseRotation.Angle = sunriseAngle;
        SunsetRotation.Angle = sunsetAngle;
    }

    private void UpdateTemperatureZones()
    {
        if (_solarTimes == null || _colorProfile == null) return;

        // Clear existing zones
        TemperatureZones.Children.Clear();

        // Create temperature zone arcs
        CreateTemperatureZoneArcs();
    }

    private void CreateTemperatureZoneArcs()
    {
        if (_solarTimes == null || _colorProfile == null) return;

        const double radius = 100; // Inner circle radius
        const double centerX = 140;
        const double centerY = 140;

        // Day zone (sunrise to sunset)
        var dayPath = CreateArcPath(
            TimeToAngle(_solarTimes.Sunrise),
            TimeToAngle(_solarTimes.Sunset),
            radius, centerX, centerY);
        
        dayPath.Stroke = new SolidColorBrush(GetTemperatureColor(_colorProfile.DayTemperature));
        dayPath.StrokeThickness = 4;
        dayPath.Opacity = 0.6;
        TemperatureZones.Children.Add(dayPath);

        // Night zone (sunset to sunrise next day)
        var nightStartAngle = TimeToAngle(_solarTimes.Sunset);
        var nightEndAngle = TimeToAngle(_solarTimes.Sunrise.AddDays(1)); // Next day sunrise
        
        var nightPath = CreateArcPath(
            nightStartAngle,
            nightEndAngle > nightStartAngle ? nightEndAngle : nightEndAngle + 360,
            radius, centerX, centerY);
        
        nightPath.Stroke = new SolidColorBrush(GetTemperatureColor(_colorProfile.NightTemperature));
        nightPath.StrokeThickness = 4;
        nightPath.Opacity = 0.6;
        TemperatureZones.Children.Add(nightPath);

        // Transition zones if enabled
        if (_colorProfile.EnableSunriseTransition)
        {
            var sunriseTransitionStart = _solarTimes.Sunrise.AddMinutes(-_colorProfile.SunriseOffsetMinutes);
            var sunriseTransitionEnd = sunriseTransitionStart.AddMinutes(_colorProfile.TransitionDurationMinutes);
            
            var sunriseTransitionPath = CreateArcPath(
                TimeToAngle(sunriseTransitionStart),
                TimeToAngle(sunriseTransitionEnd),
                radius - 10, centerX, centerY);
            
            sunriseTransitionPath.Stroke = Brushes.Orange;
            sunriseTransitionPath.StrokeThickness = 2;
            sunriseTransitionPath.Opacity = 0.4;
            TemperatureZones.Children.Add(sunriseTransitionPath);
        }

        if (_colorProfile.EnableSunsetTransition)
        {
            var sunsetTransitionStart = _solarTimes.Sunset.AddMinutes(-_colorProfile.SunsetOffsetMinutes);
            var sunsetTransitionEnd = sunsetTransitionStart.AddMinutes(_colorProfile.TransitionDurationMinutes);
            
            var sunsetTransitionPath = CreateArcPath(
                TimeToAngle(sunsetTransitionStart),
                TimeToAngle(sunsetTransitionEnd),
                radius - 10, centerX, centerY);
            
            sunsetTransitionPath.Stroke = Brushes.OrangeRed;
            sunsetTransitionPath.StrokeThickness = 2;
            sunsetTransitionPath.Opacity = 0.4;
            TemperatureZones.Children.Add(sunsetTransitionPath);
        }
    }

    private Path CreateArcPath(double startAngle, double endAngle, double radius, double centerX, double centerY)
    {
        var path = new Path();
        var geometry = new PathGeometry();
        var figure = new PathFigure();

        // Convert angles to radians
        var startRad = startAngle * Math.PI / 180;
        var endRad = endAngle * Math.PI / 180;

        // Calculate start and end points
        var startX = centerX + radius * Math.Cos(startRad - Math.PI / 2);
        var startY = centerY + radius * Math.Sin(startRad - Math.PI / 2);
        var endX = centerX + radius * Math.Cos(endRad - Math.PI / 2);
        var endY = centerY + radius * Math.Sin(endRad - Math.PI / 2);

        figure.StartPoint = new Point(startX, startY);

        var arc = new ArcSegment
        {
            Point = new Point(endX, endY),
            Size = new Size(radius, radius),
            IsLargeArc = Math.Abs(endAngle - startAngle) > 180,
            SweepDirection = SweepDirection.Clockwise
        };

        figure.Segments.Add(arc);
        geometry.Figures.Add(figure);
        path.Data = geometry;

        return path;
    }

    private double TimeToAngle(DateTime time)
    {
        // Convert time of day to angle (0° = 12:00, 90° = 15:00, etc.)
        var totalMinutes = time.Hour * 60 + time.Minute;
        return (totalMinutes / 60.0) * 15.0; // 15 degrees per hour
    }

    private void UpdateNextEventText(DateTime currentTime)
    {
        if (_solarTimes == null)
        {
            NextEventText.Text = "Calculando eventos solares...";
            return;
        }

        var now = currentTime.TimeOfDay;
        var sunrise = _solarTimes.Sunrise.TimeOfDay;
        var sunset = _solarTimes.Sunset.TimeOfDay;

        TimeSpan timeToNext;
        string eventName;

        if (now < sunrise)
        {
            // Before sunrise
            timeToNext = sunrise - now;
            eventName = "Amanecer";
        }
        else if (now < sunset)
        {
            // Between sunrise and sunset
            timeToNext = sunset - now;
            eventName = "Atardecer";
        }
        else
        {
            // After sunset - next event is tomorrow's sunrise
            timeToNext = TimeSpan.FromDays(1) - now + sunrise;
            eventName = "Amanecer";
        }

        NextEventText.Text = $"{eventName} en {FormatTimeSpan(timeToNext)}";
    }

    private void UpdateCurrentPhaseText(DateTime currentTime)
    {
        if (_solarTimes == null)
        {
            CurrentPhaseText.Text = "Calculando...";
            return;
        }

        var phase = _solarTimes.GetDayPhase(currentTime);
        CurrentPhaseText.Text = phase switch
        {
            DayPhase.Night => "Noche",
            DayPhase.Sunrise => "Amanecer",
            DayPhase.Day => "Día",
            DayPhase.Sunset => "Atardecer",
            _ => "Desconocido"
        };
    }

    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
        {
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
        }
        else if (timeSpan.TotalHours >= 1)
        {
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
        }
        else
        {
            return $"{timeSpan.Minutes}m";
        }
    }

    private Brush GetTemperatureBrush(int kelvin)
    {
        var color = GetTemperatureColor(kelvin);
        return new SolidColorBrush(color);
    }

    private Color GetTemperatureColor(int kelvin)
    {
        // Convert temperature to RGB for visualization
        var temp = new ColorTemperature(Math.Max(1000, Math.Min(10000, kelvin)));
        return Color.FromRgb(temp.RGB.R, temp.RGB.G, temp.RGB.B);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _updateTimer?.Stop();
        base.OnUnloaded(e);
    }
}