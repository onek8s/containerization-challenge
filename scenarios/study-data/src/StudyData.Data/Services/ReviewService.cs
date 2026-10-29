using Microsoft.EntityFrameworkCore;
using StudyData.Core.Design;
using StudyData.Core.Documents;
using StudyData.Core.Queries;
using StudyData.Core.Services;
using StudyData.Core.Subjects;

namespace StudyData.Data.Services;

/// <summary>Source data verification by monitors, investigator signatures, form locking by data management.</summary>
public class ReviewService
{
    private readonly StudyDataContext _db;
    private readonly AuditWriter _audit;
    private readonly IClock _clock;
    private readonly StudyReadService _read;

    public ReviewService(StudyDataContext db, AuditWriter audit, IClock clock, StudyReadService read)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _read = read;
    }

    public async Task<int> VerifyAsync(int formInstanceId, IReadOnlyCollection<string>? fieldCodes, string user, CancellationToken ct = default)
    {
        var form = await LoadFormAsync(formInstanceId, ct);
        var design = await _db.FormDefinitions.Include(f => f.Fields).FirstAsync(f => f.Id == form.FormDefinitionId, ct);
        if (form.Status < FormStatus.Complete)
        {
            throw new SubjectRuleException("Only complete forms can be verified.");
        }

        var sdvFields = design.Fields.Where(f => f.Sdv && f.Type != FieldType.Derived).Select(f => f.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = _clock.UtcNow;
        var count = 0;
        foreach (var value in form.Values.Where(v => sdvFields.Contains(v.FieldCode) && !v.Verified))
        {
            if (fieldCodes is not null && !fieldCodes.Contains(value.FieldCode, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            value.Verified = true;
            value.VerifiedBy = user;
            value.VerifiedAt = now;
            _audit.Add(user, "Sdv", $"{form.Visit!.Subject!.Number}/{design.Code}.{value.FieldCode}", form.Visit.SubjectId, "Verified", newValue: value.Value);
            count++;
        }

        var allVerified = form.Values.Where(v => sdvFields.Contains(v.FieldCode)).All(v => v.Verified);
        if (allVerified && form.Status == FormStatus.Complete)
        {
            form.Status = FormStatus.Verified;
        }

        await _db.SaveChangesAsync(ct);
        return count;
    }

    /// <summary>
    /// Investigator signature of the whole casebook of a subject (all complete forms). The caller has
    /// re-authenticated the investigator with their password.
    /// </summary>
    public async Task<int> SignCasebookAsync(int subjectId, string user, string meaning, CancellationToken ct = default)
    {
        var subject = await _read.GetCasebookAsync(subjectId, ct) ?? throw new SubjectRuleException("Unknown subject.");
        var openQueries = await _db.Queries.CountAsync(q => q.SubjectId == subjectId && (q.State == QueryState.Open || q.State == QueryState.Answered), ct);
        if (openQueries > 0)
        {
            throw new SubjectRuleException($"{subject.Number} has {openQueries} open queries.");
        }

        var forms = subject.Visits.SelectMany(v => v.Forms).Where(f => f.Status is FormStatus.Complete or FormStatus.Verified).ToList();
        var incomplete = subject.Visits.Where(v => v.Status != VisitStatus.Planned).SelectMany(v => v.Forms).Count(f => f.Status == FormStatus.InProgress);
        if (incomplete > 0)
        {
            throw new SubjectRuleException($"{subject.Number} has {incomplete} incomplete forms.");
        }

        var now = _clock.UtcNow;
        foreach (var f in forms)
        {
            f.Status = FormStatus.Signed;
            f.SignedAt = now;
            f.SignedBy = user;
            f.RowVersion = SubjectService.NewVersion();
        }

        _audit.Add(user, "Signature", subject.Number, subject.Id, "CasebookSigned", newValue: $"{forms.Count} forms", reason: meaning);
        _db.Notifications.Add(new Notification { At = now, StudyId = subject.StudyId, SiteId = subject.SiteId, SubjectId = subject.Id, Kind = "CasebookSigned", Text = $"{subject.Number} signed by {user}" });
        await _db.SaveChangesAsync(ct);
        return forms.Count;
    }

    public async Task LockAsync(int formInstanceId, bool locked, string user, CancellationToken ct = default)
    {
        var form = await LoadFormAsync(formInstanceId, ct);
        var old = form.Status;
        if (locked)
        {
            if (form.Status < FormStatus.Complete)
            {
                throw new SubjectRuleException("Incomplete forms cannot be locked.");
            }

            form.Status = FormStatus.Locked;
        }
        else if (form.Status == FormStatus.Locked)
        {
            form.Status = form.SignedAt is not null ? FormStatus.Signed : FormStatus.Complete;
        }

        _audit.Add(user, "Form", formInstanceId.ToString(), form.Visit!.SubjectId, locked ? "Locked" : "Unlocked", old.ToString(), form.Status.ToString());
        await _db.SaveChangesAsync(ct);
    }

    private async Task<FormInstance> LoadFormAsync(int id, CancellationToken ct) =>
        await _db.Forms.Include(f => f.Values).Include(f => f.Visit).ThenInclude(v => v!.Subject).FirstOrDefaultAsync(f => f.Id == id, ct)
        ?? throw new SubjectRuleException("Unknown form.");
}
