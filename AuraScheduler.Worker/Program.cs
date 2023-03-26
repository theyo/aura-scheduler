using AuraScheduler.Worker;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.Configure<LightOptions>(context.Configuration.GetSection(LightOptions.SectionName));
        services.AddHostedService<AuraScheduleWorker>();
    })
    .Build();

host.Run();
