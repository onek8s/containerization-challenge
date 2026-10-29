namespace StudyData.Documents;

/// <summary>Page layout shared by the renderers: what goes on which page.</summary>
public static class CasebookPages
{
    public abstract record Page(int Number);

    public sealed record Cover(int Number, CasebookModel Model) : Page(Number);

    public sealed record FormPage(int Number, CasebookModel Model, CasebookForm Form, IReadOnlyList<CasebookField> Rows, bool Continued) : Page(Number);

    public sealed record AuditPage(int Number, CasebookModel Model, string Title, IReadOnlyList<(string At, string User, string Text)> Rows) : Page(Number);

    public static IReadOnlyList<Page> Layout(CasebookModel model, int rowsPerPage)
    {
        var pages = new List<Page> { new Cover(1, model) };
        foreach (var form in model.Forms)
        {
            var chunks = form.Fields.Chunk(rowsPerPage).ToList();
            for (var i = 0; i < chunks.Count; i++)
            {
                pages.Add(new FormPage(pages.Count + 1, model, form, chunks[i], i > 0));
            }
        }

        if (model.Queries.Count > 0)
        {
            foreach (var chunk in model.Queries.Select(q => (q.OpenedAt, $"#{q.Id}", $"{q.Field} [{q.State}] {q.Text}")).Chunk(40))
            {
                pages.Add(new AuditPage(pages.Count + 1, model, "Queries", chunk));
            }
        }

        foreach (var chunk in model.Audit.Select(a => (a.At, a.User, AuditText(a))).Chunk(45))
        {
            pages.Add(new AuditPage(pages.Count + 1, model, "Audit trail", chunk));
        }

        return pages;
    }

    public static string Marks(CasebookField f) =>
        (f.Verified ? "✓" : "") + (f.Derived ? "ƒ" : "") + (f.HasQuery ? "?" : "");

    private static string AuditText(CasebookAudit a)
    {
        var text = a.What;
        if (a.Old is not null || a.New is not null)
        {
            text += $": {a.Old ?? "(blank)"} → {a.New ?? "(blank)"}";
        }

        if (!string.IsNullOrWhiteSpace(a.Reason))
        {
            text += $" ({a.Reason})";
        }

        return text;
    }
}
