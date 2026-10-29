using System.Security.Cryptography;

namespace ExpenseClaims.Api.Services;

public sealed class ReceiptStoreOptions
{
    public const string SectionName = "Receipts";

    /// <summary>Folder where receipt files are kept, one subfolder per year and month.</summary>
    public string RootPath { get; set; } = @"D:\ExpenseReceipts";

    public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;

    public string[] AllowedContentTypes { get; set; } = { "application/pdf", "image/jpeg", "image/png", "image/heic" };
}

public sealed record StoredReceipt(Guid FileId, string RelativePath, long Length, string Sha256);

public interface IReceiptStore
{
    Task<StoredReceipt> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);
    Stream OpenRead(string relativePath);
    void Delete(string relativePath);
}

public sealed class FileSystemReceiptStore : IReceiptStore
{
    private readonly string _root;

    public FileSystemReceiptStore(Microsoft.Extensions.Options.IOptions<ReceiptStoreOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
    }

    public async Task<StoredReceipt> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        var fileId = Guid.NewGuid();
        var now = DateTime.Now;
        var relative = Path.Combine(now.ToString("yyyy"), now.ToString("MM"), fileId.ToString("N") + extension.ToLowerInvariant());
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        using var sha = SHA256.Create();
        long length;
        await using (var file = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        await using (var hashing = new CryptoStream(file, sha, CryptoStreamMode.Write))
        {
            await content.CopyToAsync(hashing, cancellationToken);
            await hashing.FlushFinalBlockAsync(cancellationToken);
            length = file.Length;
        }

        return new StoredReceipt(fileId, relative, length, Convert.ToHexString(sha.Hash!));
    }

    public Stream OpenRead(string relativePath) =>
        new FileStream(Resolve(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

    public void Delete(string relativePath)
    {
        var full = Resolve(relativePath);
        if (File.Exists(full))
            File.Delete(full);
    }

    private string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Receipt path is outside the receipt store.");
        return full;
    }
}
