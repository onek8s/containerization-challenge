using Humongous.Quoting.Contracts.Messages;
using Humongous.Quoting.Data;
using Humongous.Quoting.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Humongous.PolicyService.Jobs;

/// <summary>Expires quotes past their validity date.</summary>
public sealed class QuoteExpiryJob(QuotingDbContext db, IPublishEndpoint bus, TimeProvider clock, ILogger<QuoteExpiryJob> logger) : IScheduledJob
{
    public string Name => "quote-expiry";

    public async Task RunAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var expired = await db.Quotes.Include(q => q.Events)
            .Where(q => (q.Status == QuoteStatus.Quoted || q.Status == QuoteStatus.Accepted || q.Status == QuoteStatus.Referred)
                        && q.ValidUntil != null && q.ValidUntil < today)
            .ToListAsync(ct);
        foreach (var quote in expired)
            quote.Move(QuoteStatus.Expired, "quote-expiry");
        await db.SaveChangesAsync(ct);
        foreach (var quote in expired)
            await bus.Publish(new QuoteStatusChanged { QuoteId = quote.Id, Reference = quote.Reference, AgencyCode = quote.AgencyCode, Status = quote.Status, Total = quote.Total }, ct);
        logger.LogInformation("Expired {Count} quotes", expired.Count);
    }
}

/// <summary>Lapses policies that reached expiry without being renewed.</summary>
public sealed class LapseJob(QuotingDbContext db, TimeProvider clock, ILogger<LapseJob> logger) : IScheduledJob
{
    public string Name => "lapse";

    public async Task RunAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var lapsed = await db.Policies.Where(p => p.Status == PolicyStatus.Live && p.ExpiryDate < today)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PolicyStatus.Lapsed), ct);
        logger.LogInformation("Lapsed {Count} policies", lapsed);
    }
}
