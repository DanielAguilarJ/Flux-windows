using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Windows;
using ChronoGuard.App.ViewModels;

namespace ChronoGuard.App;

/// <summary>
/// Interaction logic for SettingsWindow.xaml
/// </summary>
public partial class SettingsWindow : Window
{
    private SettingsViewModel? _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        
        // Set DataContext from DI container
        if (App.ServiceProvider != null)
        {
            _viewModel = App.ServiceProvider.GetRequiredService<SettingsViewModel>();
            DataContext = _viewModel;
        }
    }

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize the view model when window loads
        if (_viewModel != null)
        {
            await _viewModel.InitializeAsync();
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // Check for unsaved changes
        if (_viewModel?.HasUnsavedChanges == true)
        {
            var result = MessageBox.Show(
                "Tienes cambios sin guardar. ¿Deseas guardar antes de cerrar?",
                "Cambios no guardados",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    // Try to save
                    try
                    {
                        _ = _viewModel.SaveSettingsCommand.ExecuteAsync(null);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al guardar: {ex.Message}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        e.Cancel = true;
                    }
                    break;
                case MessageBoxResult.No:
                    // Don't save, just close
                    break;
                case MessageBoxResult.Cancel:
                    // Cancel closing
                    e.Cancel = true;
                    break;
            }
        }
    }
}
