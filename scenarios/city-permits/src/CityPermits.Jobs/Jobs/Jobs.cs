using System.Text.Json;
using CityPermits.Archive;
using CityPermits.Data;
using CityPermits.Documents;
using CityPermits.Domain.Model;
using CityPermits.Domain.Rules;
using CityPermits.Domain.Workflow;
using CityPermits.Gis;
using CityPermits.Integration;
using Microsoft.EntityFrameworkCore;

namespace CityPermits.Jobs.Jobs;

/// <summary>Sends applicant and officer notifications queued by the web apps.</summary>
public sealed class OutboxJob : JobBase
{
    private readonly Mailer _mail;

    public OutboxJob(PermitsDbContext db, ILogger<OutboxJob> log, Mailer mail) : base(db, log) => _mail = mail;

    protected override async Task<int> RunAsync(CancellationToken ct)
    {
        var batch = await Db.Outbox.Where(o => o.ProcessedUtc == null && o.Attempts < 5).OrderBy(o => o.Id).Take(50).ToListAsync(ct);
        foreach (var m in batch)
        {
            try
            {
                using var doc = JsonDocument.Parse(m.Payload);
                var caseId = doc.RootElement.GetProperty("Id").GetInt32();
                var c = await Db.Cases.Include(x => x.Applicant).FirstAsync(x => x.Id == caseId, ct);
                switch (m.Type)
                {
                    case OutboxTypes.ApplicantNotification when c.Applicant?.Email is { Length: > 0 } email:
                        var template = doc.RootElement.GetProperty("Template").GetString();
                        _mail.Send(email, $"{c.Reference}: {Subject(template)}",
                            $"Dear {c.Applicant.DisplayName},\n\n{Body(template, c)}\n\nView your application: https://permits.ashford.gov.uk/Cases\n\nCity of Ashford Council");
                        break;
                    case OutboxTypes.OfficerNotification:
                        _mail.Send($"{(c.Team ?? "permits").ToLowerInvariant().Replace(' ', '.')}@ashford.gov.uk", $"New case {c.Reference}", $"{c.Title}\n{c.SiteAddress}");
                        break;
                }
                m.ProcessedUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                m.Attempts++;
                m.LastError = ex.Message;
            }
        }
        await Db.SaveChangesAsync(ct);
        return batch.Count;
    }

    private static string Subject(string? t) => t switch
    {
        "submitted" => "application received",
        "issued" => "your permit has been issued",
        "refused" => "decision on your application",
        "information" => "more information needed",
        _ => "update"
    };

    private static string Body(string? t, PermitCase c) => t switch
    {
        "submitted" => $"We have received your application and payment. We aim to decide by {c.TargetDecisionDate:d MMMM yyyy}.",
        "issued" => "Your permit has been issued. You can download it from your account.",
        "refused" => "A decision has been made. The decision notice explains the reasons and how to appeal.",
        _ => "There is an update on your application."
    };
}

/// <summary>
/// Files decision documents in RecordsOne. Records are captured by reference: the records
/// server reads each file from the archive share path, so only metadata goes over SOAP.
/// </summary>
public sealed class RecordsFilingJob : JobBase
{
    private readonly IRecordCaptureService _records;
    private readonly IDocumentArchive _archive;
    private readonly RecordsOptions _options;

    public RecordsFilingJob(PermitsDbContext db, ILogger<RecordsFilingJob> log, IRecordCaptureService records, IDocumentArchive archive, RecordsOptions options)
        : base(db, log) { _records = records; _archive = archive; _options = options; }

