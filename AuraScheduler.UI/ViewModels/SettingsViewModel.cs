using System.ComponentModel;
using Microsoft.Extensions.Options;

using AuraScheduler.Worker;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuraScheduler.UI.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AuraScheduler.UI
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IOptionsMonitor<LightOptions> _optionsMonitor;
        private readonly ISettingsFileProvider _settingsFileProvider;
        private readonly ILogger<SettingsViewModel> _logger;

        private bool _skipMarkDirty = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveChangesCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelChangesCommand))]
        bool isDirty = false;


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ScheduleEnabled))]
        private LightMode mode;

        [ObservableProperty]
        private TimeOnly scheduleLightsOn;

        [ObservableProperty]
        private TimeOnly scheduleLightsOff;


        public bool ScheduleEnabled { get => Mode == LightMode.Schedule; }

        public IEnumerable<LightMode> LightModes { get; private set; }

        public SettingsViewModel(IOptionsMonitor<LightOptions> optionsMonitor, ISettingsFileProvider settingsFileProvider, ILogger<SettingsViewModel> logger)
        {
            ArgumentNullException.ThrowIfNull(nameof(optionsMonitor));
            ArgumentNullException.ThrowIfNull(nameof(settingsFileProvider));

            _optionsMonitor = optionsMonitor;
            _settingsFileProvider = settingsFileProvider;
            _logger = logger;

            UpdateOptions(_optionsMonitor.CurrentValue);

            LightModes = Enum.GetValues(typeof(LightMode)).Cast<LightMode>();

            _optionsMonitor.OnChange(UpdateOptions);

            PropertyChanged += OptionsChanged;
        }

        public bool CanSaveOrCancel()
        {
            return IsDirty;
        }

        [RelayCommand(CanExecute = nameof(CanSaveOrCancel))]
        public void SaveChanges()
        {
            _logger.LogInformation("Saving changes...");
            var options = _optionsMonitor.CurrentValue;

            options.LightMode = Mode;
            options.Schedule.LightsOn = ScheduleLightsOn;
            options.Schedule.LightsOff = ScheduleLightsOff;

            _settingsFileProvider.UpdateSettingsFile(options);

            IsDirty = false;
            _logger.LogInformation("Changes saved!");
        }

        [RelayCommand(CanExecute = nameof(CanSaveOrCancel))]
        public void CancelChanges()
        {
            IsDirty = false;
            UpdateOptions(_optionsMonitor.CurrentValue);
        }

        private void UpdateOptions(LightOptions updatedOptions)
        {
            _skipMarkDirty = true;

            Mode = updatedOptions.LightMode;
            ScheduleLightsOn = updatedOptions.Schedule.LightsOn;
            ScheduleLightsOff = updatedOptions.Schedule.LightsOff;

            _skipMarkDirty = false;
        }

        private void OptionsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_skipMarkDirty || e.PropertyName == nameof(IsDirty))
                return;

            IsDirty = true;
        }
    }
}
