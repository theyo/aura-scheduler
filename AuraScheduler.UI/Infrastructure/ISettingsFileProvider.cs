using AuraScheduler.Worker;

namespace AuraScheduler.UI.Infrastructure
{
    public interface ISettingsFileProvider
    {
        bool UpdateSettingsFile(LightOptions updatedSettings);

        string LightSettingsFilePath { get; }
    }
}
