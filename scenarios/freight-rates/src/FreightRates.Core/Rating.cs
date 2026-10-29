namespace FreightRates.Core;

public interface IRatingEngine
{
    Task<EngineRateResult> RateAsync(ShipmentRequest shipment, CancellationToken cancellationToken = default);
    Task<TariffInfo> GetTariffInfoAsync(CancellationToken cancellationToken = default);
}

public sealed record EngineLineRate(decimal FreightClass, int WeightLbs, decimal RatePerCwt, decimal Charge);

public sealed record EngineRateResult
{
    public decimal GrossCharge { get; init; }
    public decimal TariffMinimumCharge { get; init; }
    public int DeficitWeightLbs { get; init; }
    public decimal DeficitCharge { get; init; }
    public int TransitDays { get; init; }
    public string TariffName { get; init; } = "";
    public string OriginTerminal { get; init; } = "";
    public string DestinationTerminal { get; init; } = "";
    public IReadOnlyList<EngineLineRate> Lines { get; init; } = Array.Empty<EngineLineRate>();
}

public sealed record TariffInfo(string TariffName, DateOnly EffectiveDate, string EngineVersion, string KeySerial, int LicenceDaysLeft);

public enum RatingErrorKind
{
    InvalidShipment,
    PointNotServed,
    EngineUnavailable,
}

public sealed class RatingException : Exception
{
    public RatingException(RatingErrorKind kind, int code, string message)
        : base(message)
    {
        Kind = kind;
        Code = code;
    }

    public RatingErrorKind Kind { get; }
    public int Code { get; }
}
