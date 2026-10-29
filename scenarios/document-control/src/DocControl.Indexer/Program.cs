using Litware.DocControl.Data;
using Litware.DocControl.Indexer;
using Litware.DocControl.Platform;

// Usage:
//   Litware.DocControl.Indexer                              run as service / console
//   Litware.DocControl.Indexer --scan-once                  scan the drop share once and exit
//   Litware.DocControl.Indexer --import-doctrack <folder> --project <code>
//                                                           unpack DocTrack .dta archives into the project drop folder
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(o => o.ServiceName = "LitwareDocIndexer");
builder.Services.AddDocControlData(builder.Configuration);
builder.Services.Configure<IndexerOptions>(builder.Configuration.GetSection(IndexerOptions.SectionName));
builder.Services.AddSingleton(new EventLogWriter("Litware DocControl Indexer"));
builder.Services.AddSingleton<DropFileProcessor>();
builder.Services.AddSingleton<DropScanner>();

var command = args.FirstOrDefault(a => a.StartsWith("--", StringComparison.Ordinal) && !a.Contains('='));
if (command is null)
{
    builder.Services.AddHostedService<IndexerWorker>();
    builder.Build().Run();
    return 0;
}

using var host = builder.Build();
var log = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Indexer");

switch (command)
{
    case "--scan-once":
        using (var mutex = new Mutex(false, IndexerWorker.MutexName))
        {
            if (!mutex.WaitOne(TimeSpan.FromSeconds(10)))
            {
                log.LogError("The indexer service is scanning; stop it or try again later");
                return 3;
            }
            try
            {
                var result = host.Services.GetRequiredService<DropScanner>().ScanAsync(CancellationToken.None).GetAwaiter().GetResult();
                log.LogInformation("{Accepted} accepted, {Rejected} rejected, {Skipped} skipped", result.Accepted, result.Rejected, result.Skipped);
                return result.Rejected > 0 ? 2 : 0;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

    case "--import-doctrack":
        var folder = ArgValue(args, "--import-doctrack");
        var project = ArgValue(args, "--project");
        if (folder is null || project is null)
        {
            log.LogError("Usage: --import-doctrack <folder> --project <code>");
            return 1;
        }
        var options = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Litware.DocControl.Data.Storage.StorageOptions>>().Value;
        return DocTrackImport.Run(folder, Path.Combine(options.DropRoot, project), log);

    default:
        log.LogError("Unknown command {Command}", command);
        return 1;
}

static string? ArgValue(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
