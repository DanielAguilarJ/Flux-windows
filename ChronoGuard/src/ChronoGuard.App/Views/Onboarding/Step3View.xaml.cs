using System.Windows.Controls;

namespace ChronoGuard.App.Views.Onboarding
{
    public partial class Step3View : global::System.Windows.Controls.UserControl
    {
        public Step3View(object viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }
    }
}
