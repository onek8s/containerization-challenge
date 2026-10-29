using Microsoft.EntityFrameworkCore;
using PolicyChanges.Core.Requests;
using PolicyChanges.Data.Entities;

namespace PolicyChanges.Data.Services;

public sealed record SubmitResult(ChangeRequest? Request, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Request is not null;
}

public sealed record StatusCount(ChangeStatus Status, int Count);

public sealed class ChangeService
{
    private readonly PolicyChangesContext _db;
    private readonly ChangeValidator _validator;
    private readonly TimeProvider _time;

    public ChangeService(PolicyChangesContext db, ChangeValidator validator, TimeProvider time)
    {
        _db = db;
        _validator = validator;
        _time = time;
    }

    public async Task<SubmitResult> SubmitAsync(ChangeDraft draft, string submittedBy, ChangeChannel channel,
        Partner? partner = null, string? partnerReference = null, CancellationToken ct = default)
    {
        draft.PolicyNumber = PolicyNumber.Normalize(draft.PolicyNumber);
        var errors = _validator.Validate(draft).ToList();

        if (partner is not null && !string.IsNullOrEmpty(partnerReference)
            && await _db.ChangeRequests.AnyAsync(r => r.PartnerId == partner.Id && r.PartnerReference == partnerReference, ct))
            errors.Add($"Partner reference {partnerReference} has already been submitted.");

        if (errors.Count == 0 && await _db.ChangeRequests.AnyAsync(r => r.PolicyNumber == draft.PolicyNumber
                && r.ChangeType == draft.ChangeType
                && (r.Status == ChangeStatus.Queued || r.Status == ChangeStatus.InProgress), ct))
            errors.Add($"A {draft.ChangeType} for {draft.PolicyNumber} is already waiting to be entered.");

        if (errors.Count > 0) return new SubmitResult(null, errors);

        var now = _time.GetUtcNow().UtcDateTime;
        var fields = draft.Fields
            .Where(f => !string.IsNullOrWhiteSpace(f.Value))
            .ToDictionary(f => ChangeFields.For(draft.ChangeType).First(n => n.Equals(f.Key, StringComparison.OrdinalIgnoreCase)), f => Clean(f.Key, f.Value));

        for (var attempt = 0; ; attempt++)
        {
            var request = new ChangeRequest
            {
                Reference = await NextReferenceAsync(ct),
                PolicyNumber = draft.PolicyNumber,
                ChangeType = draft.ChangeType,
                EffectiveDate = draft.EffectiveDate,
                Fields = fields,
                Status = ChangeStatus.Queued,
                Channel = channel,
                SubmittedBy = submittedBy,
                PartnerId = partner?.Id,
                PartnerReference = string.IsNullOrEmpty(partnerReference) ? null : partnerReference,
                CreatedUtc = now,
                UpdatedUtc = now,
                NextAttemptUtc = now,
            };
            request.Events.Add(new ChangeEvent { AtUtc = now, Status = ChangeStatus.Queued, Message = "Submitted", By = submittedBy });
            _db.ChangeRequests.Add(request);
            try
            {
                await _db.SaveChangesAsync(ct);
                return new SubmitResult(request, Array.Empty<string>());
            }
            catch (DbUpdateException) when (attempt < 5)
            {
                // Another submission took the same reference.
                _db.Entry(request).State = EntityState.Detached;
                foreach (var ev in request.Events) _db.Entry(ev).State = EntityState.Detached;
            }
        }
    }

    private static string Clean(string field, string value) => field.ToLowerInvariant() switch
    {
        "iban" or "refundiban" => Iban.Normalize(value),
        "country" => value.Trim().ToUpperInvariant(),
        "beneficiaries" => string.Join('\n', Beneficiary.ParseList(value, out _).Select(b => b.ToLine())),
        _ => value.Trim(),
    };

    private async Task<string> NextReferenceAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        var prefix = ChangeReference.Prefix(today);
        var last = await _db.ChangeRequests
            .Where(r => r.Reference.StartsWith(prefix))
            .OrderByDescending(r => r.Reference)
            .Select(r => r.Reference)
            .FirstOrDefaultAsync(ct);
        return ChangeReference.Create(today, ChangeReference.NextSequence(last));
    }

    public Task<ChangeRequest?> FindAsync(string reference, CancellationToken ct = default) =>
        _db.ChangeRequests
            .Include(r => r.Partner)
            .Include(r => r.Events.OrderBy(e => e.AtUtc))
            .Include(r => r.Evidence.OrderBy(e => e.Attempt).ThenBy(e => e.Id))
            .FirstOrDefaultAsync(r => r.Reference == reference, ct);

    public async Task<List<ChangeRequest>> ListAsync(ChangeStatus? status, string? search, int take = 200, CancellationToken ct = default)
    {
        var q = _db.ChangeRequests.Include(r => r.Partner).AsNoTracking();
        if (status is not null) q = q.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToUpperInvariant();
            q = q.Where(r => r.Reference.Contains(s) || r.PolicyNumber.Contains(s) || (r.Endorsement != null && r.Endorsement.Contains(s)));
        }
        return await q.OrderByDescending(r => r.Id).Take(take).ToListAsync(ct);
    }

    public async Task<List<StatusCount>> CountsAsync(CancellationToken ct = default) =>
        (await _db.ChangeRequests.GroupBy(r => r.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .Select(x => new StatusCount(x.Key, x.Count))
            .OrderBy(x => x.Status)
            .ToList();

    public Task<List<DeskStation>> StationsAsync(CancellationToken ct = default) =>
        _db.DeskStations.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);

    /// <summary>Puts a request that needs review back in the queue.</summary>
    public Task<bool> RequeueAsync(string reference, string by, CancellationToken ct = default) =>
        TransitionAsync(reference, by, ChangeStatus.NeedsReview, ChangeStatus.Queued, "Queued again", r => r.Attempts = 0, ct);

    public Task<bool> WithdrawAsync(string reference, string by, string reason, CancellationToken ct = default) =>
        TransitionAsync(reference, by, null, ChangeStatus.Withdrawn, "Withdrawn: " + reason, null, ct);

    private async Task<bool> TransitionAsync(string reference, string by, ChangeStatus? from, ChangeStatus to, string message,
        Action<ChangeRequest>? change, CancellationToken ct)
    {
        var r = await _db.ChangeRequests.FirstOrDefaultAsync(x => x.Reference == reference, ct);
        if (r is null) return false;
        if (from is not null && r.Status != from) return false;
        if (from is null && r.Status is not (ChangeStatus.Queued or ChangeStatus.NeedsReview)) return false;

        var now = _time.GetUtcNow().UtcDateTime;
        r.Status = to;
        r.UpdatedUtc = now;
        r.NextAttemptUtc = now;
        r.LockedBy = null;
        r.LockedUntilUtc = null;
        if (to == ChangeStatus.Withdrawn) r.CompletedUtc = now;
        change?.Invoke(r);
        _db.ChangeEvents.Add(new ChangeEvent { ChangeRequestId = r.Id, AtUtc = now, Status = to, Message = message, By = by });
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
