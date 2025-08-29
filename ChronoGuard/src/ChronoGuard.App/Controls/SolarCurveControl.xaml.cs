using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ChronoGuard.Domain.Entities;

namespace ChronoGuard.App.Controls;

public partial class SolarCurveControl : UserControl
{
    public static readonly DependencyProperty SolarTimesProperty = DependencyProperty.Register(
        nameof(SolarTimes), typeof(SolarTimes), typeof(SolarCurveControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnDataChanged));

    public static readonly DependencyProperty NowProperty = DependencyProperty.Register(
        nameof(Now), typeof(DateTime), typeof(SolarCurveControl),
        new FrameworkPropertyMetadata(DateTime.Now, FrameworkPropertyMetadataOptions.AffectsRender, OnDataChanged));

    public SolarTimes? SolarTimes
    {
        get => (SolarTimes?)GetValue(SolarTimesProperty);
        set => SetValue(SolarTimesProperty, value);
    }

    public DateTime Now
    {
        get => (DateTime)GetValue(NowProperty);
        set => SetValue(NowProperty, value);
    }

    private Canvas? _canvas;
    private Ellipse? _sun;
    private Line? _nowLine;
    private Path? _curve;

    public SolarCurveControl()
    {
        // Build visual tree programmatically to avoid XAML-generated members
        var root = new Grid();

        var backgroundRect = new Rectangle();
        backgroundRect.Fill = new LinearGradientBrush
        {
            StartPoint = new Point(0, 1),
            EndPoint = new Point(0, 0),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(0x0D, 0x00, 0x00, 0x00), 0),
                new GradientStop(Color.FromArgb(0x1A, 0x00, 0x00, 0x00), 0.5),
                new GradientStop(Color.FromArgb(0x0D, 0x00, 0x00, 0x00), 1)
            }
        };
        root.Children.Add(backgroundRect);

        _canvas = new Canvas { Background = Brushes.Transparent };
        root.Children.Add(_canvas);

        Content = root;

        Loaded += (_, __) => Redraw(full: true);
        SizeChanged += (_, __) => Redraw(full: true);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SolarCurveControl s)
        {
            var full = e.Property == SolarTimesProperty;
            s.Redraw(full);
        }
    }

    private void Redraw(bool full)
    {
        if (_canvas == null) return;

        var accent = TryFindResource("AccentGradient") as Brush ?? new SolidColorBrush(Colors.Orange);

        if (SolarTimes == null)
        {
            _canvas.Children.Clear();
            var placeholder = new Path
            {
                Data = Geometry.Parse("M 0,150 Q 150,50 300,100 Q 450,75 600,130"),
                Stroke = accent,
                StrokeThickness = 3,
                Fill = Brushes.Transparent
            };
            _canvas.Children.Add(placeholder);
            return;
        }

        var width = ActualWidth > 0 ? ActualWidth : 600;
        var height = ActualHeight > 0 ? ActualHeight : 200;

        var sunrise = SolarTimes.Sunrise;
        var sunset = SolarTimes.Sunset;
        var noon = SolarTimes.SolarNoon;

        double TimeToX(DateTime t)
        {
            var minutes = t.TimeOfDay.TotalMinutes;
            return (minutes / (24 * 60.0)) * width;
        }

        double ElevationAt(DateTime t)
        {
            if (t <= sunrise || t >= sunset) return 0;
            var dayLen = (sunset - sunrise).TotalMinutes;
            var x = (t - sunrise).TotalMinutes / dayLen;
            var y = 4 * x * (1 - x);
            return y;
        }

        double ElevationToY(double e) => height - (e * (height - 40)) - 10;

        var xSunrise = TimeToX(sunrise);
        var xNoon = TimeToX(noon);
        var xSunset = TimeToX(sunset);
        var ySunrise = ElevationToY(0);
        var yNoon = ElevationToY(1);
        var ySunset = ElevationToY(0);

        if (full || _curve == null)
        {
            _canvas.Children.Clear();

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(xSunrise, ySunrise) };
            figure.Segments.Add(new QuadraticBezierSegment(new Point((xSunrise + xNoon) / 2, yNoon), new Point(xNoon, yNoon), true));
            figure.Segments.Add(new QuadraticBezierSegment(new Point((xNoon + xSunset) / 2, yNoon), new Point(xSunset, ySunset), true));
            geometry.Figures.Add(figure);

            _curve = new Path
            {
                Data = geometry,
                Stroke = accent,
                StrokeThickness = 3,
                Fill = Brushes.Transparent
            };
            _canvas.Children.Add(_curve);

            _nowLine = new Line
            {
                Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                StrokeDashArray = new DoubleCollection { 2, 4 },
                StrokeThickness = 1
            };
            _canvas.Children.Add(_nowLine);

            _sun = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new RadialGradientBrush(Colors.Gold, Color.FromArgb(160, 255, 200, 0)),
                Stroke = Brushes.OrangeRed,
                StrokeThickness = 1.5
            };
            _canvas.Children.Add(_sun);

            // Labels
            void AddLabel(string text, double x, double y)
            {
                var tb = new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.White,
                    FontSize = 10
                };
                Canvas.SetLeft(tb, Math.Max(0, Math.Min(width - 80, x - 40)));
                Canvas.SetTop(tb, Math.Max(0, y + 4));
                _canvas.Children.Add(tb);
            }

            AddLabel($"Amanecer {sunrise:HH:mm}", xSunrise, ySunrise);
            AddLabel($"Mediodía {noon:HH:mm}", xNoon, yNoon - 18);
            AddLabel($"Atardecer {sunset:HH:mm}", xSunset, ySunset);
        }

        // Update dynamic elements positions with animation
        var xNow = TimeToX(Now);
        var yNow = ElevationToY(ElevationAt(Now));

        if (_sun != null)
        {
            AnimateCanvasProperty(_sun, Canvas.LeftProperty, xNow - _sun.Width / 2);
            AnimateCanvasProperty(_sun, Canvas.TopProperty, yNow - _sun.Height / 2);
        }

        if (_nowLine != null)
        {
            _nowLine.X1 = xNow; _nowLine.X2 = xNow;
            _nowLine.Y1 = 0; _nowLine.Y2 = height;
        }
    }

    private static void AnimateCanvasProperty(DependencyObject target, DependencyProperty dp, double to)
    {
        var current = target.GetValue(dp);
        double from = current is double d ? d : double.NaN;
        var anim = new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(450),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        if (!double.IsNaN(from)) anim.From = from;
        Storyboard.SetTargetProperty(anim, new PropertyPath(dp));
        var sb = new Storyboard();
        sb.Children.Add(anim);
        Storyboard.SetTarget(sb, (DependencyObject)target);
        sb.Begin();
    }
}
