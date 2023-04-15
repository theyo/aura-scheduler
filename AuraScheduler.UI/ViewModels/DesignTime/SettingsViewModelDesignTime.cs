using AuraScheduler.Worker;

namespace AuraScheduler.UI.ViewModels.DesignTime
{
    public partial class SettingsViewModelDesignTime : SettingsViewModel
    {

        public static LightOptions DefaultOptions = new()
        {
            LightMode = LightMode.Schedule,
            Schedule = new LightOptions.LEDSchedule()
            {
                LightsOn = new TimeOnly(7, 30, 0),
                LightsOff = new TimeOnly(21, 30, 0)
            }
        };

        public SettingsViewModelDesignTime() :
            base(new MockOptionsMonitor<LightOptions>(DefaultOptions), new MockSettingsFileProvider())
        { }
    }
}
