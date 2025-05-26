using System.Windows;
using System.Windows.Controls;

namespace ChronoGuard.App.Views.Onboarding
{
    public partial class Step4View : UserControl
    {
        public Step4View(object viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }
    }
}
