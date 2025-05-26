using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace ChronoGuard.App.Accessibility
{
    public class ChronoGuardSliderAutomationPeer : SliderAutomationPeer
    {
        public ChronoGuardSliderAutomationPeer(Slider owner) : base(owner) { }
        protected override string GetNameCore()
        {
            var slider = (Slider)Owner;
            return $"Temperatura de color: {slider.Value} Kelvin. Rango de {slider.Minimum} a {slider.Maximum} Kelvin.";
        }
    }
}
