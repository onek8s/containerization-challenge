using System.Security.Cryptography;

namespace Proseware.ControlTower.Documents.Storage;

internal static class DocumentHashing
{
    /// <summary>Copies the content while hashing it; returns length and SHA-256 hex.</summary>
    public static async Task<(long Length, string Sha256)> CopyAsync(Stream source, Stream target, CancellationToken ct)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            sha.AppendData(buffer, 0, read);
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            total += read;
        }
        return (total, Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant());
    }
}
