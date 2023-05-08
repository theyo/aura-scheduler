using System;
using System.IO;
using System.Reflection;
using System.Windows;

using AuraScheduler.UI.Infrastructure;
using AuraScheduler.Worker;

using Hardcodet.Wpf.TaskbarNotification;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuraScheduler.UI
{
    public class Program
    {
        private static IHost? _host;

        private const string LightSettingsFileName = "LightSettings.json";

        [STAThread]
        public static void Main(string[]? args = null)
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

            builder.Services.AddSingleton<App>();
            builder.Services.AddSingleton<MainWindow>();
            builder.Services.AddSingleton<NotifyIconViewModel>();
            builder.Services.AddSingleton<SettingsViewModel>();

            builder.Logging.ClearProviders();

            builder.Logging.AddObservableLogger();

            _host = builder.Build();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            var app = _host.Services.GetRequiredService<App>();
            var mainWindow = _host!.Services.GetRequiredService<MainWindow>();
            var notifyIconVM =_host.Services.GetRequiredService<NotifyIconViewModel>();

            app.MainWindow = mainWindow;

            app.SetTaskBarIconViewModel(notifyIconVM);

            app.Startup += Application_Startup;
            app.Exit += Application_Exit;

            app.Run();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var loggerFactory = _host!.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<App>();

            logger.LogError(e.ExceptionObject as Exception, "An unhandled error occurred");
        }

        private static async void Application_Startup(object sender, StartupEventArgs e)
        {
            Application.Current.MainWindow.Show();

            await Task.Run(async () =>
            {
                await _host!.StartAsync();
            });
        }

        private static async void Application_Exit(object sender, ExitEventArgs e)
        {
            using (_host)
            {
                await _host!.StopAsync();
            }
        }

    }
}
