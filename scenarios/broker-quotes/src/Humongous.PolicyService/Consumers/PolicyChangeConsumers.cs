using Humongous.Quoting.Contracts.Messages;
using Humongous.Quoting.Data;
using Humongous.Quoting.Data.Entities;
using Humongous.Quoting.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Humongous.PolicyService.Consumers;

/// <summary>Mid-term adjustments: re-rates the changed risk and charges or returns the pro rata difference.</summary>
public sealed class AdjustmentRequestedConsumer(
    QuotingDbContext db,
    IRatingEngine engine,
    TimeProvider clock,
    ILogger<AdjustmentRequestedConsumer> logger) : IConsumer<AdjustmentRequested>
{
    public async Task Consume(ConsumeContext<AdjustmentRequested> context)
    {
        var m = context.Message;
        var policy = await db.Policies.Include(p => p.Transactions).FirstOrDefaultAsync(p => p.Id == m.PolicyId, context.CancellationToken);
        if (policy is null || policy.Status != PolicyStatus.Live)
        {
            logger.LogWarning("Adjustment for policy {PolicyId} ignored: policy not live", m.PolicyId);
            return;
        }

        var product = ProductCatalogue.Find(policy.ProductVersion)!;
        var rating = await engine.RateAsync(
            new RatingRequest(policy.PolicyNumber, product, m.EffectiveDate, m.Risk, RatingPurpose.MidTermAdjustment),
            context.CancellationToken);
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var outcome = UnderwritingOutcome.Build(rating, product, m.Risk, policy.CommissionRate, today);

        if (outcome.Status != QuoteStatus.Quoted)
        {
            var reasons = string.Join("; ", outcome.Messages.Select(r => $"{r.Code} {r.Text}"));
            logger.LogInformation("Adjustment on {PolicyNumber} needs an underwriter: {Reasons}", policy.PolicyNumber, reasons);
            await context.Publish(new AdjustmentProcessed
            {
                PolicyId = policy.Id,
                AgencyCode = policy.AgencyCode,
                Succeeded = false,
                Referred = true,
                Message = $"The change has been referred to an underwriter: {reasons}"
            });
            return;
        }

        var newPremium = outcome.Premium with { Fee = 0m, Total = outcome.Premium.GrossPremium + outcome.Premium.Ipt };
        var additional = ProRata.Adjustment(policy.AnnualGrossPremium, newPremium.GrossPremium, policy.InceptionDate, policy.ExpiryDate, m.EffectiveDate);
        var factor = ProRata.Factor(policy.InceptionDate, policy.ExpiryDate, m.EffectiveDate);
        var oldSections = policy.Premium.Sections.ToDictionary(s => s.Section, s => s.Gross);

        var transaction = new PolicyTransaction
        {
            Id = Guid.NewGuid(),
            PolicyId = policy.Id,
            Type = TransactionType.MidTermAdjustment,
            EffectiveDate = m.EffectiveDate,
            GrossPremium = additional,
            Ipt = Money.Round(additional * product.IptRate),
            Commission = Money.Round(additional * policy.CommissionRate),
            SectionGross = newPremium.Sections.ToDictionary(
                s => s.Section.ToCode(),
                s => Money.Round((s.Gross - oldSections.GetValueOrDefault(s.Section)) * factor)),
            Reason = m.Reason,
            CreatedBy = m.RequestedBy,
            CreatedUtc = clock.GetUtcNow().UtcDateTime
        };
        db.PolicyTransactions.Add(transaction);

        policy.Risk = m.Risk;
        policy.InsuredName = m.Risk.Insured.Name;
        policy.Postcode = Postcode.Normalise(m.Risk.Insured.Postcode);
        policy.Premium = newPremium;
        policy.AnnualGrossPremium = newPremium.GrossPremium;
        policy.Clauses = rating.Clauses.ToList();
        policy.ConcurrencyStamp = Guid.NewGuid();
        await db.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("MTA on {PolicyNumber} effective {Effective}: {Additional}", policy.PolicyNumber, m.EffectiveDate, additional);
        await context.Publish(new AdjustmentProcessed
        {
            PolicyId = policy.Id,
            TransactionId = transaction.Id,
            AgencyCode = policy.AgencyCode,
            Succeeded = true,
            AdditionalPremium = additional
        });
        await context.Publish(new DocumentRequested
        {
            DocumentType = DocumentType.AdjustmentEndorsement,
            PolicyId = policy.Id,
            TransactionId = transaction.Id
        });
    }
}

public sealed class CancellationRequestedConsumer(
    QuotingDbContext db,
    TimeProvider clock,
    ILogger<CancellationRequestedConsumer> logger) : IConsumer<CancellationRequested>
{
    public async Task Consume(ConsumeContext<CancellationRequested> context)
    {
        var m = context.Message;
        var policy = await db.Policies.FirstOrDefaultAsync(p => p.Id == m.PolicyId, context.CancellationToken);
        if (policy is null || policy.Status != PolicyStatus.Live)
            return;

        var product = ProductCatalogue.Find(policy.ProductVersion)!;
        var returned = ProRata.CancellationReturn(policy.AnnualGrossPremium, policy.InceptionDate, policy.ExpiryDate, m.EffectiveDate);
        var share = policy.AnnualGrossPremium == 0 ? 0 : returned / policy.AnnualGrossPremium;
        var transaction = new PolicyTransaction
        {
            Id = Guid.NewGuid(),
            PolicyId = policy.Id,
            Type = TransactionType.Cancellation,
            EffectiveDate = m.EffectiveDate,
            GrossPremium = -returned,
            Ipt = -Money.Round(returned * product.IptRate),
            Commission = -Money.Round(returned * policy.CommissionRate),
            SectionGross = policy.Premium.Sections.ToDictionary(s => s.Section.ToCode(), s => -Money.Round(s.Gross * share)),
            Reason = m.Reason,
            CreatedBy = m.RequestedBy,
            CreatedUtc = clock.GetUtcNow().UtcDateTime
        };
        db.PolicyTransactions.Add(transaction);
        policy.Status = PolicyStatus.Cancelled;
        policy.ExpiryDate = m.EffectiveDate;
        policy.ConcurrencyStamp = Guid.NewGuid();
        await db.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Cancelled {PolicyNumber} from {Effective}, return premium {Return}", policy.PolicyNumber, m.EffectiveDate, returned);
        await context.Publish(new PolicyCancelled { PolicyId = policy.Id, TransactionId = transaction.Id, AgencyCode = policy.AgencyCode, ReturnPremium = returned });
        await context.Publish(new DocumentRequested { DocumentType = DocumentType.CancellationNotice, PolicyId = policy.Id, TransactionId = transaction.Id });
    }
}
