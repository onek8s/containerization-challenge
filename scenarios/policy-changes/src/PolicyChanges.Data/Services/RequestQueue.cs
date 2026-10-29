using Microsoft.EntityFrameworkCore;
using PolicyChanges.Core.Flows;
using PolicyChanges.Core.Requests;
using PolicyChanges.Data.Entities;

namespace PolicyChanges.Data.Services;

public sealed class QueueOptions
{
    public const string Section = "Queue";

    /// <summary>How long a desk station owns a claimed request.</summary>
    public int LeaseMinutes { get; set; } = 10;

    /// <summary>Attempts before a request goes to review.</summary>
    public int MaxAttempts { get; set; } = 4;

    /// <summary>Wait before attempt 2, 3, 4...</summary>
    public int[] RetryDelayMinutes { get; set; } = { 5, 15, 60 };
}

public sealed record EvidenceFile(string Name, string RelativePath);

/// <summary>Queue of changes waiting for a desk station.</summary>
public sealed class RequestQueue
{
    private readonly PolicyChangesContext _db;
    private readonly TimeProvider _time;
    private readonly QueueOptions _options;

    public RequestQueue(PolicyChangesContext db, TimeProvider time, Microsoft.Extensions.Options.IOptions<QueueOptions> options)
    {
        _db = db;
        _time = time;
        _options = options.Value;
    }

    /// <summary>Claims the next request for <paramref name="station"/>. Expired leases are taken over.</summary>
    public async Task<ChangeRequest?> ClaimNextAsync(string station, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var until = now.AddMinutes(_options.LeaseMinutes);
        long? id;

        if (_db.IsSqlServer)
        {
            id = (await _db.Database.SqlQuery<long>($"""
                WITH next AS (
                    SELECT TOP (1) *
                    FROM dbo.ChangeRequests WITH (UPDLOCK, READPAST, ROWLOCK)
                    WHERE (Status = 'Queued' AND NextAttemptUtc <= {now})
                       OR (Status = 'InProgress' AND LockedUntilUtc < {now})
                    ORDER BY EffectiveDate, Id)
                UPDATE next
                SET Status = 'InProgress', LockedBy = {station}, LockedUntilUtc = {until},
                    Attempts = Attempts + 1, UpdatedUtc = {now}
                OUTPUT inserted.Id AS Value;
                """).ToListAsync(ct)).Cast<long?>().FirstOrDefault();
        }
        else
        {
            id = null;
            var candidates = await _db.ChangeRequests
                .Where(r => (r.Status == ChangeStatus.Queued && r.NextAttemptUtc <= now)
                         || (r.Status == ChangeStatus.InProgress && r.LockedUntilUtc < now))
                .OrderBy(r => r.EffectiveDate).ThenBy(r => r.Id)
                .Select(r => new { r.Id, r.Status, r.Attempts })
                .Take(5)
                .ToListAsync(ct);
            foreach (var c in candidates)
            {
                var updated = await _db.ChangeRequests
                    .Where(r => r.Id == c.Id && r.Status == c.Status && r.Attempts == c.Attempts)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(r => r.Status, ChangeStatus.InProgress)
                        .SetProperty(r => r.LockedBy, station)
                        .SetProperty(r => r.LockedUntilUtc, until)
                        .SetProperty(r => r.Attempts, r => r.Attempts + 1)
                        .SetProperty(r => r.UpdatedUtc, now), ct);
                if (updated == 1)
                {
                    id = c.Id;
                    break;
                }
            }
        }

