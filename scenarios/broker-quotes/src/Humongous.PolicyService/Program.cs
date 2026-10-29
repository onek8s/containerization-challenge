using Humongous.PolicyService.Bordereau;
using Humongous.PolicyService.Consumers;
using Humongous.PolicyService.Jobs;
using Humongous.Quoting.Data;
using Humongous.Quoting.Messaging;
using Humongous.Quoting.Rating;
using MassTransit;
using Serilog;

// Usage:
//   Humongous.PolicyService.exe                         run as the Windows service (or console)
//   Humongous.PolicyService.exe run-job <name> [--period yyyy-MM]
//       runs one job now: renewals, quote-expiry, lapse, bordereau
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "HumongousPolicyService");
builder.Services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddQuotingData(builder.Configuration);
builder.Services.AddRating(builder.Configuration);

builder.Services.Configure<JobOptions>(builder.Configuration.GetSection(JobOptions.SectionName));
builder.Services.Configure<BordereauOptions>(builder.Configuration.GetSection(BordereauOptions.SectionName));
builder.Services.AddScoped<IScheduledJob, RenewalJob>();
builder.Services.AddScoped<IScheduledJob, QuoteExpiryJob>();
builder.Services.AddScoped<IScheduledJob, LapseJob>();
builder.Services.AddScoped<BordereauJob>();
builder.Services.AddScoped<IScheduledJob>(sp =>
{
    var job = sp.GetRequiredService<BordereauJob>();
    job.Period = builder.Configuration["period"];
    return job;
});
if (builder.Configuration["Bordereau:Delivery"] == "Folder")
    builder.Services.AddSingleton<IBordereauDelivery, FolderDelivery>();
else
    builder.Services.AddSingleton<IBordereauDelivery, SftpDelivery>();

var runJob = args.Length >= 2 && args[0] == "run-job" ? args[1] : null;

builder.Services.AddQuotingBus(builder.Configuration, bus =>
{
    if (runJob is not null) return; // one-off job: publish only
    bus.AddConsumer<BindRequestedConsumer>();
    bus.AddConsumer<AdjustmentRequestedConsumer>(c => c.UseConcurrentMessageLimit(2));
    bus.AddConsumer<CancellationRequestedConsumer>();
});
if (runJob is null)
    builder.Services.AddHostedService<JobScheduler>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitialiseAsync();

if (runJob is not null)
{
    await host.StartAsync();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    var ok = await JobScheduler.RunJobAsync(host.Services.GetRequiredService<IServiceScopeFactory>(), runJob, logger, CancellationToken.None);
    await host.StopAsync();
    return ok ? 0 : 1;
}

await host.RunAsync();
return 0;
