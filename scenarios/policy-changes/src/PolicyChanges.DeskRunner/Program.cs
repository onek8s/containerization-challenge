using PolicyChanges.Data;
using PolicyChanges.DeskRunner;
using PolicyChanges.DeskRunner.Desktop;

if (args.Length > 0 && args[0] is "store-credential" or "check")
    return Commands.Run(args);

// One runner per desk session.
using var single = new Mutex(true, @"Local\PolicyChanges.DeskRunner", out var first);
if (!first)
{
    Console.Error.WriteLine("The desk runner is already running in this session.");
    return 1;
}

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.Configure<DeskOptions>(builder.Configuration.GetSection(DeskOptions.Section));
builder.Services.Configure<PolicyDeskOptions>(builder.Configuration.GetSection(PolicyDeskOptions.Section));
builder.Logging.AddProvider(new FileLoggerProvider(builder.Configuration[$"{DeskOptions.Section}:LogPath"] ?? new DeskOptions().LogPath));

builder.Services.AddPolicyChangesData(builder.Configuration);
builder.Services.AddSingleton<PolicyDeskClient>();
builder.Services.AddSingleton<FlowLibrary>();
builder.Services.AddSingleton<EvidenceWriter>();
builder.Services.AddHostedService<DeskWorker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PolicyChangesContext>();
    await DbInitializer.InitializeAsync(db, builder.Configuration.GetValue<bool>("Database:SeedSampleData"));
}

await host.RunAsync();
return 0;
