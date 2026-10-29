using System.Globalization;
using Microsoft.EntityFrameworkCore;
using StudyData.Core.Coding;
using StudyData.Core.Design;
using StudyData.Core.Documents;
using StudyData.Core.EditChecks;
using StudyData.Core.Queries;
using StudyData.Core.Services;
using StudyData.Core.Subjects;

namespace StudyData.Data.Services;

public record SaveFormRequest(int FormInstanceId, IReadOnlyDictionary<string, string?> Values, string User, string? Reason, byte[]? RowVersion);

public record SaveFormResult(
    bool Saved,
    FormInstance? Form,
    IReadOnlyList<CheckResult> Errors,
    IReadOnlyList<CheckResult> Queries,
    IReadOnlyList<CheckResult> Warnings,
    IReadOnlyDictionary<string, string?> Derived,
    IReadOnlyList<string> Messages);

public class FormConflictException : Exception
{
    public FormConflictException() : base("The form was changed by someone else. Reload it and enter your changes again.")
    {
    }
}

/// <summary>
/// Saving an eCRF: field checks, derivations from the study workbook, edit checks, audit trail with reasons
/// for change, automatic queries, coding items and SDV reset of changed fields.
/// </summary>
public class FormEntryService
{
    private readonly StudyDataContext _db;
    private readonly AuditWriter _audit;
    private readonly IClock _clock;
    private readonly StudyReadService _read;
    private readonly IDerivationService _derivation;

