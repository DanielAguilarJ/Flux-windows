using System.Windows;
using System.Windows.Controls;

namespace ChronoGuard.App.Views.Onboarding
{
    public partial class Step2View : UserControl
    {
        public Step2View(object viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }
    }
}
