using StudyData.Core.Design;
using StudyData.Core.EditChecks;
using StudyData.Core.Subjects;

namespace StudyData.Data.Services;

/// <summary>
/// Values of a subject's casebook for cross-form references (FORM.FIELD) in edit checks and workbook inputs.
/// A reference resolves to the same visit first, then to the latest earlier visit, then to any visit.
/// VISIT.FIELD (for example SCR.VSHEIGHT) reads the field from that visit only.
/// </summary>
public sealed class SubjectSnapshot
{
    private readonly Study _study;
    private readonly Subject _subject;
    private readonly Visit _current;

    public SubjectSnapshot(Study study, Subject subject, Visit current)
    {
        _study = study;
        _subject = subject;
        _current = current;
    }

    public (FieldType? Type, string? Value) Lookup(string formCode, string fieldCode)
    {
        var visitDef = _study.Visits.FirstOrDefault(v => string.Equals(v.Code, formCode, StringComparison.OrdinalIgnoreCase));
        if (visitDef is not null)
        {
            return LookupInVisit(visitDef.Id, fieldCode);
        }

        var form = _study.Forms.FirstOrDefault(f => string.Equals(f.Code, formCode, StringComparison.OrdinalIgnoreCase));
        var field = form?.Fields.FirstOrDefault(f => string.Equals(f.Code, fieldCode, StringComparison.OrdinalIgnoreCase));
        if (form is null || field is null)
        {
            return (null, null);
        }

        var currentOrder = _current.Definition?.Order ?? int.MaxValue;
        var candidates = _subject.Visits
            .SelectMany(v => v.Forms.Where(f => f.FormDefinitionId == form.Id).Select(f => (Visit: v, Form: f)))
            .Select(x => (x.Visit, Value: x.Form.Values.FirstOrDefault(v => v.FieldCode == field.Code)?.Value))
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .ToList();

        var same = candidates.FirstOrDefault(x => x.Visit.Id == _current.Id);
        if (same.Value is not null)
        {
            return (field.Type, same.Value);
        }

        var earlier = candidates
            .Where(x => (x.Visit.Definition?.Order ?? 0) <= currentOrder)
            .OrderByDescending(x => x.Visit.Date ?? DateOnly.MinValue)
            .ThenByDescending(x => x.Visit.Definition?.Order ?? 0)
            .FirstOrDefault();
        if (earlier.Value is not null)
        {
            return (field.Type, earlier.Value);
        }

        return (field.Type, candidates.OrderByDescending(x => x.Visit.Date ?? DateOnly.MinValue).Select(x => x.Value).FirstOrDefault());
    }

    private (FieldType? Type, string? Value) LookupInVisit(int visitDefinitionId, string fieldCode)
    {
        var form = _study.Forms.FirstOrDefault(f => f.Fields.Any(x => string.Equals(x.Code, fieldCode, StringComparison.OrdinalIgnoreCase)));
        var field = form?.Fields.First(x => string.Equals(x.Code, fieldCode, StringComparison.OrdinalIgnoreCase));
        if (form is null || field is null)
        {
            return (null, null);
        }

        var value = _subject.Visits
            .Where(v => v.VisitDefinitionId == visitDefinitionId)
            .SelectMany(v => v.Forms.Where(f => f.FormDefinitionId == form.Id))
            .SelectMany(f => f.Values.Where(v => v.FieldCode == field.Code))
            .Select(v => v.Value)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        return (field.Type, value);
    }

    public FormFieldResolver ResolverFor(FormDefinition form, IReadOnlyDictionary<string, string?> values) =>
        new(form, values, Lookup);
}
