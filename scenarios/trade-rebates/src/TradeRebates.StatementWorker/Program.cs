using ContosoFoods.TradeRebates.Data;
using ContosoFoods.TradeRebates.StatementWorker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Started by the Service Broker External Activator when statement requests arrive.
// Drains the queue and exits; the activator starts it again on the next activation event.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

if (OperatingSystem.IsWindows())
    builder.Logging.AddEventLog();

builder.Services.AddSingleton<RebateDatabase>();
builder.Services.Configure<StatementOptions>(builder.Configuration.GetSection("Statements"));
builder.Services.AddSingleton<IStatementFileWriter, StatementFileWriter>();
builder.Services.AddSingleton<StatementQueueReader>();

using IHost host = builder.Build();
var reader = host.Services.GetRequiredService<StatementQueueReader>();
var log = host.Services.GetRequiredService<ILogger<Program>>();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    RunSummary summary = await reader.DrainAsync(cts.Token);
    log.LogInformation("Statement run finished: {Completed} completed, {Failed} failed, {Other} other messages.",
        summary.Completed, summary.Failed, summary.Other);
    return summary.Failed > 0 ? 2 : 0;
}
catch (Exception ex)
{
    log.LogError(ex, "Statement worker stopped.");
    return 1;
}
