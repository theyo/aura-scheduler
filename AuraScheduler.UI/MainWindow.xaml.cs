using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

using AuraScheduler.UI.Infrastructure;
using AuraScheduler.UI.ViewModels.DesignTime;
using AuraScheduler.Worker;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuraScheduler.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly Lazy<NotifyIconViewModel> notifyIconVM;
        private NotifyIconViewModel NotifyIconVM
        {
            get { return notifyIconVM.Value; }
        }

        public ObservableCollection<string>? LogEntries { get; }

        public SettingsViewModel SettingsViewModel { get; private set; }

        public MainWindow(SettingsViewModel viewModel, ILoggerProvider logProvider, IServiceProvider serviceProvider)
        {
            notifyIconVM = new Lazy<NotifyIconViewModel>(() => serviceProvider.GetRequiredService<NotifyIconViewModel>());

            SettingsViewModel = viewModel;

            DataContext = this;

            if (logProvider is IObservableLoggerProvider observableProvider)
            {
                LogEntries = observableProvider.LogEntries;
            }

            this.Closing += MainWindow_Closing;
            this.StateChanged += MainWindow_StateChanged;

            InitializeComponent();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            switch (WindowState)
            {
                case System.Windows.WindowState.Minimized:
                    NotifyIconVM.HideWindow();
                    break;
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            NotifyIconVM.HideWindow();
        }
    }
}
