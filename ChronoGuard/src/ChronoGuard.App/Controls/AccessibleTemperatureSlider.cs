using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using ChronoGuard.App.Accessibility;

namespace ChronoGuard.App.Controls;

/// <summary>
/// Control de slider personalizado con mejores características de accesibilidad
/// para ajustar la temperatura de color
/// </summary>
public class AccessibleTemperatureSlider : Slider
{
    static AccessibleTemperatureSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AccessibleTemperatureSlider), 
            new FrameworkPropertyMetadata(typeof(AccessibleTemperatureSlider)));
    }

    public AccessibleTemperatureSlider()
    {
        // Configurar propiedades de accesibilidad
        SetValue(AutomationProperties.ItemTypeProperty, "Control de temperatura de color");
        SetValue(AutomationProperties.HelpTextProperty, 
            "Usa las flechas izquierda y derecha para ajustar la temperatura. " +
            "Shift + flechas para ajustes finos. Ctrl + flechas para ajustes grandes.");
        
        // Manejar eventos de teclado para mejor navegación
        KeyDown += OnKeyDown;
        ValueChanged += OnValueChanged;
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new ChronoGuardSliderAutomationPeer(this);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var currentValue = Value;
        var smallChange = SmallChange;
        var largeChange = LargeChange;

        // Ajustes más precisos con modificadores
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            // Ajuste fino (10K)
            smallChange = 10;
            largeChange = 50;
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Ajuste grande (500K)
            smallChange = 500;
            largeChange = 1000;
        }

        switch (e.Key)
        {
            case Key.Left:
            case Key.Down:
                Value = Math.Max(Minimum, currentValue - smallChange);
                e.Handled = true;
                break;
            case Key.Right:
            case Key.Up:
                Value = Math.Min(Maximum, currentValue + smallChange);
                e.Handled = true;
                break;
            case Key.PageDown:
                Value = Math.Max(Minimum, currentValue - largeChange);
                e.Handled = true;
                break;
            case Key.PageUp:
                Value = Math.Min(Maximum, currentValue + largeChange);
                e.Handled = true;
                break;
            case Key.Home:
                Value = Minimum;
                e.Handled = true;
                break;
            case Key.End:
                Value = Maximum;
                e.Handled = true;
                break;
        }

        if (e.Handled)
        {
            AnnounceValueChange();
        }
    }

    private void OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Actualizar descripción para lectores de pantalla
        var temperatureDescription = GetTemperatureDescription((int)e.NewValue);
        SetValue(AutomationProperties.ItemStatusProperty, temperatureDescription);
    }

    private void AnnounceValueChange()
    {
        // Anunciar el cambio para lectores de pantalla
        var temperatureDescription = GetTemperatureDescription((int)Value);
        
        if (AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
        {
            var peer = UIElementAutomationPeer.FromElement(this);
            peer?.RaisePropertyChangedEvent(
                RangeValuePatternIdentifiers.ValueProperty,
                null,
                $"{Value:F0}K - {temperatureDescription}");
        }
    }

    private static string GetTemperatureDescription(int temperature)
    {
        return temperature switch
        {
            < 2000 => "Muy cálido, como luz de vela",
            < 2700 => "Cálido, como bombilla incandescente",
            < 3000 => "Cálido suave",
            < 4000 => "Blanco neutro",
            < 5000 => "Blanco frío",
            < 6500 => "Luz del día",
            _ => "Muy frío, como cielo despejado"
        };
    }
}