        if (id is null) return null;
        var request = await _db.ChangeRequests.FirstAsync(r => r.Id == id, ct);
        await _db.Entry(request).ReloadAsync(ct);
        _db.ChangeEvents.Add(new ChangeEvent
        {
            ChangeRequestId = request.Id, AtUtc = now, Status = ChangeStatus.InProgress,
            Message = $"Attempt {request.Attempts} started", By = station,
        });
        await _db.SaveChangesAsync(ct);
        return request;
    }

    /// <summary>Extends the lease while a long flow is running.</summary>
    public async Task<bool> RenewAsync(long id, string station, CancellationToken ct = default)
    {
        var until = _time.GetUtcNow().UtcDateTime.AddMinutes(_options.LeaseMinutes);
        return await _db.ChangeRequests
            .Where(r => r.Id == id && r.Status == ChangeStatus.InProgress && r.LockedBy == station)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.LockedUntilUtc, until), ct) == 1;
    }

    public async Task CompleteAsync(long id, string station, FlowOutcome outcome, string message, string? endorsement,
        IEnumerable<EvidenceFile> evidence, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var r = await _db.ChangeRequests.FirstAsync(x => x.Id == id, ct);
        if (r.Status != ChangeStatus.InProgress || r.LockedBy != station)
            throw new InvalidOperationException($"{r.Reference} is no longer claimed by {station}.");

        foreach (var e in evidence)
            _db.ChangeEvidence.Add(new ChangeEvidence
            {
                ChangeRequestId = id, Attempt = r.Attempts, Name = e.Name, RelativePath = e.RelativePath,
                CapturedUtc = now, Station = station,
            });

        r.LockedBy = null;
        r.LockedUntilUtc = null;
        r.UpdatedUtc = now;
        r.ResultMessage = Truncate(message, 500);

        switch (outcome)
        {
            case FlowOutcome.Applied:
                r.Status = ChangeStatus.Applied;
                r.Endorsement = endorsement;
                r.CompletedUtc = now;
                break;
            case FlowOutcome.Rejected:
                r.Status = ChangeStatus.Rejected;
                r.CompletedUtc = now;
                break;
            default:
                if (r.Attempts >= _options.MaxAttempts)
                {
                    r.Status = ChangeStatus.NeedsReview;
                }
                else
                {
                    r.Status = ChangeStatus.Queued;
                    var delays = _options.RetryDelayMinutes;
                    var delay = delays.Length == 0 ? 5 : delays[Math.Min(r.Attempts - 1, delays.Length - 1)];
                    r.NextAttemptUtc = now.AddMinutes(delay);
                }
                break;
        }

        _db.ChangeEvents.Add(new ChangeEvent { ChangeRequestId = id, AtUtc = now, Status = r.Status, Message = r.ResultMessage ?? "", By = station });
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Gives a claimed request back without counting the attempt (desk not usable).</summary>
    public async Task ReleaseAsync(long id, string station, string reason, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var r = await _db.ChangeRequests.FirstAsync(x => x.Id == id, ct);
        if (r.Status != ChangeStatus.InProgress || r.LockedBy != station) return;
        r.Status = ChangeStatus.Queued;
        r.Attempts = Math.Max(0, r.Attempts - 1);
        r.LockedBy = null;
        r.LockedUntilUtc = null;
        r.UpdatedUtc = now;
        r.NextAttemptUtc = now;
        _db.ChangeEvents.Add(new ChangeEvent { ChangeRequestId = id, AtUtc = now, Status = r.Status, Message = "Released: " + reason, By = station });
        await _db.SaveChangesAsync(ct);
    }

    public async Task HeartbeatAsync(string station, string userName, string state, string? detail, string? clientVersion,
        string? currentReference, FlowOutcome? lastOutcome = null, CancellationToken ct = default)
    {
        var s = await _db.DeskStations.FirstOrDefaultAsync(x => x.Name == station, ct);
        if (s is null)
        {
            s = new DeskStation { Name = station };
            _db.DeskStations.Add(s);
        }
        s.UserName = userName;
        s.State = state;
        s.Detail = Truncate(detail, 300);
        s.ClientVersion = clientVersion ?? s.ClientVersion;
        s.CurrentReference = currentReference;
        s.LastSeenUtc = _time.GetUtcNow().UtcDateTime;
        if (lastOutcome == FlowOutcome.Applied) s.AppliedCount++;
        if (lastOutcome == FlowOutcome.Failed) s.FailedCount++;
        await _db.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
