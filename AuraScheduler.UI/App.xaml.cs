using AuraScheduler.UI.Infrastructure;
using AuraScheduler.Worker;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AuraScheduler.UI
{
    public partial class App : Application
    {
        private readonly IHost _host;
        private TrayIcon? _trayIcon;

        public App(IHost host)
        {
            _host = host;
            InitializeComponent();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            var notifyIconVM = _host.Services.GetRequiredService<NotifyIconViewModel>();
            notifyIconVM.SetWindow(mainWindow);

            var logProvider = _host.Services.GetServices<Microsoft.Extensions.Logging.ILoggerProvider>()
                .OfType<ObservableLoggerProvider>()
                .FirstOrDefault();
            logProvider?.SetDispatcherQueue(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());

            _trayIcon = CreateTrayIcon(notifyIconVM);

            StartActivationListener(notifyIconVM);

            var options = _host.Services.GetRequiredService<IOptionsMonitor<LightOptions>>().CurrentValue;
            if (options.StartMinimized)
                notifyIconVM.IsWindowVisible = false; // stay hidden; correct the state set by SetWindow
            else
                mainWindow.Activate();

            // Start the host on a ThreadPool (MTA) thread instead of the WinUI3 ASTA UI thread.
            await Task.Run(async () =>
            {
                try
                {
                    await _host.StartAsync();
                }
                catch (Exception ex)
                {
                    var logger = _host.Services.GetRequiredService<ILoggerFactory>().CreateLogger<App>();
                    logger.LogCritical(ex, "Host failed to start: {message}", ex.Message);
                }
            });

            var auraStatus = _host.Services.GetRequiredService<AuraInitializationStatus>();
            try
            {
                var auraInitError = await auraStatus.Awaitable.WaitAsync(TimeSpan.FromSeconds(10));
                if (auraInitError is not null)
                    await ShowAuraErrorDialogAsync(mainWindow, auraInitError);
            }
            catch (TimeoutException)
            {
                // Worker took longer than 10 s to signal — unusual; don't block the UI.
            }
        }

        private static async Task ShowAuraErrorDialogAsync(MainWindow mainWindow, Exception _)
        {
            mainWindow.Activate();

            var xamlRoot = mainWindow.Content?.XamlRoot;
            if (xamlRoot is null)
                return; // no XAML tree yet — dialog can't be shown

            var dialog = new ContentDialog
            {
                Title = "AURA SDK not available",
                Content =
                    "Could not connect to the AURA service.\n\n" +
                    "Make sure ARMOURY CRATE (or ASUS AURA) is installed.\n\n" +
                    "The app will continue running, but light scheduling is unavailable. " +
                    "See the Activity Log for details.",
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = xamlRoot,
            };

            await dialog.ShowAsync();
        }

        private void StartActivationListener(NotifyIconViewModel notifyIconVM)
        {
            var activateEvent = _host.Services.GetRequiredService<EventWaitHandle>();
            var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            // A second launch of the app signals this event instead of starting its own
            // instance; bring the existing window to the front when that happens.
            Task.Run(() =>
            {
                while (true)
                {
                    activateEvent.WaitOne();
                    dispatcherQueue.TryEnqueue(() => notifyIconVM.ShowWindowCommand.Execute(null));
                }
            });
        }

        private static TrayIcon CreateTrayIcon(NotifyIconViewModel vm)
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
            var icon = new TrayIcon("AURA Scheduler", iconPath);

            icon.AddMenuItem("Show Window", () => vm.ShowWindowCommand.Execute(null));
            icon.AddMenuItem("Hide Window", () => vm.HideWindowCommand.Execute(null));
            icon.AddSeparator();
            icon.AddMenuItem("Exit", () => vm.ExitApplicationCommand.Execute(null));

            icon.DoubleClicked += () => vm.ShowWindowCommand.Execute(null);

            return icon;
        }

        public async Task StopAsync()
        {
            _trayIcon?.Dispose();
            await _host.StopAsync();
        }
    }
}
