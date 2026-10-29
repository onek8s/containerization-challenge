using Litware.DocControl.Data;
using Litware.DocControl.Data.Storage;
using Litware.DocControl.Documents;
using Litware.DocControl.Web.Models;
using Litware.DocControl.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace Litware.DocControl.Web.Controllers;

public sealed class RegisterController : Controller
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    private readonly DocumentRepository _documents;
    private readonly RevisionRepository _revisions;
    private readonly VaultStore _vault;
    private readonly PortalOptions _options;

    public RegisterController(DocumentRepository documents, RevisionRepository revisions, VaultStore vault, IOptions<PortalOptions> options)
    {
        _documents = documents;
        _revisions = revisions;
        _vault = vault;
        _options = options.Value;
    }

    public async Task<IActionResult> Index(string? project, string? discipline, string? q)
    {
        var projects = await _documents.ProjectsAsync(activeOnly: false);
        project ??= _options.DefaultProject ?? projects.FirstOrDefault()?.Code ?? "";
        return View(new RegisterViewModel
        {
            Projects = projects,
            Project = project,
            Discipline = discipline,
            Search = q,
            Documents = await _documents.RegisterAsync(project, discipline, q),
        });
    }

    [HttpGet("register/{number}")]
    public async Task<IActionResult> Document(string number)
    {
        var doc = await _documents.FindAsync(number);
        if (doc is null) return NotFound();
        var revisions = await _revisions.ForDocumentAsync(doc.Id);
        var history = revisions.Select(r => (RevisionCode.Parse(r.Code), r.Status));
        return View(new DocumentViewModel
        {
            Document = doc,
            Revisions = revisions,
            SuggestedRevision = RevisionRules.SuggestNext(history).ToString(),
        });
    }

    [HttpGet("register/file/{id:int}")]
    public async Task<IActionResult> File(int id)
    {
        var rev = await _revisions.GetAsync(id);
        if (rev is null || !_vault.Exists(rev.VaultPath)) return NotFound();
        if (!ContentTypes.TryGetContentType(rev.FileName, out var type)) type = "application/octet-stream";
        return File(_vault.OpenRead(rev.VaultPath), type, rev.FileName, enableRangeProcessing: true);
    }

    [HttpGet("register/export")]
    public async Task<IActionResult> Export(string project)
    {
        var docs = await _documents.RegisterAsync(project);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Number;Title;Discipline;Type;Latest revision;Status;Approved revision");
        foreach (var d in docs)
            sb.AppendLine($"{d.Number};\"{d.Title.Replace("\"", "\"\"")}\";{d.Discipline};{d.DocumentType};{d.LatestRevision};{d.LatestStatus};{d.ApprovedRevision}");
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"{project}-register-{DateTime.Today:yyyyMMdd}.csv");
    }
}
