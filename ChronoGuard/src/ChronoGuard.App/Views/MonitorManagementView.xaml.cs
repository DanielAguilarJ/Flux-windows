using System.Windows.Controls;

namespace ChronoGuard.App.Views
{
    /// <summary>
    /// Interaction logic for MonitorManagementView.xaml
    /// Advanced monitor management and configuration view
    /// </summary>
    public partial class MonitorManagementView : UserControl
    {
        public MonitorManagementView()
        {
            InitializeComponent();
        }

        public MonitorManagementView(object viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
