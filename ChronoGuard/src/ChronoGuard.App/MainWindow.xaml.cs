using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using ChronoGuard.App.ViewModels;

namespace ChronoGuard.App;

/// <summary>
/// Main application window
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Set DataContext from DI container
        if (App.ServiceProvider != null)
        {
            DataContext = App.ServiceProvider.GetRequiredService<MainWindowViewModel>();
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        
        // Hide window when minimized if minimize to tray is enabled
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            ShowInTaskbar = false;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Override close button to minimize to tray instead
        e.Cancel = true;
        WindowState = WindowState.Minimized;
    }

    /// <summary>
    /// Shows the window and brings it to front
    /// </summary>
    public void ShowAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        ShowInTaskbar = true;
        Activate();
        Focus();
    }
}
