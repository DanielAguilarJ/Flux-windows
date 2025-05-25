using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ChronoGuard.App.Converters;

/// <summary>
/// Converts boolean values to colors
/// </summary>
public class BooleanToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || parameter is not string paramString)
            return DependencyProperty.UnsetValue;

        var colors = paramString.Split('|');
        if (colors.Length != 2)
            return DependencyProperty.UnsetValue;

        var colorString = boolValue ? colors[0] : colors[1];
        
        try
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(colorString);
        }
        catch
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts string to visibility (empty/null = Collapsed, otherwise Visible)
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts color temperature (1000-10000K) to margin for slider position
/// </summary>
public class TemperatureToMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double temperature)
            return new Thickness(0);

        // Map temperature from 1000-10000K to 0-1 range
        var normalizedTemp = Math.Max(0, Math.Min(1, (temperature - 1000) / 9000));
        
        // Assume slider width is approximately 300px (will be adjusted by layout)
        var position = normalizedTemp * 288; // 300 - 12 (circle width)
        
        return new Thickness(position, 0, 0, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts null to boolean (null = false, not null = true)
/// </summary>
public class NotNullToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts a boolean value
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }
}

/// <summary>
/// Converts enum value to boolean for RadioButton binding
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        var enumValue = value.ToString();
        var targetValue = parameter.ToString();
        
        return string.Equals(enumValue, targetValue, StringComparison.InvariantCultureIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter != null)
        {
            return Enum.Parse(targetType, parameter.ToString()!);
        }
        return DependencyProperty.UnsetValue;
    }
}
