using Litware.DocControl.Data;
using Litware.DocControl.Data.Storage;
using Litware.DocControl.Packaging;
using Microsoft.Extensions.Options;

namespace Litware.DocControl.Web.Services;

/// <summary>Development only: writes placeholder files for the sample revisions that have no file in the vault.</summary>
public sealed class SampleFileSeeder : IHostedService
{
    private readonly DocumentRepository _documents;
    private readonly RevisionRepository _revisions;
    private readonly VaultStore _vault;
    private readonly IOptions<DatabaseOptions> _db;

    public SampleFileSeeder(DocumentRepository documents, RevisionRepository revisions, VaultStore vault, IOptions<DatabaseOptions> db)
    {
        _documents = documents;
        _revisions = revisions;
        _vault = vault;
        _db = db;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_db.Value.CreateSampleData) return;
        foreach (var project in await _documents.ProjectsAsync(activeOnly: false))
        foreach (var doc in await _documents.RegisterAsync(project.Code))
        foreach (var rev in await _revisions.ForDocumentAsync(doc.Id))
        {
            if (_vault.Exists(rev.VaultPath)) continue;
            var pdf = new SimplePdfWriter { Title = $"{rev.DocumentNumber} {rev.Code}", Author = rev.SubmittedBy };
            pdf.NewPage();
            pdf.Text(50, 60, rev.DocumentNumber, 18, bold: true);
            pdf.Text(50, 85, doc.Title, 12);
            pdf.Text(50, 105, "Revision " + rev.Code, 12);
            var path = _vault.FullPath(rev.VaultPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, pdf.ToArray(), cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
