using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

using AuraScheduler.UI.Infrastructure;
using AuraScheduler.UI.ViewModels.DesignTime;
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

        public MainWindow(SettingsViewModel viewModel, ILoggerProvider logProvider)
        {
            SettingsViewModel = viewModel;

            DataContext = this;

            if (logProvider is IObservableLoggerProvider observableProvider)
            {
                LogEntries = observableProvider.LogEntries;
            }

            InitializeComponent();
        }
    }
}
