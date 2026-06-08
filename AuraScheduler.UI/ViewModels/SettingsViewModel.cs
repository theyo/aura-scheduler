using System.ComponentModel;

using AuraScheduler.UI.Infrastructure;
using AuraScheduler.Worker;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        public partial bool IsDirty { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ScheduleEnabled))]
        public partial LightMode Mode { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ScheduleLightsOnTimeSpan))]
        public partial TimeOnly ScheduleLightsOn { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ScheduleLightsOffTimeSpan))]
        public partial TimeOnly ScheduleLightsOff { get; set; }

        [ObservableProperty]
        public partial bool CloseToTray { get; set; } = true;

        [ObservableProperty]
        public partial bool StartMinimized { get; set; }

        // WinUI 3 TimePicker uses TimeSpan?, not TimeOnly
        public TimeSpan? ScheduleLightsOnTimeSpan
        {
            get => ScheduleLightsOn.ToTimeSpan();
            set { if (value.HasValue) ScheduleLightsOn = TimeOnly.FromTimeSpan(value.Value); }
        }

        public TimeSpan? ScheduleLightsOffTimeSpan
        {
            get => ScheduleLightsOff.ToTimeSpan();
            set { if (value.HasValue) ScheduleLightsOff = TimeOnly.FromTimeSpan(value.Value); }
        }

        public bool ScheduleEnabled => Mode == LightMode.Schedule;

        public IEnumerable<LightMode> LightModes { get; private set; }

        public SettingsViewModel(IOptionsMonitor<LightOptions> optionsMonitor, ISettingsFileProvider settingsFileProvider, ILogger<SettingsViewModel> logger)
        {
            ArgumentNullException.ThrowIfNull(optionsMonitor);
            ArgumentNullException.ThrowIfNull(settingsFileProvider);

            _optionsMonitor = optionsMonitor;
            _settingsFileProvider = settingsFileProvider;
            _logger = logger;

            UpdateOptions(_optionsMonitor.CurrentValue);
            LightModes = Enum.GetValues(typeof(LightMode)).Cast<LightMode>();
            _optionsMonitor.OnChange(UpdateOptions);

            PropertyChanged += OptionsChanged;
        }

        public bool CanSaveOrCancel() => IsDirty;

        [RelayCommand(CanExecute = nameof(CanSaveOrCancel))]
        public void SaveChanges()
        {
            _logger.LogInformation("Saving changes...");
            var options = _optionsMonitor.CurrentValue;

            options.LightMode = Mode;
            options.Schedule.LightsOn = ScheduleLightsOn;
            options.Schedule.LightsOff = ScheduleLightsOff;
            options.CloseToTray = CloseToTray;
            options.StartMinimized = StartMinimized;

            if (_settingsFileProvider.UpdateSettingsFile(options))
            {
                _logger.LogInformation("Changes saved!");
                IsDirty = false;
            }
            else
            {
                CancelChanges();
            }
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
            CloseToTray = updatedOptions.CloseToTray;
            StartMinimized = updatedOptions.StartMinimized;
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