    protected override async Task<int> RunAsync(CancellationToken ct)
    {
        var docs = await Db.CaseDocuments.Where(d => d.RecordsFiledUtc == null && d.RecordsAttempts < 10
                && (d.Kind == DocumentKind.DecisionLetter || d.Kind == DocumentKind.PermitCertificate || d.Kind == DocumentKind.InspectionReport || d.Kind == DocumentKind.ScannedApplication))
            .OrderBy(d => d.Id).Take(100).ToListAsync(ct);
        var filed = 0;
        foreach (var d in docs)
        {
            var c = await Db.Cases.FindAsync(new object[] { d.CaseId }, ct);
            try
            {
                var path = await _archive.SharePathAsync(d.ArchiveKey, ct) ?? throw new InvalidOperationException("No archive path.");
                var res = await _records.CaptureByReferenceAsync(new CaptureRequest
                {
                    Container = $"{_options.ContainerPrefix}/{c!.Reference}",
                    Title = $"{c.Reference} {d.Kind} {d.FileName}",
                    RecordType = d.Kind.ToString(),
                    SourcePath = path,
                    RetentionSchedule = RecordsClientFactory.RetentionFor(d.Kind.ToString()),
                    ExternalId = $"CP-DOC-{d.Id}",
                    Sha256 = d.Sha256
                });
                d.RecordsSystemId = res.RecordNumber;
                d.RecordsFiledUtc = DateTime.UtcNow;
                d.RecordsError = null;
                filed++;
            }
            catch (Exception ex)
            {
                d.RecordsAttempts++;
                d.RecordsError = ex.Message;
                Log.LogWarning(ex, "Filing document {Doc} failed", d.Id);
            }
        }
        await Db.SaveChangesAsync(ct);
        return filed;
    }
}

public sealed class RecordsSimulator : IRecordCaptureService
{
    private int _n = 100000;
    public Task<CaptureResponse> CaptureByReferenceAsync(CaptureRequest request) =>
        Task.FromResult(new CaptureResponse { RecordNumber = $"R{DateTime.UtcNow:yy}/{Interlocked.Increment(ref _n)}" });
    public Task<RecordInfo> GetRecordAsync(string recordNumber) => Task.FromResult(new RecordInfo { RecordNumber = recordNumber });
}

/// <summary>Renewal reminders for licences and parking permits; creates the renewal draft on the first one.</summary>
public sealed class RenewalJob : JobBase
{
    private readonly PermitDocuments _docs;
    private readonly IDocumentArchive _archive;
    private readonly Mailer _mail;

    public RenewalJob(PermitsDbContext db, ILogger<RenewalJob> log, PermitDocuments docs, IDocumentArchive archive, Mailer mail) : base(db, log)
    {
        _docs = docs; _archive = archive; _mail = mail;
    }

    protected override async Task<int> RunAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var horizon = today.AddDays(61);
        var candidates = await Db.Cases.Include(c => c.Applicant).Include(c => c.Documents)
            .Where(c => c.Status == CaseStatus.Issued && c.ValidTo != null && c.ValidTo <= horizon && c.Kind != PermitKind.BuildingPermit)
            .ToListAsync(ct);
        var sent = 0;
        foreach (var c in candidates)
        {
            var due = RenewalPolicy.Due(c, today);
            if (due is null) continue;
            if (due.ReminderNumber == 1 && !await Db.Cases.AnyAsync(x => x.RenewalOfCaseId == c.Id, ct))
            {
                var renewal = RenewalPolicy.CreateRenewal(c, DateTime.UtcNow);
                renewal.Reference = await new ReferenceAllocator(Db).NextAsync(renewal.Kind, today.Year, ct);
                Db.Cases.Add(renewal);
            }
            var letter = _docs.RenewalReminder(c, due.ReminderNumber, "https://permits.ashford.gov.uk/Cases");
            if (c.Applicant?.PrefersPaper == true || string.IsNullOrEmpty(c.Applicant?.Email))
            {
                using var ms = new MemoryStream(letter.Content);
                var a = await _archive.StoreAsync(c.Reference, letter.FileName, ms, ct);
                c.Documents.Add(new CaseDocument
                {
                    Kind = letter.Kind, FileName = letter.FileName, ContentType = letter.ContentType, Length = a.Length, Sha256 = a.Sha256,
                    ArchiveKey = a.Key, ArchivePath = a.Path, CreatedUtc = DateTime.UtcNow, CreatedBy = "jobs", VisibleToApplicant = true
                });
            }
            else
            {
                _mail.Send(c.Applicant!.Email!, $"{c.Reference}: renew by {c.ValidTo:d MMMM yyyy}", "Your permit is due for renewal. The reminder is attached.",
                    new[] { (letter.FileName, letter.Content) });
            }
            c.RenewalReminderCount = due.ReminderNumber;
            sent++;
        }
        await Db.SaveChangesAsync(ct);
        return sent;
    }
}

public sealed class ExpiryJob : JobBase
{
    public ExpiryJob(PermitsDbContext db, ILogger<ExpiryJob> log) : base(db, log) { }

