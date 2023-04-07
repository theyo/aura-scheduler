using System.Runtime.InteropServices;

using AuraScheduler.Worker;

using CliWrap;

using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

const string ServiceName = "AURA Scheduler Service";

if (args is { Length: 1 })
{
    string executablePath = Path.Combine(AppContext.BaseDirectory, "AuraScheduler.Worker.exe");

    if (args[0] is "/Install")
    {
        await Cli.Wrap("sc")
                 .WithArguments(new[] { "create", ServiceName, $"binPath={executablePath}", "start=auto" })
                 .ExecuteAsync();

        await Cli.Wrap("sc")
             .WithArguments(new[] { "start", ServiceName })
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

var builder = Host.CreateApplicationBuilder(args);


if (builder.Environment.IsProduction())
{

    var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    var settingsPath = Path.Combine(appDataRoot, "TheYo", "Aura Scheduler", "LightSettings.json");

    builder.Configuration.AddJsonFile(settingsPath, true, true);
}

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = ServiceName;
});

if (OperatingSystem.IsWindows())
{
    // See: https://github.com/dotnet/runtime/issues/47303
    LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);
}

builder.Services.Configure<LightOptions>(builder.Configuration.GetSection(LightOptions.SectionName));
builder.Services.AddHostedService<AuraScheduleWorker>();


IHost host = builder.Build();

host.Run();
