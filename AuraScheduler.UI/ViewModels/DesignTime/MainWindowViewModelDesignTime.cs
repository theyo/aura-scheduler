using System.Collections.ObjectModel;

namespace AuraScheduler.UI.ViewModels.DesignTime
{
    public partial class MainWindowViewModelDesignTime
    {
        public ObservableCollection<string>? LogEntries { get; } = new(new() { "App Started!", "Something else", "Uh Oh! Something bad happened" });

        public SettingsViewModel SettingsViewModel { get; private set; } = new SettingsViewModelDesignTime();
    }
}
