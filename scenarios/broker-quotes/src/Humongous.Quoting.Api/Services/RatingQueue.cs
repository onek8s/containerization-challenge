using Humongous.Quoting.Api.Hubs;
using System.Threading.Channels;
using Humongous.Quoting.Contracts.Hubs;
using Humongous.Quoting.Contracts.Messages;
using Humongous.Quoting.Data;
using Humongous.Quoting.Data.Entities;
using Humongous.Quoting.Domain;
using Humongous.Quoting.Rating;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Humongous.Quoting.Api.Services;

/// <summary>Quotes waiting for rating. Rating runs in the background so the broker gets an immediate answer.</summary>
public sealed class RatingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(500)
    {
        FullMode = BoundedChannelFullMode.Wait
    });

    public ValueTask EnqueueAsync(Guid quoteId, CancellationToken ct = default) => _channel.Writer.WriteAsync(quoteId, ct);

    public ChannelReader<Guid> Reader => _channel.Reader;
}

public sealed class RatingWorker(
    RatingQueue queue,
    IServiceScopeFactory scopes,
    IOptions<RatingOptions> ratingOptions,
    ILogger<RatingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueInterruptedAsync(stoppingToken);

        var readers = Enumerable.Range(0, Math.Max(1, ratingOptions.Value.Workers))
            .Select(_ => ReadLoopAsync(stoppingToken));
        await Task.WhenAll(readers);
    }

    private async Task ReadLoopAsync(CancellationToken stoppingToken)
    {
        await foreach (var quoteId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<QuoteRater>().RateAsync(quoteId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Rating of quote {QuoteId} failed", quoteId);
            }
        }
    }

    // Quotes left in Rating by an application pool recycle are rated again on start.
    private async Task RequeueInterruptedAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QuotingDbContext>();
        var stuck = await db.Quotes.Where(q => q.Status == QuoteStatus.Rating).Select(q => q.Id).ToListAsync(ct);
        foreach (var id in stuck)
            await queue.EnqueueAsync(id, ct);
        if (stuck.Count > 0)
            logger.LogWarning("Re-queued {Count} quotes that were being rated when the application stopped", stuck.Count);
    }
}

/// <summary>Rates one quote: RateCore, platform referral rules, premium build-up and status.</summary>
public sealed class QuoteRater(
    QuotingDbContext db,
    IRatingEngine engine,
    HubNotifier notifier,
    IPublishEndpoint bus,
    TimeProvider clock,
    ILogger<QuoteRater> logger)
{
    public async Task RateAsync(Guid quoteId, CancellationToken ct)
    {
        var quote = await db.Quotes.Include(q => q.Referrals).Include(q => q.Agency)
            .FirstOrDefaultAsync(q => q.Id == quoteId, ct);
        if (quote is null || quote.Status != QuoteStatus.Rating)
            return;

        var product = ProductCatalogue.Find(quote.ProductVersion)
            ?? throw new InvalidOperationException($"Unknown product version {quote.ProductVersion}.");
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var purpose = quote.RenewalOfPolicyId is null ? RatingPurpose.NewBusiness : RatingPurpose.Renewal;

        RatingResult rating;
        try
        {
            rating = await engine.RateAsync(new RatingRequest(quote.Reference, product, quote.InceptionDate, quote.Risk, purpose), ct);
        }
        catch (Exception ex) when (ex is RatingException or TimeoutException)
        {
            quote.RatingError = ex.Message;
            quote.Move(QuoteStatus.RatingFailed, "rating", ex.Message);
            await db.SaveChangesAsync(ct);
            await NotifyAsync(quote, "Rating failed. Please try again or contact your underwriter.");
            return;
        }

        var outcome = UnderwritingOutcome.Build(rating, product, quote.Risk, quote.CommissionRate, today);

        quote.Premium = outcome.Premium;
        quote.TechnicalPremium = rating.TechnicalPremium;
        quote.GrossPremium = outcome.Premium.GrossPremium;
        quote.Total = outcome.Premium.Total;
        quote.Clauses = rating.Clauses.ToList();
        quote.RatingEngine = $"{engine.Name} {rating.EngineVersion}".Trim();
        quote.RatingTables = rating.TableVersion;
        quote.RatingError = null;
        quote.RatingMilliseconds = (int)rating.Duration.TotalMilliseconds;
        quote.ValidUntil = today.AddDays(QuoteLifecycle.ValidityDays);
        quote.Loadings = [];
        quote.Referrals.Clear();
        quote.Referrals.AddRange(outcome.Messages.Select(m => new QuoteReferral { QuoteId = quote.Id, Code = m.Code, Text = m.Text, Source = m.Source }));
        if (outcome.Status == QuoteStatus.Referred)
            quote.ReferredUtc = clock.GetUtcNow().UtcDateTime;
        quote.Move(outcome.Status, "rating", $"{rating.TableVersion}; total {Money.Format(outcome.Premium.Total)}");
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Quote {Reference} rated with {Tables}: {Status}, total {Total}",
            quote.Reference, rating.TableVersion, quote.Status, quote.Total);

        await NotifyAsync(quote, null);
        if (quote.Status == QuoteStatus.Quoted)
            await bus.Publish(new DocumentRequested { DocumentType = DocumentType.QuoteSchedule, QuoteId = quote.Id, EmailToBroker = false }, ct);
    }

    private async Task NotifyAsync(Quote quote, string? message)
    {
        await notifier.QuoteChanged(new QuoteNotice(quote.Id, quote.Reference, quote.AgencyCode, quote.Status, quote.Total, message));
        await bus.Publish(new QuoteStatusChanged
        {
            QuoteId = quote.Id,
            Reference = quote.Reference,
            AgencyCode = quote.AgencyCode,
            Status = quote.Status,
            Total = quote.Total
        });
    }
}
