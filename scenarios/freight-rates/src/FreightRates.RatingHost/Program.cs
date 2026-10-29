using System.Text.Json;
using FreightRates.RatingHost.Engine;
using FreightRates.RatingHost.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

if (args.Length > 0 && args[0] == "protect-password")
{
    Console.Write("RateEngine licence password: ");
    var password = args.Length > 1 ? args[1] : Console.ReadLine() ?? "";
    Console.WriteLine();
    Console.WriteLine("RatingEngine:EncryptedPassword value for this server:");
    Console.WriteLine(LicencePassword.Protect(password));
    return;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Host.UseWindowsService(o => o.ServiceName = "FreightRates.RatingHost");
builder.Logging.AddEventLog(o =>
{
    o.LogName = "Application";
    o.SourceName = "Freight Rates Rating Host";
});

builder.Services.Configure<EngineOptions>(builder.Configuration.GetSection(EngineOptions.Section));

var provider = builder.Configuration[$"{EngineOptions.Section}:Provider"] ?? "Meridian";
if (string.Equals(provider, "Simulator", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IHostedRatingEngine, SimulatorRatingEngine>();
}
else
{
    builder.Services.AddSingleton<MeridianRatingEngine>();
    builder.Services.AddSingleton<IHostedRatingEngine>(sp => sp.GetRequiredService<MeridianRatingEngine>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<MeridianRatingEngine>());
    builder.Services.AddHostedService<TariffImportService>();
}

builder.Services.AddGrpc(o => o.EnableDetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddHealthChecks().AddCheck<EngineHealthCheck>("rating-engine");

var app = builder.Build();

app.Logger.LogInformation("Rating host starting with provider {Provider}, data path {DataPath}",
    provider, app.Services.GetRequiredService<IOptions<EngineOptions>>().Value.DataPath);

app.MapGrpcService<RatingGrpcService>();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(e => e.Key, e => new { status = e.Value.Status.ToString(), e.Value.Description, e.Value.Data }),
        });
    },
});

app.Run();
