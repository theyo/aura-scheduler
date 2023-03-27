using AuraScheduler.Worker;

using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

IHost host = Host.CreateDefaultBuilder(args)
                 .ConfigureServices((context, services) =>
                 {
                     services.AddWindowsService(options =>
                     {
                         options.ServiceName = "Aura Scheduler Service";
                     });

                     // See: https://github.com/dotnet/runtime/issues/47303
                     LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(services);

                     services.Configure<LightOptions>(context.Configuration.GetSection(LightOptions.SectionName));
                     services.AddHostedService<AuraScheduleWorker>();
                 }).Build();

host.Run();
