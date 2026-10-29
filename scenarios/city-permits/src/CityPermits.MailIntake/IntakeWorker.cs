using System.Text.Json;
using CityPermits.Archive;
using CityPermits.Data;
using CityPermits.Domain.Model;
using Microsoft.EntityFrameworkCore;

namespace CityPermits.MailIntake;

/// <summary>
/// Picks up scanned post from the mailroom scan folder, reads it, files the scan and opens a
/// draft paper case for the counter team when the form is recognised.
/// </summary>
public sealed class IntakeWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IPageReader _reader;
    private readonly IConfiguration _cfg;
    private readonly ILogger<IntakeWorker> _log;

    public IntakeWorker(IServiceScopeFactory scopes, IPageReader reader, IConfiguration cfg, ILogger<IntakeWorker> log)
    {
        _scopes = scopes; _reader = reader; _cfg = cfg; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var inbox = _cfg["Intake:ScanFolder"] ?? @"D:\Scans\Permits";
        var interval = TimeSpan.FromSeconds(int.Parse(_cfg["Intake:PollSeconds"] ?? "60"));
        Directory.CreateDirectory(inbox);
        while (!ct.IsCancellationRequested)
        {
            foreach (var file in Directory.EnumerateFiles(inbox, "*.tif*").Where(f => File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddSeconds(-30)).ToList())
            {
                try { await ProcessAsync(file, ct); }
                catch (Exception ex) { _log.LogError(ex, "Scan {File} failed", file); Move(file, inbox, "failed"); }
            }
            if (_cfg["Intake:Once"] == "true") break;
            await Task.Delay(interval, ct);
        }
    }

    private async Task ProcessAsync(string file, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PermitsDbContext>();
        var archive = scope.ServiceProvider.GetRequiredService<IDocumentArchive>();
        var refs = scope.ServiceProvider.GetRequiredService<ReferenceAllocator>();
        var name = Path.GetFileName(file);
        if (await db.IntakeItems.AnyAsync(i => i.SourceFile == name, ct)) { Move(file, Path.GetDirectoryName(file)!, "duplicates"); return; }

        var pages = await _reader.ReadAsync(file, ct);
        var form = FormReader.Read(pages);
        var item = new IntakeItem
        {
            SourceFile = name, Pages = pages.Count, FormCode = form?.FormCode, Confidence = form?.Confidence ?? 0,
            ExtractedJson = form is null ? null : JsonSerializer.Serialize(form.Fields), OcrText = string.Join("\n\f\n", pages.Select(p => p.Text)),
            ReceivedUtc = File.GetCreationTimeUtc(file), Status = form is null ? "Unrecognised" : form.Confidence < 0.6 ? "Review" : "Drafted"
        };
        db.IntakeItems.Add(item);

        if (form is not null && form.Confidence >= 0.6)
        {
            var f = form.Fields;
            var applicant = new Applicant
            {
                Type = ApplicantType.Person, DisplayName = f.GetValueOrDefault("Applicant name") ?? "Unknown", Email = f.GetValueOrDefault("Email"),
                Phone = f.GetValueOrDefault("Telephone"), PostalAddress = f.GetValueOrDefault("Site address") ?? f.GetValueOrDefault("Address") ?? f.GetValueOrDefault("Premises address"),
                Postcode = f.GetValueOrDefault("Postcode"), PrefersPaper = true, CreatedUtc = DateTime.UtcNow
            };
            var c = new PermitCase
            {
                Kind = form.Kind, Status = CaseStatus.Draft, Channel = Channel.Paper, Applicant = applicant, CreatedUtc = DateTime.UtcNow,
                Title = $"Paper application {form.FormCode}: {applicant.DisplayName}", SiteAddress = applicant.PostalAddress,
                Description = f.GetValueOrDefault("Description of works"), Team = "Customer Contact",
                Parking = form.Kind == PermitKind.ParkingPermit ? new ParkingDetails { Registration = f.GetValueOrDefault("Vehicle registration"), PermitType = ParkingPermitType.Resident, EmissionBand = VehicleEmissionBand.Standard } : null,
                Business = form.Kind == PermitKind.BusinessLicence ? new BusinessDetails { TradingName = f.GetValueOrDefault("Trading name") ?? "", OpeningHours = f.GetValueOrDefault("Opening hours") } : null,
                Building = form.Kind == PermitKind.BuildingPermit ? new BuildingDetails { WorkType = WorkType.Extension } : null,
            };
            c.Reference = await refs.NextAsync(c.Kind, DateTime.UtcNow.Year, ct);
            await using (var s = File.OpenRead(file))
            {
                var a = await archive.StoreAsync(c.Reference, name, s, ct);
                c.Documents.Add(new CaseDocument
                {
                    Kind = DocumentKind.ScannedApplication, FileName = name, ContentType = "image/tiff", Length = a.Length, Sha256 = a.Sha256,
                    ArchiveKey = a.Key, ArchivePath = a.Path, CreatedUtc = DateTime.UtcNow, CreatedBy = "mail-intake"
                });
            }
            db.Cases.Add(c);
            await db.SaveChangesAsync(ct);
            item.CaseId = c.Id;
        }
        item.ProcessedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        _log.LogInformation("Scan {File}: {Pages} pages, form {Form} ({Confidence:P0}), {Status}", name, pages.Count, item.FormCode, item.Confidence, item.Status);
        Move(file, Path.GetDirectoryName(file)!, "processed");
    }

    private static void Move(string file, string inbox, string sub)
    {
        var dir = Path.Combine(inbox, sub, DateTime.Today.ToString("yyyy-MM"));
        Directory.CreateDirectory(dir);
        File.Move(file, Path.Combine(dir, Path.GetFileName(file)), true);
        var txt = Path.ChangeExtension(file, ".txt");
        if (File.Exists(txt)) File.Move(txt, Path.Combine(dir, Path.GetFileName(txt)), true);
    }
}
