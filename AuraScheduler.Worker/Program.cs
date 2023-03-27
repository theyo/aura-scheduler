using AuraScheduler.Worker;

using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;


using CliWrap;

const string ServiceName = "Aura Scheduler Service";

if (args is { Length: 1 })
{
    string executablePath = Path.Combine(AppContext.BaseDirectory, "AuraScheduler.Worker.exe");

    if (args[0] is "/Install")
    {
        await Cli.Wrap("sc")
            .WithArguments(new[] { "create", ServiceName, $"binPath={executablePath}", "start=auto" })
            .ExecuteAsync();
    }
    else if (args[0] is "/Uninstall")
    {
        await Cli.Wrap("sc")
            .WithArguments(new[] { "stop", ServiceName })
            .ExecuteAsync();

        await Cli.Wrap("sc")
            .WithArguments(new[] { "delete", ServiceName })
            .ExecuteAsync();
    }

    return;
}

IHost host = Host.CreateDefaultBuilder(args)
                 .ConfigureServices((context, services) =>
                 {
                     services.AddWindowsService(options =>
                     {
                         options.ServiceName = ServiceName;
                     });

                     // See: https://github.com/dotnet/runtime/issues/47303
                     LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(services);

                     services.Configure<LightOptions>(context.Configuration.GetSection(LightOptions.SectionName));
                     services.AddHostedService<AuraScheduleWorker>();
                 }).Build();

host.Run();
