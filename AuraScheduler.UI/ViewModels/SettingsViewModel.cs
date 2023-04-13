using System.ComponentModel;

using AuraScheduler.Worker;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Options;

namespace AuraScheduler.UI
{
    public class MockOptionsMonitor<T> : IOptionsMonitor<T> where T : new()
    {
        private Action<T, string>? _listener;

        public T CurrentValue { get; } = new T();

        public T Get(string? name)
        {
            return CurrentValue;
        }

        public MockOptionsMonitor(T instance)
        {
            CurrentValue = instance;
        }

        public IDisposable? OnChange(Action<T, string?> listener)
        {
            _listener = listener;
            return null;
        }
    }

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
            base(new MockOptionsMonitor<LightOptions>(DefaultOptions))
        { }
    }

    public partial class SettingsViewModel : ObservableObject
    {
        readonly IOptionsMonitor<LightOptions> _optionsMonitor;

        private bool _skipMarkDirty = false;

        [ObservableProperty]
        bool isDirty = false;


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ScheduleEnabled))]
        private LightMode lightMode;

        [ObservableProperty]
        private TimeOnly scheduleLightsOn;

        [ObservableProperty]
        private TimeOnly scheduleLightsOff;

        public bool ScheduleEnabled { get => LightMode == LightMode.Schedule; }

        public IEnumerable<LightMode> LightModes { get; private set; }

        public SettingsViewModel(IOptionsMonitor<LightOptions> optionsMonitor)
        {
            ArgumentNullException.ThrowIfNull(nameof(optionsMonitor));

            _optionsMonitor = optionsMonitor;

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
            //IsDirty = false;
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

            LightMode = updatedOptions.LightMode;
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
