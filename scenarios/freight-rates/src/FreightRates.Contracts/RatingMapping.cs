using System.Globalization;
using FreightRates.Core;

namespace FreightRates.Contracts;

public static class RatingMapping
{
    public static int ToDateInt(DateOnly date) => date.Year * 10000 + date.Month * 100 + date.Day;

    public static DateOnly FromDateInt(int value) =>
        value <= 0 ? DateOnly.MinValue : new DateOnly(value / 10000, value / 100 % 100, value % 100);

    public static int ClassToX10(decimal freightClass) => (int)Math.Round(freightClass * 10m, MidpointRounding.AwayFromZero);

    public static decimal ClassFromX10(int value) => value / 10m;

    public static decimal FromCents(long cents) => cents / 100m;

    public static RateRequest ToRequest(ShipmentRequest shipment, string? tariffName = null)
    {
        var request = new RateRequest
        {
            OriginZip = shipment.OriginZip,
            DestinationZip = shipment.DestinationZip,
            ShipDate = ToDateInt(shipment.ShipDate),
            TariffName = tariffName ?? "",
        };
        foreach (var line in shipment.Lines)
        {
            request.Lines.Add(new RateLine
            {
                FreightClassX10 = ClassToX10(FreightClass.Resolve(line)),
                WeightLbs = line.WeightLbs,
            });
        }
        return request;
    }

    public static EngineRateResult ToResult(RateReply reply) => new()
    {
        GrossCharge = FromCents(reply.GrossChargeCents),
        TariffMinimumCharge = FromCents(reply.MinimumChargeCents),
        DeficitWeightLbs = reply.DeficitWeightLbs,
        DeficitCharge = FromCents(reply.DeficitChargeCents),
        TransitDays = reply.TransitDays,
        TariffName = reply.TariffName,
        OriginTerminal = reply.OriginTerminal,
        DestinationTerminal = reply.DestinationTerminal,
        Lines = reply.Lines
            .Select(l => new EngineLineRate(ClassFromX10(l.FreightClassX10), l.WeightLbs, l.RatePerCwtCents / 100m, FromCents(l.ChargeCents)))
            .ToList(),
    };

    public static TariffInfo ToTariffInfo(TariffInfoReply reply) => new(
        reply.TariffName,
        FromDateInt(reply.EffectiveDate),
        reply.EngineVersion,
        reply.KeySerial,
        reply.LicenceDaysLeft);

    public static string FormatDateInt(int value) =>
        FromDateInt(value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
