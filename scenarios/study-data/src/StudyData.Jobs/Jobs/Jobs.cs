using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;
using StudyData.Calculation;
using StudyData.Coding;
using StudyData.Core.Documents;
using StudyData.Core.Queries;
using StudyData.Core.Services;
using StudyData.Data;
using StudyData.Data.Services;
using StudyData.Documents;
using StudyData.Integration;
using StudyData.Integration.Labs;
using StudyData.Integration.Mail;
using StudyData.Integration.Odm;

namespace StudyData.Jobs.Jobs;

/// <summary>Casebooks, ODM exports and safety reports requested from the monitor SPA, the sponsor API and the scheduled jobs.</summary>
[DisallowConcurrentExecution]
public class GeneratedDocumentsJob : IJob
{
    private readonly StudyDataContext _db;
    private readonly StudyReadService _read;
    private readonly ICasebookRenderer _renderer;
    private readonly OdmExporter _odm;
    private readonly SafetyReportBuilder _safety;
    private readonly ExportOptions _exports;
    private readonly IClock _clock;
    private readonly ILogger<GeneratedDocumentsJob> _log;

    public GeneratedDocumentsJob(StudyDataContext db, StudyReadService read, ICasebookRenderer renderer, OdmExporter odm, SafetyReportBuilder safety,
        IOptions<ExportOptions> exports, IClock clock, ILogger<GeneratedDocumentsJob> log)
    {
        _db = db;
        _read = read;
        _renderer = renderer;
        _odm = odm;
        _safety = safety;
        _exports = exports.Value;
        _clock = clock;
        _log = log;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var requested = await _db.GeneratedDocuments.Where(d => d.State == JobState.Requested).OrderBy(d => d.Id).Take(20).ToListAsync(ct);
        foreach (var doc in requested)
        {
            doc.State = JobState.Running;
            await _db.SaveChangesAsync(ct);
            try
            {
                var study = (await _read.GetDesignAsync(doc.StudyId, ct))!;
                doc.Path = doc.Kind switch
                {
                    "Casebook" => await CasebookAsync(study, doc, ct),
                    "OdmExport" => await OdmAsync(study, doc, ct),
                    "SafetyWorkbook" => await _safety.BuildAsync(study, ct),
                    _ => throw new InvalidOperationException($"Unknown document kind {doc.Kind}.")
                };
                doc.Sha256 = Hash(doc.Path);
                doc.State = JobState.Completed;
                doc.CompletedAt = _clock.UtcNow;
                _db.Notifications.Add(new Notification
                {
                    At = _clock.UtcNow, StudyId = doc.StudyId, SubjectId = doc.SubjectId, Kind = doc.Kind + "Ready",
                    Text = $"{doc.Kind} #{doc.Id} is ready ({Path.GetFileName(doc.Path)})"
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "{Kind} #{Id} failed", doc.Kind, doc.Id);
                doc.State = JobState.Failed;
                doc.Error = ex.Message;
                doc.CompletedAt = _clock.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<string> CasebookAsync(Core.Design.Study study, GeneratedDocument doc, CancellationToken ct)
    {
        var subject = await _read.GetCasebookAsync(doc.SubjectId!.Value, ct) ?? throw new InvalidOperationException("Unknown subject.");
        var audit = await _db.Audit.Where(a => a.SubjectId == subject.Id).OrderBy(a => a.Id).ToListAsync(ct);
        var queries = await _db.Queries.Where(q => q.SubjectId == subject.Id).ToListAsync(ct);
        var model = CasebookModel.Build(study, subject, audit, queries, doc.RequestedBy, _clock.UtcNow);
        var stem = Path.Combine(_exports.OutputRoot, study.Code, "Casebooks", subject.Site!.Number, $"{subject.Number}_{_clock.UtcNow:yyyyMMdd-HHmm}");
        return await _renderer.WriteArchiveAsync(model, stem, ct);
    }

    private async Task<string> OdmAsync(Core.Design.Study study, GeneratedDocument doc, CancellationToken ct)
    {
        var folder = Path.Combine(_exports.OutputRoot, study.Code, "Exports");
        _exports.ArchivePasswords.TryGetValue(doc.RequestedBy, out var password);
        var result = await _odm.ExportAsync(study, folder, $"{study.Code}_ODM_{_clock.UtcNow:yyyyMMdd-HHmm}_{doc.Id}",
            doc.Parameters == "includeArm", password, _clock.UtcNow, ct);
        _log.LogInformation("ODM export #{Id}: {Subjects} subjects, {Items} items", doc.Id, result.Subjects, result.Items);
        return result.ArchivePath ?? result.XmlPath;
    }

    private static string Hash(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();
    }
}

/// <summary>Requests a new casebook PDF for every subject whose data changed since its last casebook.</summary>
[DisallowConcurrentExecution]
public class NightlyCasebooksJob : IJob
{
    private readonly StudyDataContext _db;
    private readonly IClock _clock;

    public NightlyCasebooksJob(StudyDataContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var lastBySubject = await _db.GeneratedDocuments.Where(d => d.Kind == "Casebook" && d.SubjectId != null && d.State != JobState.Failed)
            .GroupBy(d => d.SubjectId!.Value).Select(g => new { g.Key, At = g.Max(d => d.RequestedAt) }).ToDictionaryAsync(x => x.Key, x => x.At);
        var changed = await _db.Audit.Where(a => a.SubjectId != null).GroupBy(a => a.SubjectId!.Value)
            .Select(g => new { g.Key, At = g.Max(a => a.At) }).ToListAsync();
        var subjects = await _db.Subjects.ToDictionaryAsync(s => s.Id, s => s.StudyId);
        foreach (var c in changed.Where(c => !lastBySubject.TryGetValue(c.Key, out var at) || c.At > at))
        {
            _db.GeneratedDocuments.Add(new GeneratedDocument
            {
                StudyId = subjects[c.Key], SubjectId = c.Key, Kind = "Casebook", State = JobState.Requested, RequestedBy = "nightly", RequestedAt = _clock.UtcNow
            });
        }

        await _db.SaveChangesAsync(context.CancellationToken);
    }
}

[DisallowConcurrentExecution]
public class SafetyReportJob : IJob
{
    private readonly StudyDataContext _db;
    private readonly IClock _clock;

    public SafetyReportJob(StudyDataContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        foreach (var study in await _db.Studies.Where(s => s.Status != Core.Design.StudyStatus.Setup && s.Status != Core.Design.StudyStatus.Locked).ToListAsync())
        {
            _db.GeneratedDocuments.Add(new GeneratedDocument
            {
                StudyId = study.Id, Kind = "SafetyWorkbook", State = JobState.Requested, RequestedBy = "schedule", RequestedAt = _clock.UtcNow,
                Parameters = $"cutoff {_clock.UtcNow:yyyy-MM-dd}"
            });
        }

        await _db.SaveChangesAsync(context.CancellationToken);
    }
}

[DisallowConcurrentExecution]
public class CodingJob : IJob
{
    private readonly CodingService _coding;

    public CodingJob(CodingService coding)
    {
        _coding = coding;
    }

    public Task Execute(IJobExecutionContext context) => _coding.AutoCodeAsync(context.CancellationToken);
}

[DisallowConcurrentExecution]
public class LabImportJob : IJob
{
    private readonly ILabFileSource _source;
    private readonly LabImportService _import;
    private readonly ILogger<LabImportJob> _log;

    public LabImportJob(ILabFileSource source, LabImportService import, ILogger<LabImportJob> log)
    {
        _source = source;
        _import = import;
        _log = log;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        foreach (var file in await _source.FetchAsync(context.CancellationToken))
        {
            var result = await _import.ImportAsync(file, context.CancellationToken);
            if (result.Errors.Count > 0)
            {
                _log.LogWarning("Lab file {File}: {Errors}", file.Name, string.Join("; ", result.Errors.Take(10)));
            }

            await _source.MarkDoneAsync(file, context.CancellationToken);
        }
    }
}

/// <summary>Mails each site a list of its open queries older than the study's reminder interval.</summary>
[DisallowConcurrentExecution]
public class QueryRemindersJob : IJob
{
    private readonly StudyDataContext _db;
    private readonly Mailer _mail;
    private readonly IClock _clock;
    private readonly JobsOptions _options;

    public QueryRemindersJob(StudyDataContext db, Mailer mail, IClock clock, IOptions<JobsOptions> options)
    {
        _db = db;
        _mail = mail;
        _clock = clock;
        _options = options.Value;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var now = _clock.UtcNow;
        var open = await (from q in _db.Queries
                          join s in _db.Subjects on q.SubjectId equals s.Id
                          where q.State == QueryState.Open
                          select new { Query = q, Subject = s, s.Site, s.Study }).ToListAsync();
        foreach (var site in open.Where(x => QueryWorkflow.NeedsReminder(x.Query, now, x.Study!.QueryReminderDays)).GroupBy(x => x.Site!))
        {
            var body = new StringBuilder($"Dear {site.Key.Investigator} and study team,\n\nthe following queries of study {site.First().Study!.Code} at site {site.Key.Number} are waiting for an answer:\n\n");
            foreach (var x in site.OrderBy(x => x.Query.OpenedAt))
            {
                body.AppendLine(CultureInfo.InvariantCulture, $"  #{x.Query.Id}  {x.Subject.Number}  {x.Query.FieldCode}  open since {x.Query.OpenedAt:dd-MMM-yyyy}: {x.Query.Text}");
                x.Query.LastReminderAt = now;
                x.Query.ReminderCount++;
            }

            body.AppendLine().AppendLine($"Answer them in the site portal: {_options.PortalUrl}queries").AppendLine().AppendLine("Fabrikam Clinical Research, data management");
            await _mail.SendAsync(site.Key.ContactEmail, $"{site.First().Study!.Code} site {site.Key.Number}: {site.Count()} open queries", body.ToString(), context.CancellationToken);
        }

        await _db.SaveChangesAsync(context.CancellationToken);
    }
}

public static class JobRunner
{
    private static readonly Dictionary<string, Type> Jobs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["documents"] = typeof(GeneratedDocumentsJob),
        ["casebooks"] = typeof(NightlyCasebooksJob),
        ["safety"] = typeof(SafetyReportJob),
        ["coding"] = typeof(CodingJob),
        ["labs"] = typeof(LabImportJob),
        ["reminders"] = typeof(QueryRemindersJob)
    };

    public static async Task RunOnceAsync(IServiceProvider services, string? name)
    {
        if (name is null || !Jobs.TryGetValue(name, out var type))
        {
            Console.WriteLine($"Usage: StudyData.Jobs run <{string.Join('|', Jobs.Keys)}>");
            return;
        }

        using var scope = services.CreateScope();
        var job = (IJob)ActivatorUtilities.CreateInstance(scope.ServiceProvider, type);
        await job.Execute(new ManualContext());
        if (name.Equals("safety", StringComparison.OrdinalIgnoreCase) || name.Equals("casebooks", StringComparison.OrdinalIgnoreCase))
        {
            await ((IJob)ActivatorUtilities.CreateInstance(scope.ServiceProvider, typeof(GeneratedDocumentsJob))).Execute(new ManualContext());
        }

        Console.WriteLine($"{type.Name} finished.");
    }

    private sealed class ManualContext : IJobExecutionContext
    {
        public CancellationToken CancellationToken => CancellationToken.None;
        public IScheduler Scheduler => throw new NotSupportedException();
        public ITrigger Trigger => throw new NotSupportedException();
        public ICalendar? Calendar => null;
        public bool Recovering => false;
        public TriggerKey RecoveringTriggerKey => throw new NotSupportedException();
        public int RefireCount => 0;
        public JobDataMap MergedJobDataMap { get; } = new();
        public IJobDetail JobDetail => throw new NotSupportedException();
        public IJob JobInstance => throw new NotSupportedException();
        public DateTimeOffset FireTimeUtc => DateTimeOffset.UtcNow;
        public DateTimeOffset? ScheduledFireTimeUtc => null;
        public DateTimeOffset? PreviousFireTimeUtc => null;
        public DateTimeOffset? NextFireTimeUtc => null;
        public string FireInstanceId => "manual";
        public object? Result { get; set; }
        public TimeSpan JobRunTime => TimeSpan.Zero;
        public void Put(object key, object objectValue) { }
        public object? Get(object key) => null;
    }
}