    protected override async Task<int> RunAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var expired = await Db.Cases.Include(c => c.Events).Where(c => c.Status == CaseStatus.Issued && c.ValidTo < today).ToListAsync(ct);
        foreach (var c in expired) CaseWorkflow.Apply(c, CaseWorkflow.Expire, "jobs", new[] { Roles.System }, null, DateTime.UtcNow);
        await Db.SaveChangesAsync(ct);
        return expired.Count;
    }
}

/// <summary>Refreshes this server's GIS replica and re-checks open building cases whose site data changed.</summary>
public sealed class GisReplicaJob : JobBase
{
    private readonly IGisService _gis;

    public GisReplicaJob(PermitsDbContext db, ILogger<GisReplicaJob> log, IGisService gis) : base(db, log) => _gis = gis;

    protected override async Task<int> RunAsync(CancellationToken ct)
    {
        var result = _gis.SynchroniseReplica(ct);
        if (result.Sum(r => r.Inserted + r.Updated + r.Deleted) == 0) return 0;
        var open = await Db.Cases.Include(c => c.Events)
            .Where(c => c.Kind == PermitKind.BuildingPermit && c.ParcelId != null && c.Status >= CaseStatus.IntakeCheck && c.Status <= CaseStatus.DecisionPending)
            .ToListAsync(ct);
        foreach (var c in open)
        {
            var site = _gis.CheckSite(new Contracts.SiteCheckRequest(c.ParcelId!, c.Building?.FootprintWkt));
            var json = JsonSerializer.Serialize(WithoutTimestamp(site));
            if (json != c.SpatialCheckJson)
            {
                c.SpatialCheckJson = json;
                c.SpatialCheckedUtc = DateTime.UtcNow;
                c.Events.Add(new CaseEvent { AtUtc = DateTime.UtcNow, Actor = "jobs", Type = "site-checks", Text = "Site data changed after GIS update." });
            }
        }
        await Db.SaveChangesAsync(ct);
        return open.Count;
    }

    private static Domain.SiteConstraints WithoutTimestamp(Domain.SiteConstraints s) { s.CheckedUtc = default; return s; }
}

/// <summary>Reconciles the Paystream daily settlement file with payments.</summary>
public sealed class SettlementJob : JobBase
{
    private readonly IConfiguration _cfg;

    public SettlementJob(PermitsDbContext db, ILogger<SettlementJob> log, IConfiguration cfg) : base(db, log) => _cfg = cfg;

    protected override async Task<int> RunAsync(CancellationToken ct)
    {
        var folder = _cfg["Settlement:InboxFolder"] ?? @"D:\Interfaces\Paystream\Inbox";
        if (!Directory.Exists(folder)) return 0;
        var n = 0;
        foreach (var file in Directory.GetFiles(folder, "settlement_*.csv").OrderBy(f => f))
        {
            using (var r = new StreamReader(file))
                foreach (var line in PaystreamClient.ParseSettlement(r))
                {
                    var p = await Db.Payments.FirstOrDefaultAsync(x => x.ProviderReference == line.PaymentId, ct);
                    if (p is null) { Log.LogWarning("Settlement line for unknown payment {Payment}", line.PaymentId); continue; }
                    p.Status = PaymentStatus.Settled;
                    p.SettledUtc = line.SettledUtc;
                    p.SettlementBatch = line.Batch;
                    n++;
                }
            await Db.SaveChangesAsync(ct);
            var done = Path.Combine(folder, "processed");
            Directory.CreateDirectory(done);
            File.Move(file, Path.Combine(done, Path.GetFileName(file)), true);
        }
        return n;
    }
}

public sealed class ConsultationReminderJob : JobBase
{
    private readonly Mailer _mail;

    public ConsultationReminderJob(PermitsDbContext db, ILogger<ConsultationReminderJob> log, Mailer mail) : base(db, log) => _mail = mail;

    protected override async Task<int> RunAsync(CancellationToken ct)
    {
        var soon = DateOnly.FromDateTime(DateTime.Today.AddDays(3));
        var open = await Db.Consultations.Where(c => c.RespondedUtc == null && c.DueDate <= soon).ToListAsync(ct);
        foreach (var c in open)
            _mail.Send($"{c.Consultee.ToLowerInvariant().Replace(' ', '.')}@consultees.ashford.gov.uk", "Consultation response due", $"Response due {c.DueDate:d MMM yyyy}: {c.Reason}");
        return open.Count;
    }
}
