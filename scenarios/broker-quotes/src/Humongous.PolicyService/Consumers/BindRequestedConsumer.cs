using Humongous.Quoting.Contracts.Messages;
using Humongous.Quoting.Data;
using Humongous.Quoting.Data.Entities;
using Humongous.Quoting.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Humongous.PolicyService.Consumers;

/// <summary>Issues the policy for a quote the broker has asked to bind.</summary>
public sealed class BindRequestedConsumer(
    QuotingDbContext db,
    NumberGenerator numbers,
    TimeProvider clock,
    ILogger<BindRequestedConsumer> logger) : IConsumer<BindRequested>
{
    public async Task Consume(ConsumeContext<BindRequested> context)
    {
        var message = context.Message;
        var quote = await db.Quotes.Include(q => q.Agency).Include(q => q.Events)
            .FirstOrDefaultAsync(q => q.Id == message.QuoteId, context.CancellationToken);
        if (quote is null)
        {
            logger.LogWarning("Bind requested for unknown quote {QuoteId}", message.QuoteId);
            return;
        }
        if (quote.Status != QuoteStatus.BindRequested)
        {
            logger.LogInformation("Quote {Reference} is {Status}; bind request ignored", quote.Reference, quote.Status);
            return;
        }

        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var problem = quote.Agency is not { Active: true } ? "The agency is not active."
            : quote.Premium is null ? "The quote has no premium."
            : quote.InceptionDate < today ? "The inception date has passed."
            : null;
        if (problem is not null)
        {
            quote.Move(quote.DecidedBy is null ? QuoteStatus.Quoted : QuoteStatus.Accepted, "policy-service", problem);
            await db.SaveChangesAsync(context.CancellationToken);
            await context.Publish(new BindFailed { QuoteId = quote.Id, AgencyCode = quote.AgencyCode, Reason = problem });
            return;
        }

        var policy = await IssueAsync(quote, message.RequestedBy, context.CancellationToken);

        await context.Publish(new PolicyIssued
        {
            PolicyId = policy.Id,
            QuoteId = quote.Id,
            PolicyNumber = policy.PolicyNumber,
            AgencyCode = policy.AgencyCode
        });
        await context.Publish(new DocumentRequested
        {
            DocumentType = DocumentType.PolicySchedule,
            PolicyId = policy.Id,
            TransactionId = policy.Transactions[0].Id,
            EmailToBroker = true,
            AdditionalRecipient = message.SendDocumentsToInsured ? message.InsuredEmail : null
        });
        if (policy.Risk.EmployersLiability is not null)
        {
            await context.Publish(new DocumentRequested
            {
                DocumentType = DocumentType.ElCertificate,
                PolicyId = policy.Id,
                EmailToBroker = true,
                AdditionalRecipient = message.SendDocumentsToInsured ? message.InsuredEmail : null
            });
        }
    }

    private async Task<Policy> IssueAsync(Quote quote, string requestedBy, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        Policy? previous = null;
        string number;
        if (quote.RenewalOfPolicyId is { } previousId)
        {
            previous = await db.Policies.FirstAsync(p => p.Id == previousId, ct);
            number = References.RenewedPolicy(previous.PolicyNumber, quote.InceptionDate);
        }
        else
        {
            number = References.Policy(await numbers.NextAsync(NumberGenerator.PolicySequence, ct), quote.InceptionDate);
        }

        var premium = quote.Premium!;
        var type = previous is null ? TransactionType.NewBusiness : TransactionType.Renewal;
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = number,
            QuoteId = quote.Id,
            AgencyCode = quote.AgencyCode,
            Status = PolicyStatus.Live,
            InceptionDate = quote.InceptionDate,
            ExpiryDate = quote.InceptionDate.AddYears(1).AddDays(-1),
            ProductVersion = quote.ProductVersion,
            Risk = quote.Risk,
            Premium = premium,
            Clauses = quote.Clauses,
            InsuredName = quote.InsuredName,
            Postcode = quote.Postcode,
            TradeCode = quote.TradeCode,
            AnnualGrossPremium = premium.GrossPremium,
            CommissionRate = quote.CommissionRate,
            PreviousPolicyId = previous?.Id,
            CreatedUtc = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        policy.Transactions.Add(new PolicyTransaction
        {
            Id = Guid.NewGuid(),
            PolicyId = policy.Id,
            Type = type,
            EffectiveDate = policy.InceptionDate,
            GrossPremium = premium.GrossPremium,
            Ipt = premium.Ipt,
            Commission = premium.Commission,
            Fee = premium.Fee,
            SectionGross = premium.Sections.ToDictionary(s => s.Section.ToCode(), s => s.Gross),
            CreatedBy = requestedBy,
            CreatedUtc = now
        });
        db.Policies.Add(policy);

        if (previous is not null)
        {
            previous.Status = PolicyStatus.Renewed;
            previous.ConcurrencyStamp = Guid.NewGuid();
        }

        quote.PolicyId = policy.Id;
        quote.Move(QuoteStatus.Bound, requestedBy, $"Policy {policy.PolicyNumber}");
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Issued {Type} policy {PolicyNumber} from quote {Reference}, gross {Gross}",
            type, policy.PolicyNumber, quote.Reference, premium.GrossPremium);
        return policy;
    }
}
