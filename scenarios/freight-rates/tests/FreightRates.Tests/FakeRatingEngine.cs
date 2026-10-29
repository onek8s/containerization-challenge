using FreightRates.Core;

namespace FreightRates.Tests;

public sealed class FakeRatingEngine : IRatingEngine
{
    public decimal RatePerCwt { get; set; } = 50m;
    public decimal MinimumCharge { get; set; } = 120m;
    public RatingException? Failure { get; set; }
    public List<ShipmentRequest> Requests { get; } = new();

    public Task<EngineRateResult> RateAsync(ShipmentRequest shipment, CancellationToken cancellationToken = default)
    {
        Requests.Add(shipment);
        if (Failure is not null)
            throw Failure;

        var lines = shipment.Lines
            .Select(l => new EngineLineRate(FreightClass.Resolve(l), l.WeightLbs, RatePerCwt, RatePerCwt * l.WeightLbs / 100m))
            .ToList();
        return Task.FromResult(new EngineRateResult
        {
            GrossCharge = lines.Sum(l => l.Charge),
            TariffMinimumCharge = MinimumCharge,
            TransitDays = 3,
            TariffName = "TEST0001",
            OriginTerminal = "CHI",
            DestinationTerminal = "ATL",
            Lines = lines,
        });
    }

    public Task<TariffInfo> GetTariffInfoAsync(CancellationToken cancellationToken = default) =>
        Failure is not null
            ? Task.FromException<TariffInfo>(Failure)
            : Task.FromResult(new TariffInfo("TEST0001", new DateOnly(2026, 7, 1), "test", "K-0001", 300));
}
