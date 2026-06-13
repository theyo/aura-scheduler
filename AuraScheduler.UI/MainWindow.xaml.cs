using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;

using AuraScheduler.UI.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AuraScheduler.UI
{
    public sealed partial class MainWindow : Window
    {
        private readonly Lazy<NotifyIconViewModel> _notifyIconVM;

        public ObservableCollection<string>? LogEntries { get; }
        public SettingsViewModel SettingsViewModel { get; }

        public MainWindow(SettingsViewModel viewModel, ILoggerProvider logProvider, IServiceProvider serviceProvider)
        {
            _notifyIconVM = new Lazy<NotifyIconViewModel>(serviceProvider.GetRequiredService<NotifyIconViewModel>);
            SettingsViewModel = viewModel;

            if (logProvider is IObservableLoggerProvider observableProvider)
                LogEntries = observableProvider.LogEntries;

            InitializeComponent();

            RootGrid.DataContext = this;

            SystemBackdrop = new MicaBackdrop();

            // Remove maximize button — this is a fixed-size utility window
            if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                presenter.IsMaximizable = false;

            // Modern extended title bar
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                // Keep caption button foreground in sync with theme
                UpdateCaptionButtonColors(RootGrid.ActualTheme);
                RootGrid.ActualThemeChanged += (_, _) => UpdateCaptionButtonColors(RootGrid.ActualTheme);

                SetTitleBar(AppTitleBar);
            }

            var hIcon = AppIcon.ExtractLargeIcon();
            if (hIcon != IntPtr.Zero)
            {
                AppWindow.SetIcon(Win32Interop.GetIconIdFromIcon(hIcon));
                AppIcon.DestroyIcon(hIcon);
            }

            _ = LoadTitleBarIconAsync();

            AppWindow.Resize(new Windows.Graphics.SizeInt32(960, 640));
            AppWindow.Title = "AURA Scheduler";

            // Center on the display that contains this window
            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
            var workArea = displayArea.WorkArea;
            var size = AppWindow.Size;
            AppWindow.Move(new Windows.Graphics.PointInt32(
                workArea.X + (workArea.Width - size.Width) / 2,
                workArea.Y + (workArea.Height - size.Height) / 2));

            // Select Dashboard by default
            NavView.SelectedItem = DashboardItem;

            AppWindow.Closing += (_, args) =>
            {
                args.Cancel = true;
                if (SettingsViewModel.CloseToTray)
                    _notifyIconVM.Value.HideWindow();
                else
                    Application.Current.Exit();
            };
        }

        private async Task LoadTitleBarIconAsync()
        {
            using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("icon.ico");
            if (stream is null)
                return;

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            TitleBarIcon.Source = bitmap;
        }

        private void UpdateCaptionButtonColors(ElementTheme theme)
        {
            var titleBar = AppWindow.TitleBar;
            if (theme == ElementTheme.Dark)
            {
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonPressedForegroundColor = Colors.White;
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF);
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF);
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF);
            }
            else
            {
                titleBar.ButtonForegroundColor = Colors.Black;
                titleBar.ButtonHoverForegroundColor = Colors.Black;
                titleBar.ButtonPressedForegroundColor = Colors.Black;
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(0x66, 0x00, 0x00, 0x00);
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x1A, 0x00, 0x00, 0x00);
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x33, 0x00, 0x00, 0x00);
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var tag = (args.SelectedItem as NavigationViewItem)?.Tag?.ToString();
            DashboardPanel.Visibility = tag == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
            SettingsPanel.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }
    }
}
