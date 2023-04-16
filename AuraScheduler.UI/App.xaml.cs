using System.IO;
using System.Reflection;
using System.Windows;

using AuraScheduler.UI.Infrastructure;
using AuraScheduler.Worker;

using ControlzEx.Theming;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuraScheduler.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string LightSettingsFileName = "LightSettings.json";

        private readonly IHost _host;

        public App(string[]? args = null)
        {

            var builder = Host.CreateApplicationBuilder(args);

            string settingsPath;

            if (builder.Environment.IsProduction())
            {
                var company = typeof(App).Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company;
                var product = typeof(App).Assembly.GetCustomAttribute<AssemblyProductAttribute>()!.Product;
                var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                settingsPath = Path.Combine(appDataRoot, company, product, LightSettingsFileName);
            }
            else
            {
                settingsPath = LightSettingsFileName;
            }


            builder.Configuration.AddJsonFile(settingsPath, true, true);

            builder.Services.Configure<LightOptions>(builder.Configuration.GetSection(LightOptions.SectionName));
            builder.Services.AddHostedService<AuraScheduleWorker>();

            builder.Services.AddSingleton<ISettingsFileProvider>(x => new SettingsFileProvider(x.GetRequiredService<ILogger<SettingsFileProvider>>(), settingsPath));
            builder.Services.AddSingleton<MainWindow>();
            builder.Services.AddSingleton<SettingsViewModel>();

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
