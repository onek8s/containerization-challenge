using System.IO.Compression;
using System.Security.Cryptography;

namespace Litware.DocControl.Packaging;

/// <summary>
/// Writes a transmittal package: <c>{number}/documents/*</c>, <c>{number}/manifest.csv</c>
/// and <c>{number}/{number}.pdf</c> (cover sheet).
/// </summary>
public static class TransmittalPackageWriter
{
    public static IReadOnlyList<PackagedFile> Write(Stream output, TransmittalHeader header, IEnumerable<PackageEntry> entries,
        Action<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var files = new List<PackagedFile>();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Invoke(entry.FileName);

                var name = $"{header.Number}/documents/{entry.FileName}";
                var zipEntry = zip.CreateEntry(name, ChooseLevel(entry.FileName));
                zipEntry.LastWriteTime = header.IssuedUtc;

                long size;
                string hash;
                using (var source = entry.Open())
                using (var target = zipEntry.Open())
                using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    var buffer = new byte[81920];
                    size = 0;
                    int n;
                    while ((n = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        sha.AppendData(buffer, 0, n);
                        target.Write(buffer, 0, n);
                        size += n;
                    }
                    hash = Convert.ToHexString(sha.GetHashAndReset());
                }

                files.Add(new PackagedFile(entry.DocumentNumber, entry.Revision, entry.Title, "documents/" + entry.FileName, size, hash));
            }

            AddBytes(zip, $"{header.Number}/manifest.csv", ManifestWriter.ToBytes(files), header.IssuedUtc);
            AddBytes(zip, $"{header.Number}/{header.Number}.pdf", CoverSheet.Render(header, files), header.IssuedUtc);
        }
        return files;
    }

    private static void AddBytes(ZipArchive zip, string name, byte[] bytes, DateTime time)
    {
        var e = zip.CreateEntry(name, CompressionLevel.Optimal);
        e.LastWriteTime = time;
        using var s = e.Open();
        s.Write(bytes, 0, bytes.Length);
    }

    // PDFs and Office files are already compressed; deflating them again only costs time.
    private static CompressionLevel ChooseLevel(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" or ".docx" or ".xlsx" or ".zip" => CompressionLevel.Fastest,
        _ => CompressionLevel.Optimal,
    };
}