    public FormEntryService(StudyDataContext db, AuditWriter audit, IClock clock, StudyReadService read, IDerivationService derivation)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _read = read;
        _derivation = derivation;
    }

    public async Task<SaveFormResult> SaveAsync(SaveFormRequest request, CancellationToken ct = default)
    {
        var instance = await _db.Forms.Include(f => f.Values).Include(f => f.Visit)
            .FirstOrDefaultAsync(f => f.Id == request.FormInstanceId, ct)
            ?? throw new SubjectRuleException("Unknown form.");
        if (request.RowVersion is not null && !request.RowVersion.SequenceEqual(instance.RowVersion))
        {
            throw new FormConflictException();
        }

        var subject = await _read.GetCasebookAsync(instance.Visit!.SubjectId, ct) ?? throw new SubjectRuleException("Unknown subject.");
        var study = await _read.GetDesignAsync(subject.StudyId, ct) ?? throw new SubjectRuleException("Unknown study.");
        var form = study.Forms.First(f => f.Id == instance.FormDefinitionId);
        var visit = subject.Visits.First(v => v.Id == instance.VisitId);
        var messages = new List<string>();

        if (instance.Status == FormStatus.Locked || study.Status == StudyStatus.Locked)
        {
            throw new SubjectRuleException("The form is locked.");
        }

        // Normalise: only fields of the form, blanks as null, decimal comma accepted.
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in form.Fields.Where(f => f.Type != FieldType.Derived))
        {
            request.Values.TryGetValue(field.Code, out var raw);
            values[field.Code] = Normalise(field, raw);
        }

        var changed = form.Fields.Where(f => f.Type != FieldType.Derived)
            .Where(f => !string.Equals(Current(instance, f.Code), values[f.Code], StringComparison.Ordinal))
            .ToList();
        var editing = changed.Any(f => Current(instance, f.Code) is not null);
        if (editing && instance.Status >= FormStatus.Complete && string.IsNullOrWhiteSpace(request.Reason))
        {
            return Refused(new CheckResult("SYS.REASON", "", EditCheckSeverity.Hard, "A reason for change is required."));
        }

        var snapshot = new SubjectSnapshot(study, subject, visit);
        var derived = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var hard = form.Fields.SelectMany(f => f.Type == FieldType.Derived ? Array.Empty<CheckResult>() : EditCheckRunner.FieldChecks(f, values[f.Code]))
            .Where(r => r.Severity == EditCheckSeverity.Hard)
            .ToList();
        if (hard.Count > 0)
        {
            return Refused(hard.ToArray());
        }

        string? derivedWith = instance.DerivedWith;
        if (form.WorkbookCode is not null && form.Fields.Any(f => f.Type == FieldType.Derived))
        {
            var workbook = study.Workbooks.Where(w => w.Code == form.WorkbookCode && w.Active).OrderByDescending(w => w.Version).FirstOrDefault()
                ?? throw new DerivationException($"No active workbook {form.WorkbookCode} for {study.Code}.");
            var resolver = snapshot.ResolverFor(form, values);
            var evaluator = new Evaluator(resolver, DateOnly.FromDateTime(_clock.UtcNow));
            var inputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var input in workbook.Inputs)
            {
                try
                {
                    inputs[input.Name] = evaluator.Evaluate(EditCheckRunner.Compile(input.Source));
                }
                catch (ExpressionException ex)
                {
                    messages.Add($"Workbook input {input.Name}: {ex.Message}");
                    inputs[input.Name] = null;
                }
            }

            var outputs = form.Fields.Where(f => f.Type == FieldType.Derived && f.WorkbookOutput is not null).Select(f => f.WorkbookOutput!).ToList();
            var result = await _derivation.DeriveAsync(new DerivationRequest(study, workbook, inputs, outputs), ct);
            messages.AddRange(result.Messages);
            foreach (var field in form.Fields.Where(f => f.Type == FieldType.Derived && f.WorkbookOutput is not null))
            {
                result.Values.TryGetValue(field.WorkbookOutput!, out var v);
                derived[field.Code] = v is null ? null : Round(v, field.Decimals);
                values[field.Code] = derived[field.Code];
            }

            derivedWith = result.WorkbookVersion;
        }

        var checks = EditCheckRunner.Run(form, snapshot.ResolverFor(form, values), values, DateOnly.FromDateTime(_clock.UtcNow));
        hard = checks.Where(c => c.Severity == EditCheckSeverity.Hard).ToList();
        if (hard.Count > 0)
        {
            return Refused(hard.ToArray());
        }

        var now = _clock.UtcNow;
        var key = $"{subject.Number}/{visit.Definition!.Code}/{form.Code}" + (form.Repeating ? $"#{instance.Row}" : "");
        foreach (var field in form.Fields)
        {
            var newValue = values.GetValueOrDefault(field.Code);
            var existing = instance.Values.FirstOrDefault(v => v.FieldCode == field.Code);
            if (string.Equals(existing?.Value, newValue, StringComparison.Ordinal))
            {
                continue;
            }

            if (existing is null)
            {
                if (newValue is null)
                {
                    continue;
                }

                existing = new FieldValue { FieldCode = field.Code };
                instance.Values.Add(existing);
            }

            _audit.Add(request.User, "Field", $"{key}.{field.Code}", subject.Id,
                field.Type == FieldType.Derived ? "Derived" : existing.Value is null ? "Entered" : "Changed",
                existing.Value, newValue, field.Type == FieldType.Derived ? derivedWith : request.Reason);
            existing.Value = newValue;
            if (existing.Verified)
            {
                existing.Verified = false;
                existing.VerifiedBy = null;
                existing.VerifiedAt = null;
                _audit.Add(request.User, "Sdv", $"{key}.{field.Code}", subject.Id, "SdvReset", reason: "Value changed");
            }

            if (field.CodingDictionary is not null)
            {
                await UpsertCodingAsync(subject.Id, instance.Id, field, newValue, ct);
            }
        }

        if (instance.SignedAt is not null && changed.Count > 0)
        {
            _audit.Add(request.User, "Signature", key, subject.Id, "SignatureVoided", instance.SignedBy, null, "Data changed after signature");
            instance.SignedAt = null;
            instance.SignedBy = null;
        }

        var queryResults = checks.Where(c => c.Severity == EditCheckSeverity.Query).ToList();
        await SyncAutoQueriesAsync(subject, instance, queryResults, now, ct);

        var requiredMissing = form.Fields.Any(f => f.Required && f.Type != FieldType.Derived && values[f.Code] is null);
        var newStatus = requiredMissing ? FormStatus.InProgress : FormStatus.Complete;
        if (instance.Status != newStatus && instance.Status < FormStatus.Signed || instance.Status == FormStatus.Signed && changed.Count > 0)
        {
            instance.Status = newStatus;
        }

        instance.LastSavedAt = now;
        instance.LastSavedBy = request.User;
        instance.DerivedWith = derivedWith;
        instance.RowVersion = SubjectService.NewVersion();

        if (visit.Status == VisitStatus.Planned)
        {
            visit.Status = VisitStatus.Started;
        }

        var visitForms = visit.Forms.Where(f => f.Id != instance.Id).Select(f => f.Status).Append(instance.Status);
        if (visitForms.All(s => s >= FormStatus.Complete) && visit.Date is not null && !(visit.Definition.Unscheduled))
        {
            visit.Status = VisitStatus.Completed;
        }

        if (changed.Count > 0)
        {
            _db.Notifications.Add(new Notification
            {
                At = now, StudyId = study.Id, SiteId = subject.SiteId, SubjectId = subject.Id, Kind = "FormSaved",
                Text = $"{key} saved by {request.User} ({changed.Count} field(s))"
            });
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new FormConflictException();
        }

        return new SaveFormResult(true, instance, Array.Empty<CheckResult>(), queryResults,
            checks.Where(c => c.Severity == EditCheckSeverity.Warning).ToList(), derived, messages);

        SaveFormResult Refused(params CheckResult[] errors) =>
            new(false, instance, errors, Array.Empty<CheckResult>(), Array.Empty<CheckResult>(), new Dictionary<string, string?>(), messages);
    }

    private async Task SyncAutoQueriesAsync(Subject subject, FormInstance instance, List<CheckResult> failing, DateTime now, CancellationToken ct)
    {
        var open = await _db.Queries.Include(q => q.Messages)
            .Where(q => q.FormInstanceId == instance.Id && q.Origin == QueryOrigin.EditCheck
                        && (q.State == QueryState.Open || q.State == QueryState.Answered))
            .ToListAsync(ct);

        foreach (var q in open.Where(q => failing.All(f => f.Code != q.EditCheckCode)))
        {
            QueryWorkflow.AutoClose(q, now);
            _audit.Add("system", "Query", q.Id.ToString(CultureInfo.InvariantCulture), subject.Id, "AutoClosed", reason: q.EditCheckCode);
        }

        foreach (var f in failing.Where(f => open.All(q => q.EditCheckCode != f.Code)))
        {
            var q = QueryWorkflow.Open(subject.Id, instance.Id, f.FieldCode, QueryOrigin.EditCheck, f.Message, "system", now, f.Code);
            _db.Queries.Add(q);
            _audit.Add("system", "Query", f.Code, subject.Id, "Opened", newValue: f.Message);
            _db.Notifications.Add(new Notification
            {
                At = now, StudyId = subject.StudyId, SiteId = subject.SiteId, SubjectId = subject.Id, Kind = "QueryOpened",
                Text = $"{subject.Number}: {f.Message}"
            });
        }
    }

    private async Task UpsertCodingAsync(int subjectId, int formInstanceId, FieldDefinition field, string? verbatim, CancellationToken ct)
    {
        var item = await _db.Coding.FirstOrDefaultAsync(c => c.FormInstanceId == formInstanceId && c.FieldCode == field.Code, ct);
        if (verbatim is null)
        {
            if (item is not null)
            {
                _db.Coding.Remove(item);
            }

            return;
        }

        if (item is null)
        {
            item = new CodingItem { SubjectId = subjectId, FormInstanceId = formInstanceId, FieldCode = field.Code, Dictionary = field.CodingDictionary! };
            _db.Coding.Add(item);
        }

        item.Verbatim = verbatim;
        item.State = CodingState.Pending;
        item.Code = null;
        item.Term = null;
        item.Hierarchy = null;
        item.Score = null;
        item.CodedBy = null;
        item.CodedAt = null;
    }

    private static string? Current(FormInstance instance, string fieldCode) =>
        instance.Values.FirstOrDefault(v => v.FieldCode == fieldCode)?.Value;

    public static string? Normalise(FieldDefinition field, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        raw = raw.Trim();
        switch (field.Type)
        {
            case FieldType.Integer:
            case FieldType.Decimal:
                var n = raw.Replace(',', '.');
                return decimal.TryParse(n, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
                    ? d.ToString(CultureInfo.InvariantCulture)
                    : raw;
            case FieldType.Date:
                string[] formats = { "yyyy-MM-dd", "d.M.yyyy", "dd.MM.yyyy", "dd-MMM-yyyy" };
                return DateOnly.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : raw;
            case FieldType.Boolean:
                return raw is "1" or "true" or "on" or "Y" ? "1" : raw is "0" or "false" or "N" ? "0" : raw;
            default:
                return raw;
        }
    }

    private static string? Round(string value, int? decimals)
    {
        if (decimals is null || !decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var d))
        {
            return value;
        }

        return decimal.Round(d, decimals.Value, MidpointRounding.AwayFromZero).ToString("F" + decimals.Value, CultureInfo.InvariantCulture);
    }
}
