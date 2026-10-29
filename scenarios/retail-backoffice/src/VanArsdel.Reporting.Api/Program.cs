using Microsoft.Extensions.Diagnostics.HealthChecks;
using VanArsdel.BuildingBlocks.Caching;
using VanArsdel.BuildingBlocks.Hosting;
using VanArsdel.BuildingBlocks.Messaging;
using VanArsdel.BuildingBlocks.Web;
using VanArsdel.Reporting.Api;
using VanArsdel.Reporting.Api.Consumers;
using VanArsdel.Reporting.Api.Data;
using VanArsdel.Reporting.Api.Reports;

ReportingDb.RegisterTypeHandlers();

return VanArsdelHost.RunWeb("reporting-api", args, ReportingApp.Build, (host, a) =>
{
    var db = host.Services.GetRequiredService<ReportingDb>();
    if (VanArsdelHost.HasSwitch(a, "--print-schema"))
    {
        // Reporting has no EF model; the embedded script is the schema.
        var script = ReportingDb.SqlServerScript();
        var file = VanArsdelHost.SwitchValue(a, "--print-schema");
        if (string.IsNullOrEmpty(file)) Console.WriteLine(script); else File.WriteAllText(file, script);
        return false;
    }
    if (host.Services.GetRequiredService<IConfiguration>().GetValue("Database:AutoCreate", false))
    {
        db.CreateSchema();
    }
    return true;
});

public static class ReportingApp
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseVanArsdelSerilog("reporting-api");
        var configuration = builder.Configuration;

        builder.Services.AddVanArsdelApi(configuration, "VanArsdel Reporting API", typeof(Policies).Assembly, Policies.Configure);
        builder.Services.AddSingleton<ReportingDb>();
        builder.Services.AddSingleton(configuration.GetSection("Currency").Get<CurrencyOptions>() ?? new CurrencyOptions());
        builder.Services.AddVanArsdelCaching(configuration, "reporting");
        builder.Services.AddVanArsdelMessaging(configuration, "reporting", x =>
        {
            x.AddConsumer<DimensionConsumers>();
            x.AddConsumer<SalesConsumers>();
        });

        builder.Services.Configure<ReportOptions>(configuration.GetSection("Reports"));
        builder.Services.AddSingleton<IReport, DailySalesFlashReport>();
        builder.Services.AddSingleton<IReport, TopStylesReport>();
        builder.Services.AddSingleton<IReport, ReturnsReport>();
        builder.Services.AddSingleton<IReport, WebOrdersReport>();
        builder.Services.AddScoped<ReportRunner>();

        builder.Services.AddHealthChecks().AddCheck<ReportingDbHealthCheck>("database", HealthStatus.Unhealthy);

        var app = builder.Build();
        app.UseVanArsdelApi(app.Environment);
        return app;
    }
}

public partial class Program
{
}
