using System.Windows;

using AuraScheduler.Worker;
using AuraScheduler.UI.Infrastructure;

using ControlzEx.Theming;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Windows.UI.ApplicationSettings;

namespace AuraScheduler.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IHost _host;

        public App(string[]? args = null)
        {

            var builder = Host.CreateApplicationBuilder(args);

#if !DEBUG
            var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var settingsPath = Path.Combine(appDataRoot, "TheYo", "AURA Scheduler", "LightSettings.json");

            builder.Configuration.AddJsonFile(settingsPath, true, true);
#endif

            builder.Services.Configure<LightOptions>(builder.Configuration.GetSection(LightOptions.SectionName));
            builder.Services.AddHostedService<AuraScheduleWorker>();

            builder.Services.AddSingleton<MainWindow>();

            builder.Logging.ClearProviders();

            builder.Logging.AddObservableLogger();

            _host = builder.Build();


            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<App>();

                logger.LogError(args.ExceptionObject as Exception, "An unhandled error occurred");
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ThemeManager.Current.ThemeSyncMode = ThemeSyncMode.SyncWithAppMode;
            ThemeManager.Current.SyncTheme();
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private async void Application_Exit(object sender, ExitEventArgs e)
        {
            using (_host)
            {
                await _host.StopAsync();
            }
        }
    }
}
