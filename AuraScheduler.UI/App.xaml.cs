using AuraScheduler.UI.Infrastructure;
using AuraScheduler.Worker;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AuraScheduler.UI
{
    public partial class App : Application
    {
        private readonly IHost _host;
        private readonly SemaphoreSlim _updateDialogLock = new(1, 1);
        private DispatcherQueue? _dispatcherQueue;
        private UpdateCheckWorker? _updateCheckWorker;
        private MainWindow? _mainWindow;
        private TrayIcon? _trayIcon;
        private ReleaseInfo? _availableRelease;
        private string? _notifiedUpdateVersion;
        private bool _appNotificationsRegistered;
        private bool _showUpdateWhenAvailable;
        private int _stopping;

        public App(IHost host)
        {
            _host = host;
            InitializeComponent();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            _mainWindow = mainWindow;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _updateCheckWorker = _host.Services.GetRequiredService<UpdateCheckWorker>();
            _updateCheckWorker.StateChanged += OnUpdateCheckStateChanged;

            var notifyIconVM = _host.Services.GetRequiredService<NotifyIconViewModel>();
            notifyIconVM.SetWindow(mainWindow);

            var logProvider = _host.Services.GetServices<Microsoft.Extensions.Logging.ILoggerProvider>()
                .OfType<ObservableLoggerProvider>()
                .FirstOrDefault();
            logProvider?.SetDispatcherQueue(_dispatcherQueue);

            _trayIcon = CreateTrayIcon(notifyIconVM);
            _trayIcon.AddMenuItem("View Available Update", () =>
            {
                if (_availableRelease is not null)
                    _ = ShowUpdateDialogAsync(mainWindow, _updateCheckWorker!, _availableRelease);
            });

            _trayIcon.AddMenuItem("Check for Updates", () => _ = _updateCheckWorker?.CheckNowAsync());
            mainWindow.UpdateRequested += (_, _) =>
            {
                if (_availableRelease is not null)
                    _ = ShowUpdateDialogAsync(mainWindow, _updateCheckWorker!, _availableRelease);
            };
            RegisterAppNotifications(mainWindow, notifyIconVM);

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

        private void OnUpdateCheckStateChanged(UpdateCheckState state)
        {
            var dispatcherQueue = _dispatcherQueue;
            if (dispatcherQueue is null || Volatile.Read(ref _stopping) != 0)
                return;

            dispatcherQueue.TryEnqueue(() => _ = HandleUpdateCheckStateAsync(state));
        }

        private async Task HandleUpdateCheckStateAsync(UpdateCheckState state)
        {
            try
            {
                var mainWindow = _mainWindow;
                if (mainWindow is null)
                    return;

                switch (state.Status)
                {
                    case UpdateCheckStatus.NoUpdateAvailable:
                        ClearAvailableUpdate(mainWindow);
                        if (state.Kind == UpdateCheckKind.Manual)
                        {
                            await ShowMessageAsync(
                                mainWindow,
                                "No Updates Available",
                                "You are running the latest version.",
                                "OK");
                        }
                        break;

                    case UpdateCheckStatus.UpdateAvailable when state.Release is not null:
                        await HandleUpdateAvailableAsync(mainWindow, state.Release, state.Kind);
                        break;

                    case UpdateCheckStatus.Downloading when state.Release is not null:
                        mainWindow.SetUpdateDownloading(state.Release.Version);
                        _trayIcon?.SetTooltip($"AURA Scheduler — Downloading update v{state.Release.Version}");
                        break;

                    case UpdateCheckStatus.Installing when state.Release is not null:
                        mainWindow.SetUpdateInstalling(state.Release.Version);
                        _trayIcon?.SetTooltip($"AURA Scheduler — Installing update v{state.Release.Version}");
                        break;

                    case UpdateCheckStatus.Failed when state.Release is not null:
                        mainWindow.SetUpdateAvailable(state.Release.Version);
                        _trayIcon?.SetTooltip($"AURA Scheduler — Update available: v{state.Release.Version}");
                        await ShowMessageAsync(
                            mainWindow,
                            "Update Installation Failed",
                            state.Error?.Message ?? "The update installer could not be started.",
                            "OK");
                        break;

                    case UpdateCheckStatus.Failed when state.Kind == UpdateCheckKind.Manual:
                        await ShowMessageAsync(
                            mainWindow,
                            "Update Check Failed",
                            "Could not check GitHub for updates.",
                            "OK");
                        break;
                }
            }
            catch (Exception ex)
            {
                _host.Services.GetRequiredService<ILogger<App>>()
                    .LogWarning(ex, "Update-check state handling failed for {Status}.", state.Status);
            }
        }

        private async Task HandleUpdateAvailableAsync(MainWindow mainWindow, ReleaseInfo release, UpdateCheckKind kind)
        {
            _availableRelease = release;
            mainWindow.SetUpdateAvailable(release.Version);
            _trayIcon?.SetTooltip($"AURA Scheduler — Update available: v{release.Version}");
            _trayIcon?.SetUpdateAvailable(true);

            var notificationsEnabled = _host.Services
                .GetRequiredService<IOptionsMonitor<LightOptions>>().CurrentValue.CheckForUpdates;
            var showReleaseDialog = kind == UpdateCheckKind.Manual;

            if (_showUpdateWhenAvailable)
            {
                _showUpdateWhenAvailable = false;
                _notifiedUpdateVersion = release.Version;
                showReleaseDialog = true;
            }
            else if (notificationsEnabled)
            {
                await ShowUpdateNotificationAsync(release);
            }

            if (showReleaseDialog)
                await ShowUpdateDialogAsync(mainWindow, _updateCheckWorker!, release);
        }

        private void ClearAvailableUpdate(MainWindow mainWindow)
        {
            _availableRelease = null;
            mainWindow.ClearUpdateAvailable();
            _trayIcon?.SetTooltip("AURA Scheduler");
            _trayIcon?.SetUpdateAvailable(false);
        }

        private void RegisterAppNotifications(MainWindow mainWindow, NotifyIconViewModel notifyIconVM)
        {
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            try
            {
                if (!AppNotificationManager.IsSupported())
                {
                    logger.LogInformation("Windows app notifications are not supported on this system.");
                    return;
                }

                var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                AppNotificationManager.Default.NotificationInvoked += (_, _) =>
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        notifyIconVM.ShowWindow();
                        if (_availableRelease is not null)
                            _ = ShowUpdateDialogAsync(mainWindow, _updateCheckWorker!, _availableRelease);
                        else
                            _showUpdateWhenAvailable = true;
                    });
                AppNotificationManager.Default.Register();
                _appNotificationsRegistered = true;
                logger.LogInformation(
                    "Windows app notifications registered. Notification setting: {NotificationSetting}.",
                    AppNotificationManager.Default.Setting);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Windows app notifications could not be registered.");
            }
        }

        private async Task ShowUpdateNotificationAsync(ReleaseInfo release)
        {
            if (!_appNotificationsRegistered || _notifiedUpdateVersion == release.Version)
                return;

            try
            {
                var setting = AppNotificationManager.Default.Setting;
                if (setting != AppNotificationSetting.Enabled)
                {
                    _host.Services.GetRequiredService<ILogger<App>>()
                        .LogWarning(
                            "Windows update notification was not shown because the notification setting is {NotificationSetting}.",
                            setting);
                    return;
                }

                var notification = new AppNotificationBuilder()
                    .AddArgument("action", "viewUpdate")
                    .AddText($"AURA Scheduler v{release.Version} is available")
                    .AddText("Review the release notes and choose when to install the update.")
                    .BuildNotification();

                AppNotificationManager.Default.Show(notification);
                var delivered = (await AppNotificationManager.Default.GetAllAsync())
                    .Any(item => item.Id == notification.Id);
                if (delivered)
                {
                    _notifiedUpdateVersion = release.Version;
                    _host.Services.GetRequiredService<ILogger<App>>()
                        .LogInformation(
                            "Windows update notification delivered to Notification Center for version {Version}.",
                            release.Version);
                }
                else
                {
                    _host.Services.GetRequiredService<ILogger<App>>()
                        .LogWarning(
                            "Windows accepted the update notification for version {Version}, but it was not found in Notification Center.",
                            release.Version);
                }
            }
            catch (Exception ex)
            {
                _host.Services.GetRequiredService<ILogger<App>>()
                    .LogWarning(ex, "Windows update notification could not be shown.");
            }
        }

        private async Task ShowUpdateDialogAsync(MainWindow mainWindow, UpdateCheckWorker worker, ReleaseInfo release)
        {
            if (!await _updateDialogLock.WaitAsync(0))
                return;

            try
            {
                _host.Services.GetRequiredService<NotifyIconViewModel>().ShowWindow();
                var dialog = new ContentDialog
                {
                    Title = $"AURA Scheduler v{release.Version} is available",
                    Content = new ScrollViewer
                    {
                        MaxHeight = 420,
                        Content = new TextBlock
                        {
                            Text = string.IsNullOrWhiteSpace(release.Notes)
                                ? "No release notes were provided."
                                : release.Notes,
                            TextWrapping = TextWrapping.Wrap
                        }
                    },
                    PrimaryButtonText = "Download & Install",
                    SecondaryButtonText = "View on GitHub",
                    CloseButtonText = "Later",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = mainWindow.Content?.XamlRoot
                };
                if (dialog.XamlRoot is null)
                    return;

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Secondary)
                {
                    worker.OpenRelease(release);
                }
                else if (result == ContentDialogResult.Primary)
                {
                    var installerStarted = await worker.DownloadAndLaunchInstallerAsync(release);
                    if (installerStarted)
                        await ExitAsync();
                }
            }
            finally
            {
                _updateDialogLock.Release();
            }
        }

        private static async Task ShowMessageAsync(MainWindow mainWindow, string title, string content, string closeButton)
        {
            mainWindow.Activate();
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = closeButton,
                XamlRoot = mainWindow.Content?.XamlRoot
            };
            if (dialog.XamlRoot is not null)
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
            var icon = new TrayIcon("AURA Scheduler");

            icon.AddMenuItem("Show Window", () => vm.ShowWindowCommand.Execute(null));
            icon.AddMenuItem("Hide Window", () => vm.HideWindowCommand.Execute(null));
            icon.AddSeparator();
            icon.AddMenuItem("Exit", () => vm.ExitApplicationCommand.Execute(null));

            icon.DoubleClicked += () => vm.ShowWindowCommand.Execute(null);

            return icon;
        }

        public async Task ExitAsync()
        {
            await StopAsync();
            Application.Current.Exit();
        }

        public async Task StopAsync()
        {
            if (Interlocked.Exchange(ref _stopping, 1) != 0)
                return;

            if (_updateCheckWorker is not null)
                _updateCheckWorker.StateChanged -= OnUpdateCheckStateChanged;

            if (_appNotificationsRegistered)
                AppNotificationManager.Default.Unregister();

            _trayIcon?.Dispose();
            await _host.StopAsync();
        }
    }
}
