using Humongous.Documents.Consumers;
using Humongous.Documents.Mail;
using Humongous.Documents.Rendering;
using Humongous.Documents.Wording;
using Humongous.Quoting.Data;
using Humongous.Quoting.Messaging;
using MassTransit;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "HumongousDocuments");
builder.Services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddQuotingData(builder.Configuration);
builder.Services.Configure<DocumentOptions>(builder.Configuration.GetSection(DocumentOptions.SectionName));
builder.Services.Configure<MailOptions>(builder.Configuration.GetSection(MailOptions.SectionName));
builder.Services.AddSingleton<ClauseLibrary>();
builder.Services.AddSingleton<DocumentComposer>();
if (builder.Configuration["Mail:Mode"] == "Pickup")
    builder.Services.AddSingleton<IMailSender, PickupMailSender>();
else
    builder.Services.AddSingleton<IMailSender, SmtpMailSender>();

builder.Services.AddQuotingBus(builder.Configuration, bus =>
    bus.AddConsumer<DocumentRequestedConsumer>(c => c.UseConcurrentMessageLimit(4)));

var host = builder.Build();
using (var scope = host.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitialiseAsync();
await host.RunAsync();
