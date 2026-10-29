using System.Data;
using System.Text.Json;

namespace Lucerne.Lending.Data;

/// <summary>Work handed from the web applications to the background services (claimed with a lease).</summary>
public sealed class OutboxRepository
{
    private readonly Db _db;

    public OutboxRepository(Db db) => _db = db;

    public Task EnqueueAsync(string topic, object payload, IDbTransaction? tx = null, DateTime? notBeforeUtc = null)
    {
        var now = DateTime.UtcNow;
        return _db.ExecuteAsync(@"INSERT INTO lending.Outbox(Topic, Payload, CreatedUtc, NextAttemptUtc, Attempts)
            VALUES (@topic, @payload, @now, @next, 0)",
            new { topic, payload = JsonSerializer.Serialize(payload), now, next = notBeforeUtc ?? now }, tx);
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(string[] topics, string worker, int take, TimeSpan lease, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var candidates = await _db.QueryAsync<long>(@"SELECT TOP (@take) Id FROM lending.Outbox WITH (UPDLOCK, READPAST)
            WHERE Topic IN @topics AND ProcessedUtc IS NULL AND NextAttemptUtc <= @now AND (LockedUntilUtc IS NULL OR LockedUntilUtc < @now)
            ORDER BY Id", new { take, topics, now }, ct: ct);
        var claimed = new List<OutboxMessage>();
        foreach (var id in candidates)
        {
            var rows = await _db.ExecuteAsync(@"UPDATE lending.Outbox SET LockedBy = @worker, LockedUntilUtc = @until, Attempts = Attempts + 1
                WHERE Id = @id AND ProcessedUtc IS NULL AND (LockedUntilUtc IS NULL OR LockedUntilUtc < @now)",
                new { id, worker, until = now + lease, now }, ct: ct);
            if (rows == 1)
                claimed.Add((await _db.FirstOrDefaultAsync<OutboxMessage>("SELECT Id, Topic, Payload, Attempts, CreatedUtc FROM lending.Outbox WHERE Id = @id",
                    new { id }, ct: ct))!);
        }
        return claimed;
    }

    public Task CompleteAsync(long id) =>
        _db.ExecuteAsync("UPDATE lending.Outbox SET ProcessedUtc = @now, LockedBy = NULL, LockedUntilUtc = NULL, LastError = NULL WHERE Id = @id",
            new { id, now = DateTime.UtcNow });

    public Task FailAsync(long id, string error, TimeSpan retryIn) =>
        _db.ExecuteAsync(@"UPDATE lending.Outbox SET LockedBy = NULL, LockedUntilUtc = NULL, LastError = @error, NextAttemptUtc = @next WHERE Id = @id",
            new { id, error = error.Length > 2000 ? error[..2000] : error, next = DateTime.UtcNow + retryIn });

    public Task<int> PendingCountAsync(string topic) =>
        _db.ScalarAsync<int>("SELECT COUNT(*) FROM lending.Outbox WHERE Topic = @topic AND ProcessedUtc IS NULL", new { topic });

    public static T Read<T>(OutboxMessage message) => JsonSerializer.Deserialize<T>(message.Payload)!;
}

public sealed record DocumentRequest(string Kind, long? ApplicationId, long? CollectionCaseId, string? Template = null,
    Recipient? Recipient = null, Dictionary<string, string>? Values = null);

public sealed record Recipient(string Name, string Street, string PostalCode, string City, string? Email);

public sealed record SigningRequest(long ApplicationId, long DocumentId);

public sealed record CustomerNotification(string Email, string Template, Dictionary<string, string> Values);
