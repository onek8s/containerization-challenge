using CityPermits.Archive;
using CityPermits.Data;
using CityPermits.MailIntake;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(o => o.ServiceName = "CityPermitsMailIntake");
builder.Services.AddSerilog((sp, cfg) => cfg.ReadFrom.Configuration(builder.Configuration));
builder.Services.AddPermitsData(builder.Configuration);
builder.Services.AddDocumentArchive(builder.Configuration);
if (builder.Configuration["Ocr:Engine"] == "Sidecar")
    builder.Services.AddSingleton<IPageReader, SidecarTextPageReader>();
else if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
    builder.Services.AddSingleton<IPageReader, WindowsOcrPageReader>();
builder.Services.AddHostedService<IntakeWorker>();
builder.Build().Run();
