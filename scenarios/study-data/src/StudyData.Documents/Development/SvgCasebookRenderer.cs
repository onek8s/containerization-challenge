using System.IO;
using System.Net;
using System.Text;

namespace StudyData.Documents.Development;

/// <summary>Development renderer: SVG page images and an HTML archive copy, without WPF or a printer.</summary>
public sealed class SvgCasebookRenderer : ICasebookRenderer
{
    private readonly int _rowsPerPage;

    public SvgCasebookRenderer(int rowsPerPage = 24)
    {
        _rowsPerPage = rowsPerPage;
    }

    public Task<IReadOnlyList<RenderedPage>> RenderPagesAsync(CasebookModel model, double dpi, CancellationToken ct = default)
    {
        var layout = CasebookPages.Layout(model, _rowsPerPage);
        IReadOnlyList<RenderedPage> pages = layout
            .Select(p => new RenderedPage(p.Number, Encoding.UTF8.GetBytes(Svg(p, model, layout.Count)), "image/svg+xml"))
            .ToList();
        return Task.FromResult(pages);
    }

    public async Task<string> WriteArchiveAsync(CasebookModel model, string outputPathWithoutExtension, CancellationToken ct = default)
    {
        var path = outputPathWithoutExtension + ".html";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var pages = await RenderPagesAsync(model, 96, ct);
        var html = new StringBuilder("<!doctype html><meta charset=\"utf-8\"><title>")
            .Append(E($"{model.StudyCode} {model.SubjectNumber}")).Append("</title><body style=\"background:#ddd\">");
        foreach (var p in pages)
        {
            html.Append("<div style=\"margin:12px auto;width:794px;background:#fff\">").Append(Encoding.UTF8.GetString(p.Content)).Append("</div>");
        }

        await File.WriteAllTextAsync(path, html.Append("</body>").ToString(), ct);
        return path;
    }

    private static string Svg(CasebookPages.Page page, CasebookModel model, int total)
    {
        var sb = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"794\" height=\"1123\" viewBox=\"0 0 794 1123\" font-family=\"Segoe UI, Arial, sans-serif\">");
        sb.Append("<rect width=\"794\" height=\"1123\" fill=\"#fff\"/>");
        sb.Append($"<text x=\"48\" y=\"58\" font-size=\"15\" font-weight=\"600\" fill=\"#1F4E79\">{E(model.StudyCode)}</text>");
        sb.Append($"<text x=\"746\" y=\"58\" font-size=\"12\" text-anchor=\"end\">Subject {E(model.SubjectNumber)}</text>");
        sb.Append("<line x1=\"48\" y1=\"70\" x2=\"746\" y2=\"70\" stroke=\"#1F4E79\" stroke-width=\"2\"/>");
        var y = 104;
        switch (page)
        {
            case CasebookPages.Cover:
                sb.Append($"<text x=\"72\" y=\"200\" font-size=\"30\" fill=\"#1F4E79\">Subject Casebook</text>");
                y = 250;
                foreach (var (k, v) in new[] { ("Study", model.StudyCode), ("Site", model.Site), ("Investigator", model.Investigator), ("Status", model.Status), ("Randomisation", model.RandomisationNumber ?? "not randomised"), ("Generated", model.GeneratedAt) })
                {
                    sb.Append($"<text x=\"72\" y=\"{y}\" font-size=\"12\" fill=\"#606060\">{E(k)}</text><text x=\"240\" y=\"{y}\" font-size=\"12\">{E(v)}</text>");
                    y += 26;
                }

                break;
            case CasebookPages.FormPage f:
                sb.Append($"<text x=\"48\" y=\"{y}\" font-size=\"14\" font-weight=\"600\">{E(f.Form.FormName)}{(f.Form.FormCode is "AE" or "CM" ? $" #{f.Form.Row}" : "")}</text>");
                sb.Append($"<text x=\"48\" y=\"{y + 16}\" font-size=\"10\" fill=\"#606060\">{E(f.Form.VisitName)} ({E(f.Form.VisitCode)}) {E(f.Form.VisitDate ?? "")} · {E(f.Form.Status)}</text>");
                y += 44;
                var i = 0;
                foreach (var r in f.Rows)
                {
                    sb.Append($"<rect x=\"48\" y=\"{y - 15}\" width=\"698\" height=\"24\" fill=\"{(i++ % 2 == 0 ? "#fff" : "#F7F9FB")}\"/>");
                    sb.Append($"<text x=\"56\" y=\"{y}\" font-size=\"9\" font-family=\"Consolas, monospace\" fill=\"#707070\">{E(r.Code)}</text>");
                    sb.Append($"<text x=\"136\" y=\"{y}\" font-size=\"10.5\">{E(r.Label + (r.Unit is null ? "" : $" ({r.Unit})"))}</text>");
                    sb.Append($"<text x=\"420\" y=\"{y}\" font-size=\"10.5\" font-weight=\"600\">{E(r.Value ?? "—")}</text>");
                    sb.Append($"<text x=\"740\" y=\"{y}\" font-size=\"9\" text-anchor=\"end\" fill=\"#9C2B23\">{E(CasebookPages.Marks(r))}</text>");
                    y += 24;
                }

                sb.Append($"<text x=\"48\" y=\"1060\" font-size=\"9\">{E(f.Form.SignedBy is null ? "Not signed" : $"Electronically signed by {f.Form.SignedBy} on {f.Form.SignedAt}")}</text>");
                break;
            case CasebookPages.AuditPage a:
                sb.Append($"<text x=\"48\" y=\"{y}\" font-size=\"14\" font-weight=\"600\" fill=\"#1F4E79\">{E(a.Title)}</text>");
                y += 28;
                foreach (var r in a.Rows)
                {
                    sb.Append($"<text x=\"48\" y=\"{y}\" font-size=\"8.5\" font-family=\"Consolas, monospace\">{E(r.At)}</text>");
                    sb.Append($"<text x=\"160\" y=\"{y}\" font-size=\"8.5\">{E(Trim(r.User, 26))}</text>");
                    sb.Append($"<text x=\"300\" y=\"{y}\" font-size=\"8.5\">{E(Trim(r.Text, 90))}</text>");
                    y += 20;
                }

                break;
        }

        sb.Append($"<text x=\"746\" y=\"1090\" font-size=\"9\" text-anchor=\"end\">Page {page.Number} of {total}</text></svg>");
        return sb.ToString();
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    private static string E(string s) => WebUtility.HtmlEncode(s);
}
