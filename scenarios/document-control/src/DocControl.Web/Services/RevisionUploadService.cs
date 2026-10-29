using Litware.DocControl.Data;
using Litware.DocControl.Data.Storage;
using Litware.DocControl.Documents;

namespace Litware.DocControl.Web.Services;

public sealed record UploadResult(bool Success, string? Error, int? RevisionId = null, string? DocumentNumber = null);

public sealed class RevisionUploadService
{
    private readonly DocumentRepository _documents;
    private readonly RevisionRepository _revisions;
    private readonly VaultStore _vault;
    private readonly CurrentUser _user;
    private readonly ILogger<RevisionUploadService> _log;

    public RevisionUploadService(DocumentRepository documents, RevisionRepository revisions, VaultStore vault, CurrentUser user,
        ILogger<RevisionUploadService> log)
    {
        _documents = documents;
        _revisions = revisions;
        _vault = vault;
        _user = user;
        _log = log;
    }

    public async Task<UploadResult> UploadAsync(string documentNumber, string revision, string? title, IFormFile file, CancellationToken ct)
    {
        if (!DocumentNumber.TryParse(documentNumber, out var number))
            return new(false, $"'{documentNumber}' is not a valid document number.");
        if (!RevisionCode.TryParse(revision, out var code))
            return new(false, $"'{revision}' is not a valid revision code.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!RevisionFileName.AllowedExtensions.Contains(ext))
            return new(false, $"File type '{ext}' is not accepted.");

        var project = await _documents.ProjectAsync(number.Project);
        if (project is null || !project.Active)
            return new(false, $"Project {number.Project} is not open for new documents.");

        var doc = await _documents.FindAsync(number.ToString());
        if (doc is null && string.IsNullOrWhiteSpace(title))
            return new(false, "Enter a title for the new document.");

        var history = doc is null ? [] : await _revisions.HistoryAsync(doc.Id);
        var problem = RevisionRules.CheckNewRevision(code, history);
        if (problem is not null) return new(false, problem);

        var fileName = new RevisionFileName(number, code, ext);
        StoredFile stored;
        await using (var stream = file.OpenReadStream())
            stored = await _vault.SaveAsync(fileName, stream, ct);

        if (ext == ".pdf")
        {
            await using var check = _vault.OpenRead(stored.RelativePath);
            var pdfTitle = PdfTitleReader.ReadTitle(check);
            if (pdfTitle is not null && !pdfTitle.Contains(number.ToString(), StringComparison.OrdinalIgnoreCase))
                _log.LogWarning("PDF title '{Title}' of {File} does not contain the document number", pdfTitle, fileName);
        }

        var docId = doc?.Id ?? await _documents.CreateAsync(number, title!.Trim(), _user.Name);
        if (doc is not null && !string.IsNullOrWhiteSpace(title) && title.Trim() != doc.Title)
            await _documents.UpdateTitleAsync(doc.Id, title.Trim());

        if (await _revisions.HashExistsAsync(docId, stored.Sha256))
            _log.LogWarning("{File} has the same content as an earlier revision", fileName);

        var id = await _revisions.AddAsync(new RevisionRecord
        {
            DocumentId = docId,
            Code = code.ToString(),
            Status = RevisionStatus.Submitted,
            FileName = fileName.ToString(),
            VaultPath = stored.RelativePath,
            Size = stored.Size,
            Sha256 = stored.Sha256,
            Source = "Portal",
            SubmittedBy = _user.Name,
            SubmittedUtc = DateTime.UtcNow,
        });

        _log.LogInformation("{User} submitted {File} ({Size} bytes)", _user.Name, fileName, stored.Size);
        return new(true, null, id, number.ToString());
    }
}
