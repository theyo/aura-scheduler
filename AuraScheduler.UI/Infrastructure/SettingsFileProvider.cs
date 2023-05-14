using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using AuraScheduler.Worker;
using System.Text.Json.Serialization;

namespace AuraScheduler.UI.Infrastructure
{
    public class SettingsFileProvider : ISettingsFileProvider
    {
        private record LightOptionsSettingsWrapper(LightOptions LightOptions);

        private object _lock = new object();
        private readonly ILogger<SettingsFileProvider> _logger;

        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public string LightSettingsFilePath { get; }

        public SettingsFileProvider(ILogger<SettingsFileProvider> logger, string lightSettingsFilePath)
        {
            _logger = logger;
            LightSettingsFilePath = lightSettingsFilePath;

            _jsonSerializerOptions = new JsonSerializerOptions() { WriteIndented = true, IgnoreReadOnlyFields = true, IgnoreReadOnlyProperties = true };
            _jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

        }

        public bool UpdateSettingsFile(LightOptions updatedSettings)
        {
            lock (_lock)
            {
                try
                {
                    _logger.LogDebug("Writing updated settings to file...");

                    var wrappedOptions = new LightOptionsSettingsWrapper(updatedSettings);

                    File.WriteAllText(LightSettingsFilePath, JsonSerializer.Serialize(wrappedOptions, wrappedOptions.GetType(), _jsonSerializerOptions));

                    _logger.LogDebug("Finished writing updated settings to file!");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred trying to write to the settings file");
                    return false;
                }
            }

            return true;
        }
    }
}
