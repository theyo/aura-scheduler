using System.Reflection;

using AuraScheduler.UI.Infrastructure;
using AuraScheduler.Worker;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Microsoft.UI.Dispatching;

using WinRT;

namespace AuraScheduler.UI
{
    public class Program
    {
        private const string SettingsFileName = "settings.json";

        [STAThread]
        public static void Main(string[]? args = null)
        {
            // Required for PublishSingleFile + self-contained Windows App SDK deployments:
            // the bootstrapper needs to know where to find the bundled Windows App Runtime
            // before any WinRT/WinAppSDK API is touched, so this must be the very first thing
            // that runs in Main.
            Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);

            var builder = Host.CreateApplicationBuilder(args);

            string settingsPath;

            if (builder.Environment.IsProduction())
            {
                var company = typeof(App).Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company;
                var product = typeof(App).Assembly.GetCustomAttribute<AssemblyProductAttribute>()!.Product;
                var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                settingsPath = Path.Combine(appDataRoot, company, product, SettingsFileName);

                var settingsFileInfo = new FileInfo(settingsPath);
                if (!settingsFileInfo.Exists)
                {
                    settingsFileInfo.Directory!.Create();

                    // In a PublishSingleFile deployment AppContext.BaseDirectory is the temp
                    // extraction directory for bundled assemblies, NOT the directory that holds
                    // the exe.  Files marked ExcludeFromSingleFile=true (e.g. settings.json)
                    // are placed next to the exe, so we must use the process path to locate them.
                    var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
                    File.Copy(Path.Combine(exeDir, SettingsFileName), settingsPath);
                }
            }
            else
            {
                settingsPath = SettingsFileName;
            }

            builder.Configuration.AddJsonFile(settingsPath, optional: true, reloadOnChange: true);
            builder.Services.Configure<LightOptions>(builder.Configuration.GetSection(LightOptions.SectionName));
            builder.Services.AddSingleton<AuraInitializationStatus>();
            builder.Services.AddHostedService<AuraScheduleWorker>();

            builder.Services.AddSingleton<ISettingsFileProvider>(_ =>
                new SettingsFileProvider(
                    _.GetRequiredService<ILogger<SettingsFileProvider>>(),
                    settingsPath));

            builder.Services.AddSingleton<MainWindow>();
            builder.Services.AddSingleton<NotifyIconViewModel>();
            builder.Services.AddSingleton<SettingsViewModel>();

            builder.Logging.ClearProviders();
            builder.Logging.AddObservableLogger();

            var host = builder.Build();

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger<App>();
                logger.LogError(e.ExceptionObject as Exception, "Unhandled exception");
            };

            Microsoft.UI.Xaml.Application.Start(_ =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);

                new App(host);
            });
        }
    }
}
