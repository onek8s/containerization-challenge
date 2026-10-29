using System.Globalization;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BatchRelease.Vault;

public sealed record StoredFile(string RelativePath, string Sha256, long Length);

public sealed class VaultIntegrityException : Exception
{
    public VaultIntegrityException(string message) : base(message)
    {
    }
}

/// <summary>
/// Write-once store for executed batch records, certificates of analysis and signed batch
/// certificates. Files are encrypted with EFS under the service account, carry a protected
/// DACL and are checked against their SHA-256 on every read.
/// </summary>
public sealed class RecordVault
{
    private readonly VaultOptions _options;
    private readonly ILogger<RecordVault> _logger;
    private readonly string _root;

    public RecordVault(IOptions<VaultOptions> options, ILogger<RecordVault> logger)
    {
        _options = options.Value;
        _logger = logger;
        _root = Path.GetFullPath(_options.Root);
    }

    public string Root => _root;

    public bool RootEncrypted =>
        Directory.Exists(_root) && new DirectoryInfo(_root).Attributes.HasFlag(FileAttributes.Encrypted);

    public void EnsureReady()
    {
        if (!Directory.Exists(_root))
            throw new DirectoryNotFoundException($"Vault root {_root} does not exist. Create it with create-vault.ps1.");

        var drive = new DriveInfo(Path.GetPathRoot(_root)!);
        if (!string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Vault root {_root} must be on an NTFS volume (found {drive.DriveFormat}).");

        if (_options.RequireEncryption && !RootEncrypted)
            throw new InvalidOperationException($"Vault root {_root} is not marked for encryption. Run create-vault.ps1.");
    }

    public async Task<StoredFile> StoreAsync(string siteCode, string batchNumber, string kind, string fileName, Stream content, CancellationToken ct)
    {
        var folder = Path.Combine(_root, Safe(siteCode), Safe(batchNumber));
        Directory.CreateDirectory(folder);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
        var finalName = $"{stamp}-{Safe(kind)}-{Safe(Path.GetFileName(fileName))}";
        var finalPath = Path.Combine(folder, finalName);
        var partialPath = finalPath + ".partial";

        string sha;
        long length;
        await using (var target = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            await target.FlushAsync(ct);
            length = target.Length;
            sha = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        if (_options.RequireEncryption && !File.GetAttributes(partialPath).HasFlag(FileAttributes.Encrypted))
            File.Encrypt(partialPath);

        ProtectFile(partialPath);
        File.Move(partialPath, finalPath);
        File.SetAttributes(finalPath, File.GetAttributes(finalPath) | FileAttributes.ReadOnly);

        _logger.LogInformation("Stored {Kind} {File} for batch {Batch} ({Length} bytes, sha256 {Sha})", kind, fileName, batchNumber, length, sha);
        return new StoredFile(Path.GetRelativePath(_root, finalPath), sha, length);
    }

    public StoredFile StoreBytes(string siteCode, string batchNumber, string kind, string fileName, byte[] content)
    {
        using var ms = new MemoryStream(content, writable: false);
        return StoreAsync(siteCode, batchNumber, kind, fileName, ms, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>Opens a vault file after checking it against the hash recorded when it was stored.</summary>
    public async Task<Stream> OpenVerifiedAsync(string relativePath, string expectedSha256, CancellationToken ct)
    {
        var path = Resolve(relativePath);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        try
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
            if (!string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new VaultIntegrityException($"{relativePath}: SHA-256 is {actual}, expected {expectedSha256}.");
            stream.Position = 0;
            return stream;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    public async Task<byte[]> ReadVerifiedAsync(string relativePath, string expectedSha256, CancellationToken ct)
    {
        await using var stream = await OpenVerifiedAsync(relativePath, expectedSha256, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Path is outside the vault.", nameof(relativePath));
        return full;
    }

    private void ProtectFile(string path)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        var service = WindowsIdentity.GetCurrent().User!;
        security.SetOwner(service);
        security.AddAccessRule(new FileSystemAccessRule(service, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
        foreach (var reader in _options.AdditionalReaders)
        {
            var sid = (SecurityIdentifier)new NTAccount(reader).Translate(typeof(SecurityIdentifier));
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.Read, AccessControlType.Allow));
        }

        new FileInfo(path).SetAccessControl(security);
    }

    private static string Safe(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
            sb.Append(invalid.Contains(ch) || ch == '.' && sb.Length == 0 ? '_' : ch);
        var result = sb.ToString();
        return result.Length == 0 ? "_" : result.Length > 100 ? result[..100] : result;
    }
}
