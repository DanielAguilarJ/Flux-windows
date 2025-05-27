using System.Windows.Controls;

namespace ChronoGuard.App.Views.Onboarding
{
    public partial class Step3View : UserControl
    {
        public Step3View()
        {
            InitializeComponent();
        }
        
        public Step3View(object viewModel) : this()
        {
            this.DataContext = viewModel;
        }
    }
}
