using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChronoGuard.Domain.Interfaces;
using System.Collections.ObjectModel;
using System.Windows;
using ChronoGuard.App.Views.Onboarding;

namespace ChronoGuard.App.ViewModels
{
    public partial class OnboardingViewModel : ObservableObject
    {
        private readonly Window _window;
        private readonly IConfigurationService _configurationService;
        private readonly IProfileService _profileService;
        private int _step = 0;
        public ObservableCollection<object> Steps { get; } = new();
        [ObservableProperty] private object currentStepView;

        // Propiedades para cada paso del onboarding
        [ObservableProperty] private LocationMethod locationMethod = LocationMethod.Auto;
        [ObservableProperty] private string? manualCity;
        [ObservableProperty] private double? manualLatitude;
        [ObservableProperty] private double? manualLongitude;
        [ObservableProperty] private bool allowIpLocation = true;
        [ObservableProperty] private string? selectedProfileId;
        [ObservableProperty] private bool autoStart = true;
        [ObservableProperty] private NotificationLevel notificationLevel = NotificationLevel.Basic;
        [ObservableProperty] private string? resumenConfiguracion;

        public OnboardingViewModel(Window window)
        {
            _window = window;
            _configurationService = App.ServiceProvider?.GetService(typeof(IConfigurationService)) as IConfigurationService
                ?? throw new InvalidOperationException("No se pudo obtener IConfigurationService");
            _profileService = App.ServiceProvider?.GetService(typeof(IProfileService)) as IProfileService
                ?? throw new InvalidOperationException("No se pudo obtener IProfileService");
            Steps.Add(new Step1View(this));
            Steps.Add(new Step2View(this));
            Steps.Add(new Step3View(this));
            Steps.Add(new Step4View(this));
            CurrentStepView = Steps[0];
        }

        [RelayCommand]
        public void Next()
        {
            // Validación por paso
            if (_step == 1 && LocationMethod == LocationMethod.Manual)
            {
                if (string.IsNullOrWhiteSpace(ManualCity) && (!ManualLatitude.HasValue || !ManualLongitude.HasValue))
                {
                    System.Windows.MessageBox.Show("Debe ingresar una ciudad o coordenadas válidas.", "Onboarding", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            if (_step == 2 && string.IsNullOrWhiteSpace(SelectedProfileId))
            {
                System.Windows.MessageBox.Show("Debe seleccionar un perfil inicial.", "Onboarding", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_step < Steps.Count - 1)
            {
                _step++;
                if (_step == 3) // Paso resumen
                {
                    ResumenConfiguracion = $"Ubicación: {LocationMethod}{(LocationMethod == LocationMethod.Manual ? $" ({ManualCity ?? $"{ManualLatitude},{ManualLongitude}"})" : "")}\n" +
                        $"Perfil: {SelectedProfileId}\n" +
                        $"Autoinicio: {(AutoStart ? "Sí" : "No")}\n" +
                        $"Notificaciones: {NotificationLevel}";
                }
                CurrentStepView = Steps[_step];
            }
        }

        [RelayCommand]
        public void Back()
        {
            if (_step > 0)
            {
                _step--;
                CurrentStepView = Steps[_step];
            }
        }

        [RelayCommand]
        public async Task FinishAsync()
        {
            // Guardar configuración
            var config = await _configurationService.GetConfigurationAsync();
            config.Location.Method = LocationMethod;
            config.Location.AllowIpLocation = AllowIpLocation;
            if (LocationMethod == LocationMethod.Manual)
            {
                config.Location.ManualCity = ManualCity;
                config.Location.ManualLatitude = ManualLatitude;
                config.Location.ManualLongitude = ManualLongitude;
            }
            config.General.AutoStart = AutoStart;
            config.Notifications.Level = NotificationLevel;
            await _configurationService.SaveConfigurationAsync(config);
            // Activar perfil inicial
            if (!string.IsNullOrWhiteSpace(SelectedProfileId))
                await _profileService.SetActiveProfileAsync(SelectedProfileId);
            // TODO: Configurar autoinicio real (StartupManager)
            _window.DialogResult = true;
            _window.Close();
        }
    }
}
