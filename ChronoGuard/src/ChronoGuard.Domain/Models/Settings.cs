namespace ChronoGuard.Domain.Models
{
    public class Settings
    {
        public int CurrentTemperature { get; set; } = 6500;
        public bool EnableAutoAdjustment { get; set; } = true;
        public int DayTemperature { get; set; } = 6500;
        public int NightTemperature { get; set; } = 3000;
        public string Location { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool StartWithWindows { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public int TransitionDurationMinutes { get; set; } = 30;
        public string ActiveProfileName { get; set; } = "Default";
    }
}
