using System.Threading.Channels;
using NLog.Extensions.Hosting;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Integration.Messaging;
using Proseware.ControlTower.Telematics;

var host = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .UseWindowsService(o => o.ServiceName = "ControlTowerTelematics")
    .ConfigureLogging(l => l.ClearProviders())
    .UseNLog()
    .ConfigureServices((context, services) =>
    {
        services.Configure<TelematicsOptions>(context.Configuration.GetSection(TelematicsOptions.Section));
        services.AddSingleton<IClock, SystemClock>();
        services.AddControlTowerData(context.Configuration);
        services.AddControlTowerMessaging(context.Configuration);
        services.AddSingleton(Channel.CreateBounded<TelematicsReading>(new BoundedChannelOptions(20_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        }));
        services.AddHostedService<MqttIngestService>();
        services.AddHostedService<PositionWriterService>();
    })
    .Build();

await host.Services.EnsureDevelopmentDatabaseAsync(seed: true);
await host.RunAsync();
