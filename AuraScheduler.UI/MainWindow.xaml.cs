using System.ComponentModel;
using AuraScheduler.Worker;

using Microsoft.Extensions.Options;

namespace AuraScheduler.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public SettingsViewModel SettingsViewModel { get; private set; }

        public MainWindow(IOptionsMonitor<LightOptions> optionsMonitor)
        {
            SettingsViewModel = new SettingsViewModel(optionsMonitor);

            DataContext = SettingsViewModel;

            InitializeComponent();
        }
    }
}
