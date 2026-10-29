using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts.Reporting;
using VanArsdel.Reporting.Api.Data;
using VanArsdel.Reporting.Api.Reports;

namespace VanArsdel.Reporting.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = Policies.Read)]
public class ReportsController : ControllerBase
{
    private readonly ReportRunner _runner;
    private readonly ReportingDb _db;

    public ReportsController(ReportRunner runner, ReportingDb db)
    {
        _runner = runner;
        _db = db;
    }

    [HttpGet]
    public IEnumerable<object> List() => _runner.All.Select(r => new { r.Name, r.Title });

    /// <summary>Report data as JSON for the back office grids.</summary>
    [HttpGet("{name}")]
    public Task<List<ReportTable>> Query(string name, [FromQuery] DateTime? date, CancellationToken cancellationToken) =>
        _runner.QueryAsync(name, date ?? DateTime.Today.AddDays(-1), cancellationToken);

    /// <summary>Generates the Excel file and mails the link. The scheduler calls this every morning.</summary>
    [HttpPost("{name}/run")]
    [Authorize(Policy = Policies.Run)]
    public Task<ReportReady> Run(string name, [FromQuery] DateTime? date, CancellationToken cancellationToken) =>
        _runner.RunAsync(name, date ?? DateTime.Today.AddDays(-1), User.UserName(), cancellationToken);

    [HttpGet("runs")]
    public async Task<IEnumerable<object>> Runs([FromQuery] string name, [FromQuery] int take = 50)
    {
        await using var connection = await _db.OpenAsync();
        var where = string.IsNullOrEmpty(name) ? "" : "WHERE ReportName = @name";
        return await connection.QueryAsync(
            _db.Top("Id, ReportName, BusinessDate, FileName, Rows, RequestedBy, CreatedUtc", $"FROM ReportRun {where} ORDER BY Id DESC", Math.Clamp(take, 1, 500)),
            new { name });
    }

    [HttpGet("files/{fileName}")]
    public IActionResult Download(string fileName)
    {
        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || fileName.Contains(".."))
        {
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["fileName"] = new[] { "Invalid file name." } });
        }
        var path = Path.Combine(_runner.OutputFolder(), fileName);
        if (!System.IO.File.Exists(path))
        {
            throw new NotFoundException("Report file", fileName);
        }
        return PhysicalFile(path, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
