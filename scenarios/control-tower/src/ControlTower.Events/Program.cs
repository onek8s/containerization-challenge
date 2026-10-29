using NLog.Extensions.Hosting;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Events;
using Proseware.ControlTower.Integration.Messaging;
using Proseware.ControlTower.Routing;
using Proseware.ControlTower.Rules;

var host = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .UseWindowsService(o => o.ServiceName = "ControlTowerEvents")
    .ConfigureLogging(l => l.ClearProviders())
    .UseNLog()
    .ConfigureServices((context, services) =>
    {
        services.Configure<EventsOptions>(context.Configuration.GetSection(EventsOptions.Section));
        services.AddSingleton<IClock, SystemClock>();
        services.AddControlTowerData(context.Configuration);
        services.AddControlTowerMessaging(context.Configuration);
        services.AddRoutePlanning(context.Configuration);
        services.AddSingleton<AlertEvaluator>();
        services.AddSingleton<VehicleMotionState>();
        services.AddScoped<MilestoneProcessor>();
        services.AddScoped<EtaService>();
        services.AddScoped<AlertReconciler>();
        services.AddHostedService<EventSubscriptionsService>();
        services.AddHostedService<AlertSweepService>();
    })
    .Build();

await host.Services.EnsureDevelopmentDatabaseAsync(seed: true);
await host.RunAsync();
