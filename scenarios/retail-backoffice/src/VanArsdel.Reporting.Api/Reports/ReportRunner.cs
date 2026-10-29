using Dapper;
using MassTransit;
using Microsoft.Extensions.Options;
using VanArsdel.BuildingBlocks.Caching;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.Contracts.Reporting;
using VanArsdel.Reporting.Api.Data;

namespace VanArsdel.Reporting.Api.Reports;

public class ReportOptions
{
    /// <summary>Where the Excel files go.</summary>
    public string OutputFolder { get; set; } = "reports";

    /// <summary>Base URL used in the e-mail links (through the gateway).</summary>
    public string PublicBaseUrl { get; set; } = "";

    public Dictionary<string, List<string>> Recipients { get; set; } = new();
}

public class ReportRunner
{
    private readonly IEnumerable<IReport> _reports;
    private readonly ReportingDb _db;
    private readonly IPublishEndpoint _publish;
    private readonly IDistributedLockProvider _locks;
    private readonly ReportOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ReportRunner> _logger;

    public ReportRunner(IEnumerable<IReport> reports, ReportingDb db, IPublishEndpoint publish, IDistributedLockProvider locks,
        IOptions<ReportOptions> options, IHostEnvironment environment, ILogger<ReportRunner> logger)
    {
        _reports = reports;
        _db = db;
        _publish = publish;
        _locks = locks;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public IEnumerable<IReport> All => _reports;

    public IReport Find(string name) =>
        _reports.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) ?? throw new NotFoundException("Report", name);

    public async Task<List<ReportTable>> QueryAsync(string name, DateTime businessDate, CancellationToken cancellationToken)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        return await Find(name).BuildAsync(connection, businessDate, cancellationToken);
    }

    /// <summary>Builds the Excel file, records the run and tells notifications to mail the link.</summary>
    public async Task<ReportReady> RunAsync(string name, DateTime businessDate, string requestedBy, CancellationToken cancellationToken)
    {
        var report = Find(name);
        // Both app02 nodes get the scheduler call when the load balancer retries; run once.
        await using var handle = await _locks.TryAcquireAsync("report:" + report.Name, TimeSpan.FromMinutes(10), TimeSpan.Zero, cancellationToken);
        if (handle == null)
        {
            throw new ConflictException("Report " + report.Name + " is already running.");
        }

        var tables = await QueryAsync(report.Name, businessDate, cancellationToken);
        var folder = OutputFolder();
        Directory.CreateDirectory(folder);
        var fileName = $"{report.Name}-{businessDate:yyyyMMdd}-{DateTime.UtcNow:HHmmss}.xlsx";
        await using (var file = File.Create(Path.Combine(folder, fileName)))
        {
            ExcelWriter.Write(file, report.Title, businessDate, tables);
        }

        await using (var connection = await _db.OpenAsync(cancellationToken))
        {
            await connection.ExecuteAsync(
                "INSERT INTO ReportRun (ReportName, BusinessDate, FileName, Rows, RequestedBy, CreatedUtc) VALUES (@ReportName, @BusinessDate, @FileName, @Rows, @RequestedBy, @CreatedUtc)",
                new { ReportName = report.Name, BusinessDate = businessDate.Date, FileName = fileName, Rows = tables.Sum(t => t.Rows.Count), RequestedBy = requestedBy, CreatedUtc = DateTime.UtcNow });
        }

        var ready = new ReportReady
        {
            ReportName = report.Name,
            BusinessDate = businessDate.Date,
            FileName = fileName,
            DownloadUrl = _options.PublicBaseUrl.TrimEnd('/') + "/reporting/reports/files/" + fileName,
            Recipients = _options.Recipients.TryGetValue(report.Name, out var to) ? to : new List<string>(),
        };
        await _publish.Publish(ready, cancellationToken);
        _logger.LogInformation("Report {Report} for {Date:yyyy-MM-dd} written to {File}", report.Name, businessDate, fileName);
        return ready;
    }

    public string OutputFolder() =>
        Path.IsPathRooted(_options.OutputFolder) ? _options.OutputFolder : Path.Combine(_environment.ContentRootPath, _options.OutputFolder);
}
