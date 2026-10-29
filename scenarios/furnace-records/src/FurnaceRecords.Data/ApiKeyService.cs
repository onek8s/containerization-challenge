using System.Security.Cryptography;
using System.Text;
using FurnaceRecords.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Data;

public sealed class ApiKeyService
{
    private readonly FurnaceRecordsContext _db;
    private readonly TimeProvider _time;

    public ApiKeyService(FurnaceRecordsContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    public Task<ApiClient?> FindAsync(string key, CancellationToken ct = default)
    {
        var hash = Hash(key);
        return _db.ApiClients.AsNoTracking().FirstOrDefaultAsync(c => c.KeyHash == hash && c.Active, ct);
    }

    /// <summary>Creates a client and returns the key; only the hash is stored.</summary>
    public async Task<string> IssueAsync(string name, CancellationToken ct = default)
    {
        var key = "fr_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();
        _db.ApiClients.Add(new ApiClient { Name = name, KeyHash = Hash(key), CreatedAtUtc = _time.GetUtcNow().UtcDateTime });
        await _db.SaveChangesAsync(ct);
        return key;
    }
}
