using ExpenseClaims.Jobs.Hr;
using Microsoft.Extensions.Options;
using Quartz;

namespace ExpenseClaims.Jobs.Jobs;

/// <summary>Imports the newest employee export from the HR inbound folder and moves it to Processed or Failed.</summary>
[DisallowConcurrentExecution]
public sealed class HrEmployeeImportJob : IJob
{
    private readonly HrEmployeeImporter _importer;
    private readonly HrImportOptions _options;
    private readonly ILogger<HrEmployeeImportJob> _logger;

    public HrEmployeeImportJob(HrEmployeeImporter importer, IOptions<HrImportOptions> options, ILogger<HrEmployeeImportJob> logger)
    {
        _importer = importer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var inbound = new DirectoryInfo(_options.InboundFolder);
        if (!inbound.Exists)
        {
            _logger.LogWarning("HR inbound folder {Folder} does not exist", inbound.FullName);
            return;
        }

        var files = inbound.GetFiles(_options.FilePattern).OrderBy(f => f.Name, StringComparer.Ordinal).ToList();
        if (files.Count == 0)
        {
            _logger.LogInformation("No HR export in {Folder}", inbound.FullName);
            return;
        }

        // Every export is a full snapshot; only the newest one matters.
        var newest = files[^1];
        foreach (var older in files.SkipLast(1))
            Move(older, "Processed");

        try
        {
            IReadOnlyList<EmployeeRecord> records;
            await using (var stream = newest.OpenRead())
                records = HrEmployeeImporter.Read(stream);
            var result = await _importer.ImportAsync(records, _options.MaxDeactivationShare, DateTime.Today, context.CancellationToken);
            foreach (var line in result.Rejected)
                _logger.LogWarning("HR import {File}: {Problem}", newest.Name, line);
            Move(newest, "Processed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HR import of {File} failed", newest.Name);
            Move(newest, "Failed");
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }

    private static void Move(FileInfo file, string folder)
    {
        var target = Path.Combine(file.DirectoryName!, folder);
        Directory.CreateDirectory(target);
        var path = Path.Combine(target, file.Name);
        if (File.Exists(path))
            path = Path.Combine(target, $"{Path.GetFileNameWithoutExtension(file.Name)}_{DateTime.Now:HHmmss}{file.Extension}");
        file.MoveTo(path);
    }
}
