using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudyData.Core.Documents;
using StudyData.Core.Services;
using StudyData.Data;
using StudyData.Data.Services;
using StudyData.Edc.Web.Services;

namespace StudyData.Edc.Web.Controllers;

[Authorize]
[Route("documents")]
public class DocumentsController : Controller
{
    private readonly StudyDataContext _db;
    private readonly SiteAccess _access;
    private readonly SourceDocumentVault _vault;
    private readonly AuditWriter _audit;
    private readonly IClock _clock;
    private readonly EdcOptions _options;

    public DocumentsController(StudyDataContext db, SiteAccess access, SourceDocumentVault vault, AuditWriter audit, IClock clock, IOptions<EdcOptions> options)
    {
        _db = db;
        _access = access;
        _vault = vault;
        _audit = audit;
        _clock = clock;
        _options = options.Value;
    }

    [HttpPost("subject/{subjectId:int}")]
    [RequestSizeLimit(41943040)]
    public async Task<IActionResult> Upload(int subjectId, IFormFile file, string category, int? visitId, bool redacted, CancellationToken ct)
    {
        var subject = await _access.SubjectAsync(subjectId);
        if (subject is null)
        {
            return NotFound();
        }

        if (file is null || file.Length == 0 || !SourceDocumentVault.IsAllowed(file.FileName) || file.Length > _options.MaxUploadMegabytes * 1024L * 1024L)
        {
            TempData["Error"] = $"Upload a PDF or image of at most {_options.MaxUploadMegabytes} MB.";
            return RedirectToAction("Details", "Subjects", new { id = subjectId });
        }

        if (!redacted)
        {
            TempData["Error"] = "Confirm that the document has been redacted (no name, personal identity code or address).";
            return RedirectToAction("Details", "Subjects", new { id = subjectId });
        }

        var study = await _db.Studies.FirstAsync(s => s.Id == subject.StudyId, ct);
        await using var stream = file.OpenReadStream();
        var (path, hash, length) = await _vault.StoreAsync(study.Code, subject.Site!.Number, subject.Number, file.FileName, stream, ct);
        var doc = new SourceDocument
        {
            SubjectId = subjectId, VisitId = visitId, Category = category, OriginalName = Path.GetFileName(file.FileName), StoredPath = path,
            ContentType = file.ContentType, Length = length, Sha256 = hash, Redacted = true, UploadedAt = _clock.UtcNow, UploadedBy = _access.UserName
        };
        _db.SourceDocuments.Add(doc);
        _audit.Add(_access.UserName, "SourceDocument", doc.OriginalName, subjectId, "Uploaded", newValue: hash, reason: category);
        await _db.SaveChangesAsync(ct);
        TempData["Message"] = $"{doc.OriginalName} uploaded.";
        return RedirectToAction("Details", "Subjects", new { id = subjectId });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Download(int id)
    {
        var doc = await _db.SourceDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null || await _access.SubjectAsync(doc.SubjectId) is null)
        {
            return NotFound();
        }

        return File(_vault.Open(doc.StoredPath), doc.ContentType, doc.OriginalName);
    }
}
