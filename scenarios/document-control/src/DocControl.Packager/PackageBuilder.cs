using Litware.DocControl.Data;
using Litware.DocControl.Data.Storage;
using Litware.DocControl.Packaging;
using Litware.DocControl.Platform;
using Litware.DocControl.Transmittals;
using Microsoft.Extensions.Options;

namespace Litware.DocControl.Packager;

public sealed record BuiltPackage(string RelativePath, string FullPath, long Size, IReadOnlyList<PackagedFile> Files, byte[] CoverSheet);

public sealed class PackageBuilder
{
    private readonly DocumentRepository _documents;
    private readonly RevisionRepository _revisions;
    private readonly TransmittalRepository _transmittals;
    private readonly VaultStore _vault;
    private readonly StorageOptions _storage;
    private readonly PackagerOptions _options;
    private readonly ILogger<PackageBuilder> _log;

    public PackageBuilder(DocumentRepository documents, RevisionRepository revisions, TransmittalRepository transmittals, VaultStore vault,
        IOptions<StorageOptions> storage, IOptions<PackagerOptions> options, ILogger<PackageBuilder> log)
    {
        _documents = documents;
        _revisions = revisions;
        _transmittals = transmittals;
        _vault = vault;
        _storage = storage.Value;
        _options = options.Value;
        _log = log;
    }

    public string WorkFolder => string.IsNullOrWhiteSpace(_options.WorkFolder)
        ? Path.Combine(InstallLocation.DataPath("Packager"), "work")
        : _options.WorkFolder;

    /// <summary>Checks that both the work folder and the transmittal share have room for another package.</summary>
    public string? CheckFreeSpace()
    {
        Directory.CreateDirectory(WorkFolder);
        foreach (var folder in new[] { WorkFolder, _storage.TransmittalRoot })
        {
            var space = DiskSpace.Get(folder);
            if ((long)space.FreeBytesAvailable < _options.MinFreeBytes)
                return $"Only {CoverSheet.FormatSize((long)space.FreeBytesAvailable)} free on {folder}.";
        }
        return null;
    }

    public async Task<BuiltPackage> BuildAsync(TransmittalRecord t, CancellationToken cancellationToken)
    {
        var project = await _documents.ProjectAsync(t.ProjectCode);
        var items = await _revisions.GetManyAsync(await _transmittals.ItemsAsync(t.Id));
        if (items.Count == 0) throw new InvalidOperationException("The transmittal has no documents.");

        var missing = items.Where(r => !_vault.Exists(r.VaultPath)).Select(r => r.FileName).ToList();
        if (missing.Count > 0) throw new FileNotFoundException("Missing from the vault: " + string.Join(", ", missing));

        var header = new TransmittalHeader(t.Number, t.ProjectCode, project?.Name ?? "", $"{t.Purpose.Code()} - {t.Purpose.Description()}",
            t.RecipientCompany, t.RecipientName, t.CreatedBy, DateTime.UtcNow, t.Notes);

        var entries = items.Select(r => new PackageEntry(r.DocumentNumber, r.Code, r.DocumentTitle, r.FileName, () => _vault.OpenRead(r.VaultPath)));

        Directory.CreateDirectory(WorkFolder);
        var temp = Path.Combine(WorkFolder, t.Number + ".zip.tmp");
        IReadOnlyList<PackagedFile> files;
        var timer = PerfTimer.Start();

        using (new PowerRequest())
        await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
        {
            files = TransmittalPackageWriter.Write(output, header, entries,
                name => _log.LogDebug("{Transmittal}: adding {File}", t.Number, name), cancellationToken);
            await output.FlushAsync(cancellationToken);
        }

        var relative = $"{header.IssuedUtc:yyyy}/{t.Number}.zip";
        var target = Path.Combine(_storage.TransmittalRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        FileOps.MoveWriteThrough(temp, target, overwrite: true);

        var size = new FileInfo(target).Length;
        _log.LogInformation("{Transmittal}: {Count} documents, {Size} in {Elapsed:0.0} s", t.Number, files.Count, CoverSheet.FormatSize(size), timer.Elapsed.TotalSeconds);

        return new BuiltPackage(relative, target, size, files, CoverSheet.Render(header, files));
    }
}
