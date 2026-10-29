using System.Globalization;
using StudyData.Core.Design;
using StudyData.Core.Queries;
using StudyData.Core.Subjects;

namespace StudyData.Documents;

public record CasebookField(string Code, string Label, string? Value, string? Unit, bool Verified, bool Derived, bool HasQuery, string? AuditNote);

public record CasebookForm(string VisitCode, string VisitName, string? VisitDate, string FormCode, string FormName, int Row,
    string Status, string? SignedBy, string? SignedAt, IReadOnlyList<CasebookField> Fields);

public record CasebookAudit(string At, string User, string What, string? Old, string? New, string? Reason);

public record CasebookQuery(int Id, string Field, string State, string Text, string OpenedAt);

public record CasebookModel(
    string StudyCode,
    string StudyTitle,
    string Sponsor,
    string SubjectNumber,
    string Site,
    string Investigator,
    string Status,
    string? RandomisationNumber,
    string GeneratedAt,
    string GeneratedBy,
    IReadOnlyList<CasebookForm> Forms,
    IReadOnlyList<CasebookAudit> Audit,
    IReadOnlyList<CasebookQuery> Queries)
{
    public static CasebookModel Build(Study study, Subject subject, IEnumerable<AuditEntry> audit, IEnumerable<Query> queries,
        string generatedBy, DateTime generatedAt, bool includeAudit = true)
    {
        var queryList = queries.ToList();
        var forms = new List<CasebookForm>();
        foreach (var visit in subject.Visits.OrderBy(v => v.Definition!.Order).ThenBy(v => v.Repeat))
        {
            foreach (var instance in visit.Forms.Where(f => f.Status != FormStatus.NotStarted)
                         .OrderBy(f => study.Forms.First(d => d.Id == f.FormDefinitionId).Order).ThenBy(f => f.Row))
            {
                var def = study.Forms.First(d => d.Id == instance.FormDefinitionId);
                var fields = def.Fields.OrderBy(f => f.Order).Select(f =>
                {
                    var value = instance.Values.FirstOrDefault(v => v.FieldCode == f.Code);
                    return new CasebookField(f.Code, f.Label, Display(f, value?.Value), f.Unit, value?.Verified == true,
                        f.Type == FieldType.Derived,
                        queryList.Any(q => q.FormInstanceId == instance.Id && q.FieldCode == f.Code && QueryWorkflow.BlocksSignature(q)),
                        null);
                }).ToList();

                forms.Add(new CasebookForm(visit.Definition!.Code, visit.Definition.Name, visit.Date?.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture),
                    def.Code, def.Name, instance.Row, instance.Status.ToString(), instance.SignedBy,
                    instance.SignedAt?.ToString("dd-MMM-yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture), fields));
            }
        }

        var site = subject.Site!;
        return new CasebookModel(study.Code, study.Title, study.Sponsor, subject.Number, $"{site.Number} {site.Name}", site.Investigator,
            subject.Status.ToString(), subject.RandomisationNumber,
            generatedAt.ToString("dd-MMM-yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture), generatedBy, forms,
            includeAudit
                ? audit.OrderBy(a => a.At).Select(a => new CasebookAudit(a.At.ToString("dd-MMM-yyyy HH:mm:ss", CultureInfo.InvariantCulture), a.User,
                    $"{a.Action} {a.EntityKey}", a.OldValue, a.NewValue, a.Reason)).ToList()
                : Array.Empty<CasebookAudit>(),
            queryList.Select(q => new CasebookQuery(q.Id, q.FieldCode, q.State.ToString(), q.Text,
                q.OpenedAt.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture))).ToList());
    }

    private static string? Display(FieldDefinition f, string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        switch (f.Type)
        {
            case FieldType.Choice:
                var choice = f.ChoiceList().FirstOrDefault(c => c.Key == raw);
                return choice.Value ?? raw;
            case FieldType.Boolean:
                return raw == "1" ? "Yes" : "No";
            case FieldType.Date:
                return DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                    ? d.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture)
                    : raw;
            default:
                return raw;
        }
    }
}

public record RenderedPage(int Number, byte[] Content, string ContentType);

public interface ICasebookRenderer
{
    /// <summary>Page images for the on-screen viewer.</summary>
    Task<IReadOnlyList<RenderedPage>> RenderPagesAsync(CasebookModel model, double dpi, CancellationToken ct = default);

    /// <summary>Writes the archive copy of the casebook and returns its path (PDF in production).</summary>
    Task<string> WriteArchiveAsync(CasebookModel model, string outputPathWithoutExtension, CancellationToken ct = default);
}

public class DocumentOptions
{
    public const string Section = "Documents";

    /// <summary>Wpf (production) or Development (SVG page images and HTML archive).</summary>
    public string Provider { get; set; } = "Wpf";

    /// <summary>Printer that prints XPS to PDF; its port is the file named in <see cref="PrinterPortFile"/>.</summary>
    public string PdfPrinter { get; set; } = "StudyData PDF";

    public string PrinterPortFile { get; set; } = @"C:\StudyData\Spool\studydata.pdf";

    public string SpoolFolder { get; set; } = @"C:\StudyData\Spool";

    public TimeSpan PrintTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Rows of fields per page before a form continues on the next page.</summary>
    public int RowsPerPage { get; set; } = 24;

    /// <summary>Organisation shown in the page footer.</summary>
    public string Organisation { get; set; } = "Fabrikam Clinical Research";
}
