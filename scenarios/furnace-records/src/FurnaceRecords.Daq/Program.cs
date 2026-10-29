using FurnaceRecords.Acquisition;
using FurnaceRecords.Acquisition.Simulation;
using FurnaceRecords.Daq;
using FurnaceRecords.Data;
using Microsoft.Extensions.Logging.EventLog;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Host.UseWindowsService(o => o.ServiceName = "FurnaceRecords.Daq");
if (OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog(new EventLogSettings
    {
        LogName = "Application",
        SourceName = "Furnace Records Acquisition",
    });
}

var acquisition = builder.Configuration.GetSection(AcquisitionOptions.Section).Get<AcquisitionOptions>() ?? new AcquisitionOptions();
builder.Services.AddSingleton(acquisition);
builder.Services.AddFurnaceRecordsData(builder.Configuration);
builder.Services.AddSingleton(sp => new SimulatedPlant(acquisition.Simulator, sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<IAcquisitionFactory, AcquisitionFactory>();
builder.Services.AddSingleton<LiveReadings>();
builder.Services.AddHostedService<AcquisitionService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<FurnaceRecordsContext>(),
        scope.ServiceProvider.GetRequiredService<DatabaseOptions>());
}

app.Logger.LogInformation("Acquisition starting with provider {Provider} for furnaces {Furnaces}",
    acquisition.Provider, string.Join(", ", acquisition.Furnaces.Select(f => f.Code)));

var live = app.Services.GetRequiredService<LiveReadings>();
app.MapGet("/live", () => live.All());
app.MapGet("/live/{code}", (string code) => live.Get(code) is { } f ? Results.Ok(f) : Results.NotFound());
app.MapGet("/health", () =>
{
    var all = live.All();
    var ok = all.Count == acquisition.Furnaces.Count && all.All(f => f.Status == "Running" && DateTime.UtcNow - f.UpdatedUtc < TimeSpan.FromMinutes(1));
    return Results.Json(new { status = ok ? "Healthy" : "Unhealthy", provider = acquisition.Provider, furnaces = all.Select(f => new { f.Code, f.Status, f.Board, f.ControllerConnected, f.LastError }) },
        statusCode: ok ? 200 : 503);
});

app.Run();
