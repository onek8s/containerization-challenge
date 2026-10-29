using Litware.DocControl.Data;
using Litware.DocControl.Data.Storage;
using Litware.DocControl.Documents;
using Litware.DocControl.Platform;
using Microsoft.Extensions.Options;

namespace Litware.DocControl.Indexer;

public enum DropOutcome { Accepted, Rejected, Skipped }

/// <summary>Validates one file from a project drop folder and registers it as a submitted revision.</summary>
public sealed class DropFileProcessor
{
    private readonly DocumentRepository _documents;
    private readonly RevisionRepository _revisions;
    private readonly DropFileRepository _dropFiles;
    private readonly VaultStore _vault;
    private readonly IndexerOptions _options;
    private readonly ILogger<DropFileProcessor> _log;
    private readonly string _account = $@"{Environment.UserDomainName}\{Environment.UserName}";

    public DropFileProcessor(DocumentRepository documents, RevisionRepository revisions, DropFileRepository dropFiles, VaultStore vault,
        IOptions<IndexerOptions> options, ILogger<DropFileProcessor> log)
    {
        _documents = documents;
        _revisions = revisions;
        _dropFiles = dropFiles;
        _vault = vault;
        _options = options.Value;
        _log = log;
    }

    public async Task<DropOutcome> ProcessAsync(string projectFolder, ScannedFile file, CancellationToken ct)
    {
        var fileId = FileOps.GetFileId(file.FullPath);
        var sha = VaultStore.ComputeSha256(file.FullPath);

        var earlier = await _dropFiles.FindProcessedAsync(fileId, sha);
        if (earlier is { Result: "Rejected" })
        {
            // Moved back from _rejected without changes
            Reject(projectFolder, file, earlier.Reason ?? "Rejected earlier.");
            await Record(fileId, sha, file, "Rejected", earlier.Reason, null);
            return DropOutcome.Rejected;
        }

        var (reason, parsed, doc) = await ValidateAsync(projectFolder, file);
        if (reason is not null)
        {
            Reject(projectFolder, file, reason);
            await Record(fileId, sha, file, "Rejected", reason, null);
            return DropOutcome.Rejected;
        }

        if (await _revisions.HashExistsAsync(doc!.Id, sha))
        {
            const string duplicate = "The same file was already submitted as an earlier revision.";
            Reject(projectFolder, file, duplicate);
            await Record(fileId, sha, file, "Rejected", duplicate, null);
            return DropOutcome.Rejected;
        }

        var stored = _vault.Import(parsed!, file.FullPath, sha, cancellationToken: ct);
        var revisionId = await _revisions.AddAsync(new RevisionRecord
        {
            DocumentId = doc.Id,
            Code = parsed!.Revision.ToString(),
            Status = RevisionStatus.Submitted,
            FileName = parsed.ToString(),
            VaultPath = stored.RelativePath,
            Size = stored.Size,
            Sha256 = sha,
            Source = "Drop",
            SubmittedBy = _account,
            SubmittedUtc = DateTime.UtcNow,
        });

        var processed = Path.Combine(projectFolder, _options.ProcessedFolder, DateTime.Today.ToString("yyyyMMdd"));
        Directory.CreateDirectory(processed);
        File.Move(file.FullPath, UniquePath(Path.Combine(processed, file.Name)));

        await Record(fileId, sha, file, "Accepted", null, revisionId);
        _log.LogInformation("{File} registered as revision {Id}", file.Name, revisionId);
        return DropOutcome.Accepted;
    }

    private async Task<(string? Reason, RevisionFileName? Parsed, DocumentRecord? Document)> ValidateAsync(string projectFolder, ScannedFile file)
    {
        if (!RevisionFileName.TryParse(file.Name, out var parsed, out var error))
            return (error, null, null);

        var project = Path.GetFileName(projectFolder);
        if (!string.Equals(parsed.Document.Project, project, StringComparison.OrdinalIgnoreCase))
            return ($"{parsed.Document} belongs to project {parsed.Document.Project}, not {project}.", parsed, null);

        var doc = await _documents.FindAsync(parsed.Document.ToString());
        if (doc is null)
            return ($"{parsed.Document} is not in the register. Register new documents in the portal first.", parsed, null);

        var problem = RevisionRules.CheckNewRevision(parsed.Revision, await _revisions.HistoryAsync(doc.Id));
        if (problem is not null) return (problem, parsed, doc);

        if (_options.CheckPdfTitle && parsed.Extension == ".pdf")
        {
            using var stream = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var title = PdfTitleReader.ReadTitle(stream);
            if (title is not null && !title.Contains(parsed.Document.ToString(), StringComparison.OrdinalIgnoreCase))
                return ($"The title block says '{title}', which does not match the file name.", parsed, doc);
        }

        return (null, parsed, doc);
    }

    private void Reject(string projectFolder, ScannedFile file, string reason)
    {
        var folder = Path.Combine(projectFolder, _options.RejectedFolder);
        Directory.CreateDirectory(folder);
        var target = UniquePath(Path.Combine(folder, file.Name));
        File.Move(file.FullPath, target);
        File.WriteAllText(target + ".reason.txt", $"{DateTime.Now:yyyy-MM-dd HH:mm}  {reason}{Environment.NewLine}");
        _log.LogWarning("{File} rejected: {Reason}", file.Name, reason);
    }

    private Task Record(string fileId, string sha, ScannedFile file, string result, string? reason, int? revisionId) =>
        _dropFiles.AddAsync(new DropFileRecord
        {
            FileId = fileId,
            Sha256 = sha,
            OriginalPath = file.FullPath,
            Result = result,
            Reason = reason,
            RevisionId = revisionId,
            ProcessedUtc = DateTime.UtcNow,
        });

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
