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
    
    /// <summary>
    /// Handle title bar drag to move window
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double click to maximize/restore
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            // Single click to drag
            DragMove();
        }
    }
    
    /// <summary>
    /// Handle minimize button click
    /// </summary>
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }
    
    /// <summary>
    /// Handle maximize/restore button click
    /// </summary>
    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
    
    /// <summary>
    /// Handle close button click
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Use the existing minimize to tray behavior
        WindowState = WindowState.Minimized;
    }
}
