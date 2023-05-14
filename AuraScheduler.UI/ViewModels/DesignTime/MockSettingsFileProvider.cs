using AuraScheduler.UI.Infrastructure;
using AuraScheduler.Worker;

namespace AuraScheduler.UI.ViewModels.DesignTime
{
    public class MockSettingsFileProvider : ISettingsFileProvider
    {
        public string LightSettingsFilePath { get; } = "";

        public bool UpdateSettingsFile(LightOptions updatedSettings)
        {
            throw new NotImplementedException();
        }
    }
}
