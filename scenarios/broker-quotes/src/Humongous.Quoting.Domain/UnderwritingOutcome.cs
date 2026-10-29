namespace Humongous.Quoting.Domain;

/// <summary>Status and premium of a rated risk after the platform rules and the RateCore messages.</summary>
public sealed record UnderwritingOutcome(QuoteStatus Status, PremiumBreakdown Premium, IReadOnlyList<Referral> Messages)
{
    public static UnderwritingOutcome Build(RatingResult rating, ProductVersion product, RiskDetails risk, decimal commission, DateOnly today)
    {
        var premium = PremiumCalculator.Calculate(rating, product, commission);
        var declines = ReferralRules.Declines(risk)
            .Concat(rating.Declines.Select(d => new Referral(d.Code, d.Text, "RateCore")))
            .ToList();
        if (declines.Count > 0)
            return new UnderwritingOutcome(QuoteStatus.Declined, premium, declines);

        var referrals = ReferralRules.Evaluate(risk, product, today)
            .Concat(rating.Referrals.Select(r => new Referral(r.Code, r.Text, "RateCore")))
            .ToList();
        return new UnderwritingOutcome(referrals.Count > 0 ? QuoteStatus.Referred : QuoteStatus.Quoted, premium, referrals);
    }
}
