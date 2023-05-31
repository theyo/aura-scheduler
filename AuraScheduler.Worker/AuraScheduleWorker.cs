using System.Drawing;
using System.Threading.Tasks;

using AuraScheduler.Worker.Aura;

using Microsoft.Extensions.Options;

namespace AuraScheduler.Worker
{
    public class AuraScheduleWorker : BackgroundService
    {
        private readonly ILogger<AuraScheduleWorker> _logger;
        private readonly IOptionsMonitor<LightOptions> _lightOptionsMonitor;

        private readonly AuraHelper _auraHelper;

        private readonly IDisposable? _settingsChangeEvent;

        private readonly TimeSpan _defaultTimeSpan = TimeSpan.FromSeconds(1);

        private readonly CancellationTokenSource _timerCTS = new();

        private readonly PeriodicTimer _timer;

        public AuraScheduleWorker(ILogger<AuraScheduleWorker> logger, IOptionsMonitor<LightOptions> optionsMonitor)
        {
            _logger = logger;
            _lightOptionsMonitor = optionsMonitor;
            _timer = new PeriodicTimer(_defaultTimeSpan);

            _settingsChangeEvent = _lightOptionsMonitor.OnChange(UpdateLightsAndSetTimer);

            _auraHelper = new AuraHelper();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker starting...");

            try
            {
                CheckAndSetLights(_lightOptionsMonitor.CurrentValue);

                _logger.LogInformation("Worker running!");

                while (!stoppingToken.IsCancellationRequested && await _timer.WaitForNextTickAsync(_timerCTS.Token))
                {
                    if (_lightOptionsMonitor.CurrentValue.ScheduleEnabled)
                    {
                        CheckAndSetLights(_lightOptionsMonitor.CurrentValue);
                    }
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
                _logger.LogInformation("Worker shutting down, releasing control");

                _auraHelper.ReleaseControl();

                _settingsChangeEvent?.Dispose();
            }
        }

        private void CheckAndSetLights(LightOptions options)
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);

            var lightsShouldBeOn = options.ShouldLightsBeOn(now);

            _logger.LogDebug("Lights should be {lightStatus}...", lightsShouldBeOn ? "ON" : "OFF");

            if (lightsShouldBeOn)
            {
                if (_auraHelper.HasControl)
                {
                    _logger.LogInformation("Lights should be on, releasing control");

                    _auraHelper.ReleaseControl();

                    _logger.LogDebug("Control released");
                }
                else
                {
                    _logger.LogDebug("Lights are already on, nothing to do");
                }
            }
            else
            {
                if (!_auraHelper.HasControl)
                {
                    _logger.LogInformation("Lights should be off, taking control");

                    _auraHelper.TakeControl();

                    _logger.LogDebug("Setting color to: {color}", Color.Black);

                    _auraHelper.SetLightsToColor(Color.Black);

                    _logger.LogDebug("Color set to: {color}", Color.Black);
                }
                else
                {
                    _logger.LogDebug("Lights are already off, nothing to do");
                }
            }
        }

        private void UpdateLightsAndSetTimer(LightOptions options)
        {
            _logger.LogInformation("Settings changed, updating lights");

            CheckAndSetLights(options);
        }
    }
}
