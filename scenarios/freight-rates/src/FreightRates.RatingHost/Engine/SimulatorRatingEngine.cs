using FreightRates.Core;

namespace FreightRates.RatingHost.Engine;

/// <summary>
/// Returns fixed test rates without the RateEngine SDK, for development and integration testing.
/// Rates are not tariff rates and must not be quoted to customers.
/// </summary>
public sealed class SimulatorRatingEngine : IHostedRatingEngine
{
    public const string TariffName = "SIMULATOR";

    public Task<EngineRateResult> RateAsync(ShipmentRequest shipment, CancellationToken cancellationToken = default)
    {
        foreach (var zip in new[] { shipment.OriginZip, shipment.DestinationZip })
        {
            if (zip.StartsWith("00", StringComparison.Ordinal) || zip.StartsWith("999", StringComparison.Ordinal))
                throw new RatingException(RatingErrorKind.PointNotServed, -303, $"Point not served: {zip}");
        }

        var zone = Math.Abs(int.Parse(shipment.OriginZip[..3]) - int.Parse(shipment.DestinationZip[..3])) / 100 + 1;
        var lines = shipment.Lines
            .Select(l =>
            {
                var cls = FreightClass.Resolve(l);
                var ratePerCwt = Math.Round((18m + zone * 6.5m) * cls / 100m, 2);
                var charge = Math.Round(ratePerCwt * l.WeightLbs / 100m, 2);
                return new EngineLineRate(cls, l.WeightLbs, ratePerCwt, charge);
            })
            .ToList();

        return Task.FromResult(new EngineRateResult
        {
            GrossCharge = lines.Sum(l => l.Charge),
            TariffMinimumCharge = 95m,
            TransitDays = Math.Min(1 + zone, 7),
            TariffName = TariffName,
            OriginTerminal = "SIM" + shipment.OriginZip[..3],
            DestinationTerminal = "SIM" + shipment.DestinationZip[..3],
            Lines = lines,
        });
    }

    public Task<TariffInfo> GetTariffInfoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new TariffInfo(TariffName, new DateOnly(2026, 1, 1), "simulator", "", 0));

    public Task<TariffInfo> ReloadTariffsAsync(CancellationToken cancellationToken = default) =>
        GetTariffInfoAsync(cancellationToken);

    public Task<KeyStatus> GetKeyStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new KeyStatus(false, "", 0, 0, 0));
}
