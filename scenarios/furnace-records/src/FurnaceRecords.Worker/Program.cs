using FurnaceRecords.Data;
using FurnaceRecords.Worker;
using Microsoft.Extensions.Logging.EventLog;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.AddWindowsService(o => o.ServiceName = "FurnaceRecords.Worker");
if (OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog(new EventLogSettings
    {
        LogName = "Application",
        SourceName = "Furnace Records Worker",
    });
}

var options = builder.Configuration.GetSection(WorkerOptions.Section).Get<WorkerOptions>() ?? new WorkerOptions();
builder.Services.AddSingleton(options);
builder.Services.AddFurnaceRecordsData(builder.Configuration);
builder.Services.AddHttpClient("mes", c =>
{
    c.BaseAddress = new Uri(options.MesAddress.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseDefaultCredentials = true });

builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHostedService<CertificateArchiver>();
builder.Services.AddHostedService<CalibrationReminder>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<FurnaceRecordsContext>(),
        scope.ServiceProvider.GetRequiredService<DatabaseOptions>());
}

host.Run();
