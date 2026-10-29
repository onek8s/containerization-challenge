using System.Globalization;
using FurnaceRecords.Core;
using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Worker;

/// <summary>Writes a PDF certificate (or survey report) for every released load to the certificate folder.</summary>
internal sealed class CertificateArchiver : BackgroundService
{
    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly WorkerOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<CertificateArchiver> _logger;

    public CertificateArchiver(IServiceScopeFactory scopes, WorkerOptions options, TimeProvider time, ILogger<CertificateArchiver> logger)
    {
        _scopes = scopes;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ArchiveAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Certificate archiving failed");
            }
            await Task.Delay(Poll, _time, stoppingToken);
        }
    }

    internal async Task<int> ArchiveAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FurnaceRecordsContext>();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        var loads = await db.Loads
            .Include(l => l.Furnace).Include(l => l.Recipe).Include(l => l.Parts)
            .Where(l => l.Status == LoadStatus.Released && l.CertificatePath == null)
            .OrderBy(l => l.DecidedAtUtc)
            .Take(20)
            .AsSplitQuery()
            .ToListAsync(ct);

        foreach (var load in loads)
        {
            var year = load.DecidedAtUtc!.Value.Year.ToString(CultureInfo.InvariantCulture);
            var folder = Path.Combine(_options.CertificateFolder, year);
            Directory.CreateDirectory(folder);
            var prefix = load.Kind == LoadKind.Survey ? "TUS" : "HT";
            var path = Path.Combine(folder, $"{prefix}-{load.LoadNumber}.pdf");
            var temp = path + ".tmp";
            await File.WriteAllBytesAsync(temp, CertificateDocument.Render(load, zone), ct);
            File.Move(temp, path, overwrite: true);
            load.CertificatePath = path;
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Archived {Kind} {Load} to {Path}", load.Kind, load.LoadNumber, path);
        }
        return loads.Count;
    }
}

internal static class CertificateDocument
{
    public static byte[] Render(Load load, TimeZoneInfo zone)
    {
        string L(DateTime? utc) => utc is null ? "-" : TimeZoneInfo.ConvertTimeFromUtc(utc.Value, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var r = load.Recipe!;
        var f = load.Furnace!;
        var pdf = new SimplePdf();
        var survey = load.Kind == LoadKind.Survey;
        pdf.Line(survey ? $"Temperature Uniformity Survey Report TUS-{load.LoadNumber}" : $"Heat Treatment Certificate HT-{load.LoadNumber}", 16, bold: true);
        pdf.Line("Adatum Thermal Processing", 11);
        pdf.Space(10);
        pdf.Line($"Specification: {r.Specification}");
        pdf.Line($"Process: {r.Code} {r.Name}");
        pdf.Line($"Temperature: {r.SetpointCelsius:0.#} °C (-{r.ToleranceMinusCelsius:0.#}/+{r.TolerancePlusCelsius:0.#} °C)");
        pdf.Line($"Furnace: {f.Code} {f.Name}, class {f.FurnaceClass} (±{FurnaceClass.UniformityTolerance(f.FurnaceClass):0.#} °C), instrumentation type {f.Instrumentation}");
        pdf.Line($"Cycle: {L(load.StartedAtUtc)} - {L(load.EndedAtUtc)}");
        if (survey)
        {
            pdf.Line($"Survey time after stabilisation: {load.SoakMinutes:0.#} min, spread {load.UniformityCelsius:0.0} °C");
            pdf.Space();
            pdf.Line("Sensor   Min °C   Max °C   Max deviation °C", bold: true);
            foreach (var s in LoadService.SurveySensors(load))
            {
                pdf.Line(string.Create(CultureInfo.InvariantCulture, $"{s.Channel,6}   {s.Min,6:0.0}   {s.Max,6:0.0}   {s.MaxDeviation,6:0.0}"));
            }
        }
        else
        {
            pdf.Line($"Time at temperature: {load.SoakMinutes:0.#} min (required {r.SoakMinutes} min)");
            if (load.QuenchDelaySeconds is { } q) pdf.Line($"Quench delay: {q:0.#} s (max {r.MaxQuenchDelaySeconds} s)");
            pdf.Space();
            pdf.Line("Work order      Part number          Lot          Qty", bold: true);
            foreach (var p in load.Parts)
            {
                pdf.Line($"{p.WorkOrder,-15} {p.PartNumber,-20} {p.Lot,-12} {p.Quantity,4}");
            }
        }
        pdf.Space();
        pdf.Line($"Released by {load.DecidedBy} on {L(load.DecidedAtUtc)}");
        if (!string.IsNullOrEmpty(load.DecisionComment)) pdf.Line($"Remarks: {load.DecisionComment}");
        pdf.Space();
        pdf.Line(survey
            ? "The furnace was surveyed at the temperature above. Temperature records are retained by Adatum Thermal Processing."
            : "The parts listed were heat treated in accordance with the specification above. Temperature records are retained by Adatum Thermal Processing.", 9);
        return pdf.ToArray();
    }
}
