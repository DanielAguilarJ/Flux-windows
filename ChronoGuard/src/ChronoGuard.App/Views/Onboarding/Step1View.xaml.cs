using System.Windows;
using System.Windows.Controls;

namespace ChronoGuard.App.Views.Onboarding
{
    public partial class Step1View : UserControl
    {
        public Step1View()
        {
            InitializeComponent();
        }
        
        public Step1View(object viewModel) : this()
        {
            this.DataContext = viewModel;
        }
    }
}
