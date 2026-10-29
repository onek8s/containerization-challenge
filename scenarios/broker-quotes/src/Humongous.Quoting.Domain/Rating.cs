namespace Humongous.Quoting.Domain;

public enum RatingPurpose
{
    NewBusiness,
    MidTermAdjustment,
    Renewal
}

public sealed record RatingRequest(
    string QuoteReference,
    ProductVersion Product,
    DateOnly EffectiveDate,
    RiskDetails Risk,
    RatingPurpose Purpose = RatingPurpose.NewBusiness);

public sealed record SectionRating(
    SectionCode Section,
    decimal Premium,
    decimal Rate,
    string Basis);

public sealed record RatingMessage(string Code, string Text);

public sealed record RatingResult
{
    public string EngineVersion { get; init; } = "";
    public string TableVersion { get; init; } = "";
    public IReadOnlyList<SectionRating> Sections { get; init; } = [];
    public IReadOnlyList<RatingMessage> Referrals { get; init; } = [];
    public IReadOnlyList<RatingMessage> Declines { get; init; } = [];
    public IReadOnlyList<string> Clauses { get; init; } = [];
    public TimeSpan Duration { get; init; }

    public decimal TechnicalPremium => Sections.Sum(s => s.Premium);
}

/// <summary>Rates a risk against the product's rating tables.</summary>
public interface IRatingEngine
{
    string Name { get; }

    Task<RatingResult> RateAsync(RatingRequest request, CancellationToken cancellationToken = default);
}

public class RatingException : Exception
{
    public RatingException(string message, int errorCode = 0, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }

    public int ErrorCode { get; }
}
