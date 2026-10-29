namespace Humongous.Quoting.Domain;

public sealed record Referral(string Code, string Text, string Source);

/// <summary>
/// Underwriting authority rules applied in the platform. RateCore returns its own
/// referrals (postcode, trade and table limits); these are the rules from the binding
/// authority agreement.
/// </summary>
public static class ReferralRules
{
    public static IReadOnlyList<Referral> Evaluate(RiskDetails risk, ProductVersion product, DateOnly today)
    {
        var result = new List<Referral>();
        var trade = TradeList.Find(risk.Insured.TradeCode);

        if (trade?.Acceptance == TradeAcceptance.Refer)
            result.Add(new("T01", $"Trade '{trade.Description}' is referred under the trade list.", "Rules"));

        if (risk.TotalPropertySumInsured > product.MaxTotalSumInsured)
            result.Add(new("S01", $"Total property sums insured exceed {Money.Format(product.MaxTotalSumInsured)}.", "Rules"));

        if (risk.BusinessInterruption is { IndemnityPeriodMonths: > 24 })
            result.Add(new("S02", "Indemnity period over 24 months.", "Rules"));

        if (risk.EmployersLiability is { Employees: > 50 })
            result.Add(new("S03", "More than 50 employees.", "Rules"));

        if (risk.PublicLiability is { LimitOfIndemnity: > 2_000_000m } && risk.Insured.Turnover > 2_000_000m)
            result.Add(new("S04", "5m limit with turnover over 2m.", "Rules"));

        var fiveYearsAgo = today.AddYears(-5);
        var recentClaims = risk.Claims.Where(c => c.Date >= fiveYearsAgo).ToList();
        if (recentClaims.Count > 2)
            result.Add(new("C01", $"{recentClaims.Count} claims in the last five years.", "Rules"));
        if (recentClaims.Any(c => c.Amount > 25_000m))
            result.Add(new("C02", "A claim over 25,000 in the last five years.", "Rules"));
        if (recentClaims.Any(c => c.Open))
            result.Add(new("C03", "Open claim.", "Rules"));

        if (!risk.Premises.Occupied)
            result.Add(new("P01", "Premises unoccupied.", "Rules"));
        if (risk.Premises.Construction == ConstructionType.NonStandard || risk.Premises.Roof == RoofType.Thatch)
            result.Add(new("P02", "Non-standard construction or thatched roof.", "Rules"));
        if (risk.Premises.ListedBuilding && risk.MaterialDamage is { Buildings: > 0 })
            result.Add(new("P03", "Listed building with buildings cover.", "Rules"));
        if (risk.Premises.FloodInLast10Years)
            result.Add(new("P04", "Flood at the premises in the last 10 years.", "Rules"));
        if (risk.MaterialDamage is { Stock: > 100_000m } && risk.Premises.Alarm != AlarmType.RemoteSignalling)
            result.Add(new("P05", "Stock over 100,000 without a remote signalling alarm.", "Rules"));

        if (risk.Insured.YearsTrading < 1 && risk.Insured.Turnover > 1_000_000m)
            result.Add(new("B01", "New business with turnover over 1m.", "Rules"));

        if (risk.PublicLiability is { WorkAwayFromPremises: true } && trade is { WorkAwayAllowed: false })
            result.Add(new("B02", "Work away from premises is not normal for this trade.", "Rules"));

        return result;
    }

    public static IReadOnlyList<Referral> Declines(RiskDetails risk)
    {
        var result = new List<Referral>();
        var trade = TradeList.Find(risk.Insured.TradeCode);
        if (trade?.Acceptance == TradeAcceptance.Decline)
            result.Add(new("D01", $"Trade '{trade.Description}' is outside the binding authority.", "Rules"));
        if (risk.PreviouslyDeclinedOrCancelled)
            result.Add(new("D02", "Insurance previously declined or cancelled.", "Rules"));
        if (risk.EmployersLiability is not null && trade is { ElAllowed: false })
            result.Add(new("D03", "Employers' liability is not available for this trade.", "Rules"));
        return result;
    }
}
