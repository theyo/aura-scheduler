using System.Drawing;

using AuraScheduler.Worker.Aura;

using Microsoft.Extensions.Options;

//using RGB.NET.Core;
//using RGB.NET.Devices.Asus;

namespace AuraScheduler.Worker
{
    public class AuraScheduleWorker : BackgroundService
    {
        private readonly ILogger<AuraScheduleWorker> _logger;
        private readonly IOptionsMonitor<LightOptions> _lightOptionsMonitor;

        private readonly AuraHelper _auraHelper;

        private double _countDown = 0;

        public AuraScheduleWorker(ILogger<AuraScheduleWorker> logger, IOptionsMonitor<LightOptions> optionsMonitor)
        {
            _logger = logger;
            _lightOptionsMonitor = optionsMonitor;

            _lightOptionsMonitor.OnChange(SettingsChanged);

            _auraHelper = new AuraHelper();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            CheckAndSetLights();

            _countDown = _lightOptionsMonitor.CurrentValue.SecondsUntilNextScheduledTime(TimeOnly.FromDateTime(DateTime.Now));

            await Task.Delay(1000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_lightOptionsMonitor.CurrentValue.ScheduleEnabled && _countDown-- <= 0)
                {
                    CheckAndSetLights();

                    _countDown = _lightOptionsMonitor.CurrentValue.SecondsUntilNextScheduledTime(TimeOnly.FromDateTime(DateTime.Now));
                }

                await Task.Delay(1000, stoppingToken);
            }

            // always release when the program ends
            _logger.LogInformation("Program shutting down, releasing control");

            _auraHelper.ReleaseControl();
        }

        private void CheckAndSetLights()
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);

            if (!_lightOptionsMonitor.CurrentValue.ShouldLightsBeOn(now))
            {
                _logger.LogInformation("Lights should be off, taking control");

                _auraHelper.TakeControl();

                _logger.LogInformation("Setting color to: {color}", Color.Black);

                _auraHelper.SetLightsToColor(Color.Black);
            }
            else
            {
                _logger.LogInformation("Lights should be on, releasing control");

                _auraHelper.ReleaseControl();
            }
        }

        private void SettingsChanged(LightOptions options)
        {
            _logger.LogInformation("Settings changed, updating lights");

            CheckAndSetLights();

            if (_lightOptionsMonitor.CurrentValue.ScheduleEnabled)
            {
                _countDown = _lightOptionsMonitor.CurrentValue.SecondsUntilNextScheduledTime(TimeOnly.FromDateTime(DateTime.Now));
                _logger.LogInformation("New Countdown: {countdown}", _countDown);
            }
        }
    }
}
