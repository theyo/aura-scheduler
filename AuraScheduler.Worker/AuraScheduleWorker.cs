using System.Drawing;

using AuraScheduler.Worker.Aura;

using Microsoft.Extensions.Options;

namespace AuraScheduler.Worker
{
    public class AuraScheduleWorker : BackgroundService
    {
        private readonly ILogger<AuraScheduleWorker> _logger;
        private readonly IOptionsMonitor<LightOptions> _lightOptionsMonitor;

        private readonly AuraHelper _auraHelper;

        private double _countDownReset = 0;
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

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_countDownReset-- <= 0)
                    {
                        UpdateCountdown();
                    }

                    if (_lightOptionsMonitor.CurrentValue.ScheduleEnabled && _countDown-- <= 0)
                    {
                        CheckAndSetLights();

                        _countDown = _lightOptionsMonitor.CurrentValue.SecondsUntilNextScheduledTime(TimeOnly.FromDateTime(DateTime.Now));
                    }

                    await Task.Delay(1000, stoppingToken);
                }

            }
            catch (TaskCanceledException)
            {
                // When the stopping token is canceled, for example, a call made from services.msc,
                // we shouldn't exit with a non-zero exit code. In other words, this is expected...
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{message}", ex.Message);

                // Terminates this process and returns an exit code to the operating system.
                // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
                // performs one of two scenarios:
                // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
                // 2. When set to "StopHost": will cleanly stop the host, and log errors.
                //
                // In order for the Windows Service Management system to leverage configured
                // recovery options, we need to terminate the process with a non-zero exit code.

                Environment.Exit(1);
            }
            finally
            {
                // always release when the program ends
                _logger.LogInformation("Program shutting down, releasing control");

                _auraHelper.ReleaseControl();
            }


        }

        private void CheckAndSetLights()
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);

            if (!_lightOptionsMonitor.CurrentValue.ShouldLightsBeOn(now))
            {
                if (!_auraHelper.HasControl)
                {
                    _logger.LogInformation("Lights should be off, taking control");

                    _auraHelper.TakeControl();
                }

                _logger.LogDebug("Setting color to: {color}", Color.Black);

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

            if (_lightOptionsMonitor.CurrentValue.ScheduleEnabled)
            {
                UpdateCountdown();
                _logger.LogDebug("New Countdown: {countdown}", _countDown);
            }

            CheckAndSetLights();
        }

        private void UpdateCountdown()
        {
            _countDown = _lightOptionsMonitor.CurrentValue.SecondsUntilNextScheduledTime(TimeOnly.FromDateTime(DateTime.Now));
            _countDownReset = 5;
        }
    }
}
