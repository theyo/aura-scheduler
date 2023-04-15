using System.Collections.ObjectModel;
using System.ComponentModel;

using AuraScheduler.UI.Infrastructure;
using AuraScheduler.Worker;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuraScheduler.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public ObservableCollection<string>? LogEntries { get; }

        public SettingsViewModel SettingsViewModel { get; private set; }

        public MainWindow(IOptionsMonitor<LightOptions> optionsMonitor, ILoggerProvider logProvider)
        {
            SettingsViewModel = new SettingsViewModel(optionsMonitor);

            DataContext = this;

            if (logProvider is IObservableLoggerProvider observableProvider)
            {
                LogEntries = observableProvider.LogEntries;
            }

            InitializeComponent();
        }
    }

    public partial class MainWindowViewModelDesignTime
    {
        public ObservableCollection<string>? LogEntries { get; } = new(new() { "App Started!", "Something else", "Uh Oh! Something bad happened" });

        public SettingsViewModel SettingsViewModel { get; private set; } = new SettingsViewModelDesignTime();
    }
}
