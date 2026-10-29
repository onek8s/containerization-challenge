using System.Security.Cryptography;
using Litware.DocControl.Documents;
using Litware.DocControl.Platform;
using Microsoft.Extensions.Options;

namespace Litware.DocControl.Data.Storage;

public sealed record StoredFile(string RelativePath, long Size, string Sha256);

/// <summary>
/// Document vault on the file server: <c>{root}\{project}\{document}\{revision}\{file}</c>.
/// Paths in the database are relative with forward slashes.
/// </summary>
public sealed class VaultStore
{
    private readonly StorageOptions _options;

    public VaultStore(IOptions<StorageOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.VaultRoot))
            throw new InvalidOperationException($"{StorageOptions.SectionName}:VaultRoot is not configured.");
    }

    public string Root => _options.VaultRoot;

    public static string RelativePath(RevisionFileName file) =>
        $"{file.Document.Project}/{file.Document}/{file.Revision}/{file}";

    public string FullPath(string relativePath) =>
        Path.Combine(_options.VaultRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public bool Exists(string relativePath) => File.Exists(FullPath(relativePath));

    public Stream OpenRead(string relativePath) =>
        new FileStream(FullPath(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);

    /// <summary>Stores an uploaded file. The file only appears under its final name when it is complete.</summary>
    public async Task<StoredFile> SaveAsync(RevisionFileName file, Stream content, CancellationToken cancellationToken = default)
    {
        var relative = RelativePath(file);
        var target = FullPath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var partial = target + ".partial";

        long size = 0;
        string hash;
        await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            var buffer = new byte[81920];
            int n;
            while ((n = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                sha.AppendData(buffer, 0, n);
                await output.WriteAsync(buffer.AsMemory(0, n), cancellationToken);
                size += n;
            }
            hash = Convert.ToHexString(sha.GetHashAndReset());
        }

        if (OperatingSystem.IsWindows())
            FileOps.MoveWriteThrough(partial, target, overwrite: true);
        else
            File.Move(partial, target, overwrite: true);

        File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.ReadOnly);
        return new StoredFile(relative, size, hash);
    }

    /// <summary>Copies a file from the drop share into the vault.</summary>
    public StoredFile Import(RevisionFileName file, string sourcePath, string sha256, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var relative = RelativePath(file);
        var target = FullPath(relative);
        if (File.Exists(target)) File.SetAttributes(target, FileAttributes.Normal);

        FileOps.CopyWithProgress(sourcePath, target, overwrite: true, progress, cancellationToken);
        File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.ReadOnly);
        return new StoredFile(relative, new FileInfo(target).Length, sha256);
    }

    public static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
