using System.Windows;

namespace ChronoGuard.App
{
    public partial class OnboardingWindow : Window
    {
        public OnboardingWindow()
        {
            InitializeComponent();
            DataContext = new ViewModels.OnboardingViewModel(this);
        }
    }
}
