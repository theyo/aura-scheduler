using System.IO;
using System.Reflection;
using System.Windows;


using ControlzEx.Theming;
using Hardcodet.Wpf.TaskbarNotification;

namespace AuraScheduler.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private TaskbarIcon? taskbarIcon;
        private NotifyIconViewModel? notifyIconVM;

        public App()
        {
            InitializeComponent();
        }

        public void SetTaskBarIconViewModel(NotifyIconViewModel notifyIconViewModel)
        {
            notifyIconVM = notifyIconViewModel;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            ThemeManager.Current.ThemeSyncMode = ThemeSyncMode.SyncWithAppMode;
            ThemeManager.Current.SyncTheme();

            base.OnStartup(e);

            //initialize NotifyIcon
            taskbarIcon = (TaskbarIcon)FindResource("MyNotifyIcon");

            taskbarIcon.DataContext = notifyIconVM;

        }

        protected override void OnExit(ExitEventArgs e)
        {
            taskbarIcon?.Dispose();

            base.OnExit(e);
        }
    }
}
