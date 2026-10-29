using BatchRelease.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace BatchRelease.Data;

public sealed record BatchListItem(
    int Id, string BatchNumber, string ProductCode, string ProductName, string SiteCode,
    DateOnly ManufacturedOn, DateOnly ExpiresOn, BatchStatus Status, DateTime ReceivedUtc,
    int OpenDeviations, int FailedResults, string? CertificateNumber);

public sealed class BatchQueries
{
    private readonly BatchReleaseContext _db;

    public BatchQueries(BatchReleaseContext db) => _db = db;

    public Task<List<BatchListItem>> ListAsync(IReadOnlyCollection<BatchStatus>? statuses, string? search, int take, CancellationToken ct = default)
    {
        var q = _db.Batches.AsNoTracking();
        if (statuses is { Count: > 0 })
            q = q.Where(b => statuses.Contains(b.Status));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(b => b.BatchNumber.StartsWith(s) || b.ProductCode.StartsWith(s) || b.Product!.Name.Contains(s));
        }

        return q.OrderBy(b => b.Status).ThenBy(b => b.ReceivedUtc)
            .Take(take)
            .Select(b => new BatchListItem(
                b.Id, b.BatchNumber, b.ProductCode, b.Product!.Name, b.SiteCode, b.ManufacturedOn, b.ExpiresOn, b.Status, b.ReceivedUtc,
                b.Deviations.Count(d => d.Status != DeviationStatus.Closed),
                b.Results.Count(r => r.Outcome == ResultOutcome.Fail),
                b.CertificateNumber))
            .ToListAsync(ct);
    }

    /// <summary>Loads a batch with everything the disposition rules need.</summary>
    public Task<Batch?> GetAsync(int id, CancellationToken ct = default) =>
        Full().FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<Batch?> GetByNumberAsync(string batchNumber, CancellationToken ct = default) =>
        Full().FirstOrDefaultAsync(b => b.BatchNumber == batchNumber, ct);

    public Task<List<Batch>> DispositionsSinceAsync(DateTime sinceUtc, string? siteCode, CancellationToken ct = default)
    {
        var q = _db.Batches.AsNoTracking().Include(b => b.Product)
            .Where(b => b.DispositionUtc != null && b.DispositionUtc >= sinceUtc);
        if (siteCode is not null)
            q = q.Where(b => b.SiteCode == siteCode);
        return q.OrderBy(b => b.DispositionUtc).Take(500).ToListAsync(ct);
    }

    public Task<List<Signer>> SignersAsync(CancellationToken ct = default) =>
        _db.Signers.AsNoTracking().OrderBy(s => s.DisplayName).ToListAsync(ct);

    public Task<List<ProductFamily>> FamiliesAsync(CancellationToken ct = default) =>
        _db.ProductFamilies.AsNoTracking().OrderBy(f => f.Name).ToListAsync(ct);

    private IQueryable<Batch> Full() => _db.Batches
        .Include(b => b.Product).ThenInclude(p => p!.Family)
        .Include(b => b.Steps)
        .Include(b => b.Results)
        .Include(b => b.Deviations)
        .Include(b => b.Documents)
        .Include(b => b.Decisions)
        .AsSplitQuery();
}
