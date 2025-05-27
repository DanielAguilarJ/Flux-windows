using System.Windows;
using System.Windows.Controls;

namespace ChronoGuard.App.Views.Onboarding
{
    public partial class Step4View : UserControl
    {
        public Step4View()
        {
            InitializeComponent();
        }
        
        public Step4View(object viewModel) : this()
        {
            this.DataContext = viewModel;
        }
    }
}
