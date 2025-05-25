using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using ChronoGuard.App.ViewModels;

namespace ChronoGuard.App;

/// <summary>
/// Interaction logic for SettingsWindow.xaml
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        
        // Set DataContext from DI container
        if (App.ServiceProvider != null)
        {
            DataContext = App.ServiceProvider.GetRequiredService<SettingsViewModel>();
        }
    }

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Focus the first tab when window loads
        if (DataContext is SettingsViewModel viewModel)
        {
            // Optionally load fresh data when window opens
            // This ensures settings are up-to-date
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Optionally prompt to save unsaved changes
        // For now, just allow closing
    }
}
