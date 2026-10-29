using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using ShiftClock.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "ShiftClock");
builder.Services.Configure<EventLogSettings>(settings =>
{
    settings.SourceName = EventLogWriter.Source;
    settings.LogName = "Application";
});

builder.Services.Configure<ShiftClockOptions>(builder.Configuration.GetSection(ShiftClockOptions.Section));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ShiftClockOptions>>().Value);
builder.Services.AddSingleton<PunchStore>();
builder.Services.AddSingleton<ReaderState>();
builder.Services.AddSingleton<EventLogWriter>();
builder.Services.AddSingleton<PayrollExporter>();
builder.Services.AddHostedService<BadgeReaderWorker>();
builder.Services.AddHostedService<SupervisorPipeServer>();

builder.Build().Run();
