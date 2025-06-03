namespace ChronoGuard.Domain.Interfaces
{
    public interface ISettingsService
    {
        Domain.Models.Settings GetSettings();
        void SaveSettings(Domain.Models.Settings settings);
    }
}
