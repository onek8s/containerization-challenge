using Litware.DocControl.Data;
using Litware.DocControl.Packager;
using Litware.DocControl.Platform;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(o => o.ServiceName = "LitwareDocPackager");
builder.Services.AddDocControlData(builder.Configuration);
builder.Services.Configure<PackagerOptions>(builder.Configuration.GetSection(PackagerOptions.SectionName));
builder.Services.AddSingleton(new EventLogWriter("Litware DocControl Packager"));
builder.Services.AddSingleton<TransmittalMailer>();
builder.Services.AddSingleton<PackageBuilder>();
builder.Services.AddHostedService<PackageWorker>();

var host = builder.Build();
host.Run();
