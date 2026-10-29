using FieldService.Data;
using FieldService.Worker;
using FieldService.Worker.Jobs;
using Microsoft.Extensions.Logging.EventLog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "FieldService Worker");
// The host already registers the event log provider; set its source instead of adding a
// second provider, which would be ignored.
builder.Services.Configure<EventLogSettings>(settings =>
{
    settings.SourceName = "FieldService Worker";
    settings.LogName = "Application";
});

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.Section));
builder.Services.AddFieldServiceData(builder.Configuration);
builder.Services.AddHostedService<InboxImportService>();
builder.Services.AddHostedService<NightlyJobsService>();

var host = builder.Build();

if (args.Contains("--run-nightly"))
{
    var nightly = host.Services.GetServices<IHostedService>().OfType<NightlyJobsService>().Single();
    await nightly.RunAsync(DateTime.Today, CancellationToken.None);
    return;
}

host.Run();
