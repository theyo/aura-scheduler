using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

using AuraScheduler.Worker;

const string ServiceName = "AURA Scheduler Service";

var builder = Host.CreateApplicationBuilder(args);


if (builder.Environment.IsProduction())
{

    var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    var settingsPath = Path.Combine(appDataRoot, "TheYo", "AURA Scheduler", "LightSettings.json");

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
