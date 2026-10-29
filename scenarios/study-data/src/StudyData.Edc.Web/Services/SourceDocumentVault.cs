using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace StudyData.Edc.Web.Services;

/// <summary>
/// Redacted source documents (lab reports, scan reports, ECGs) uploaded by sites for remote source data
/// verification: {vault}\{study}\{site}\{subject}\{id}_{name}.
/// </summary>
public class SourceDocumentVault
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".png", ".jpg", ".jpeg", ".tif", ".tiff" };

    private readonly EdcOptions _options;

    public SourceDocumentVault(IOptions<EdcOptions> options)
    {
        _options = options.Value;
    }

    public static bool IsAllowed(string fileName) => Allowed.Contains(Path.GetExtension(fileName));

    public async Task<(string Path, string Sha256, long Length)> StoreAsync(string study, string site, string subject, string fileName, Stream content, CancellationToken ct)
    {
        var folder = Path.Combine(_options.SourceDocumentVault, study, site, subject);
        Directory.CreateDirectory(folder);
        var safe = string.Concat(Path.GetFileName(fileName).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = Path.Combine(folder, $"{Guid.NewGuid():N}_{safe}");
        var temp = path + ".tmp";
        await using (var file = File.Create(temp))
        {
            await content.CopyToAsync(file, ct);
        }

        string hash;
        await using (var file = File.OpenRead(temp))
        {
            hash = Convert.ToHexString(await SHA256.HashDataAsync(file, ct)).ToLowerInvariant();
        }

        File.Move(temp, path);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        return (path, hash, new FileInfo(path).Length);
    }

    public Stream Open(string path) => File.OpenRead(path);
}
