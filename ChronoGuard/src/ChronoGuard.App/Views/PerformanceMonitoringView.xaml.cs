using System.Windows.Controls;
using ChronoGuard.App.ViewModels;

namespace ChronoGuard.App.Views;

/// <summary>
/// Interaction logic for PerformanceMonitoringView.xaml
/// </summary>
public partial class PerformanceMonitoringView : UserControl
{
    public PerformanceMonitoringView()
    {
        InitializeComponent();
    }

    public PerformanceMonitoringView(PerformanceMonitoringViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
