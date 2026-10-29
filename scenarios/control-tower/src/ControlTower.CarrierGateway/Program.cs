using NLog.Web;
using Proseware.ControlTower.CarrierGateway;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Integration;
using Proseware.ControlTower.Integration.Messaging;

var logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();
try
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });
    builder.Host.UseWindowsService(o => o.ServiceName = "ControlTowerCarrierGateway");
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    var gateway = builder.Configuration.GetSection(GatewayOptions.Section);
    builder.WebHost.UseUrls(gateway["WebhookUrls"] ?? "http://*:8085");

    builder.Services.Configure<GatewayOptions>(gateway);
    builder.Services.AddControlTowerData(builder.Configuration);
    builder.Services.AddControlTowerMessaging(builder.Configuration);
    builder.Services.AddCarrierIntegrations(builder.Configuration);
    builder.Services.AddCustomsIntegration(builder.Configuration);

    builder.Services.AddHostedService<CarrierBookingService>();
    builder.Services.AddHostedService<EdiInboundService>();
    builder.Services.AddHostedService<TrackingPollService>();
    builder.Services.AddHostedService<CustomsService>();

    var app = builder.Build();
    await app.Services.EnsureDevelopmentDatabaseAsync(seed: true);
    app.MapWebhooks();
    await app.RunAsync();
}
catch (Exception ex)
{
    logger.Fatal(ex, "Carrier gateway stopped");
    throw;
}
finally
{
    NLog.LogManager.Shutdown();
}
