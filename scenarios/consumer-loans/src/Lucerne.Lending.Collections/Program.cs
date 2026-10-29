using Lucerne.Lending.Collections;
using Lucerne.Lending.Collections.Jobs;
using Lucerne.Lending.Workflow;
using NLog.Extensions.Hosting;

// Lucerne.Lending.Collections.exe            Windows service (core event listener + scheduled jobs)
// Lucerne.Lending.Collections.exe run <job>  run one job now (daily-stages, payment-plans, gl-export, agency-handover)
var runOnce = args.Length >= 2 && args[0] == "run" ? args[1] : null;

var builder = Host.CreateDefaultBuilder(runOnce == null ? args : Array.Empty<string>())
    .UseWindowsService(o => o.ServiceName = "LucerneLendingCollections")
    .UseNLog()
    .ConfigureServices((context, services) =>
    {
        services.AddLendingWorkflow(context.Configuration, coreEvents: runOnce == null);
        services.AddSingleton<ICollectionsJob, DailyStagesJob>();
        services.AddSingleton<ICollectionsJob, PaymentPlanJob>();
        services.AddSingleton<ICollectionsJob, GeneralLedgerExportJob>();
        services.AddSingleton<ICollectionsJob, AgencyHandoverJob>();
        services.AddSingleton<JobRunner>();
        if (runOnce == null)
        {
            services.AddHostedService<CoreEventListener>();
            services.AddHostedService<Scheduler>();
        }
    });

using var host = builder.Build();
if (runOnce != null)
{
    var ok = await host.Services.GetRequiredService<JobRunner>().RunAsync(runOnce);
    return ok ? 0 : 1;
}
await host.RunAsync();
return 0;
