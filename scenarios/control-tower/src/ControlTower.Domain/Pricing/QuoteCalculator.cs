using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Domain.Pricing;

public sealed record QuoteRequest(
    TransportMode Mode,
    string OriginCountry,
    string OriginPostalCode,
    string DestinationCountry,
    IReadOnlyList<Package> Packages,
    DateTime ShippingDate,
    bool CustomsClearance,
    decimal? InsuredValue,
    decimal CustomerDiscountPercent = 0m);

public sealed record QuoteLine(string Code, string Description, decimal Amount);

public sealed record Quote(int? RateCardId, int? CarrierId, string Currency, decimal ChargeableKg, int TransitDays, IReadOnlyList<QuoteLine> Lines)
{
    public decimal Total => Lines.Sum(l => l.Amount);
}

public sealed class QuoteCalculator
{
    public const decimal DangerousGoodsSurcharge = 45m;
    public const decimal ReeferSurchargePercent = 18m;
    public const decimal CustomsClearanceFee = 65m;
    public const decimal InsurancePercent = 0.3m;
    public const decimal InsuranceMinimum = 15m;

    /// <summary>Returns quotes from all matching rate cards, cheapest first.</summary>
    public IReadOnlyList<Quote> Calculate(QuoteRequest request, IEnumerable<RateCard> rateCards, bool reefer = false)
    {
        var chargeable = ChargeableWeight.For(request.Mode, request.Packages);
        var quotes = new List<Quote>();
        foreach (var card in rateCards.Where(c => c.AppliesTo(request.Mode, request.OriginCountry, request.DestinationCountry, request.OriginPostalCode, request.ShippingDate)))
        {
            var rateBreak = card.Breaks.Where(b => b.FromKg <= chargeable).OrderByDescending(b => b.FromKg).FirstOrDefault();
            if (rateBreak is null) continue;

            var lines = new List<QuoteLine>();
            var freight = Math.Max(card.MinimumCharge, chargeable / 100m * rateBreak.PricePer100Kg);
            if (request.CustomerDiscountPercent > 0) freight *= 1 - request.CustomerDiscountPercent / 100m;
            lines.Add(new QuoteLine(ChargeCodes.Freight, $"Freight {request.OriginCountry}-{request.DestinationCountry}, {chargeable:0.#} kg", Round(freight)));
            if (card.FuelSurchargePercent > 0)
                lines.Add(new QuoteLine(ChargeCodes.Fuel, $"Fuel surcharge {card.FuelSurchargePercent:0.##} %", Round(freight * card.FuelSurchargePercent / 100m)));
            if (request.Packages.Any(p => p.DangerousGoods is { LimitedQuantity: false }))
                lines.Add(new QuoteLine(ChargeCodes.DangerousGoods, "Dangerous goods handling", DangerousGoodsSurcharge));
            if (reefer)
                lines.Add(new QuoteLine(ChargeCodes.Reefer, "Temperature controlled", Round(freight * ReeferSurchargePercent / 100m)));
            if (request.CustomsClearance)
                lines.Add(new QuoteLine(ChargeCodes.Customs, "Customs declaration", CustomsClearanceFee));
            if (request.InsuredValue is > 0)
                lines.Add(new QuoteLine(ChargeCodes.Insurance, "Cargo insurance", Round(Math.Max(InsuranceMinimum, request.InsuredValue.Value * InsurancePercent / 100m))));

            quotes.Add(new Quote(card.Id, card.CarrierId, card.Currency, chargeable, card.TransitDays, lines));
        }
        return quotes.OrderBy(q => q.Total).ThenBy(q => q.TransitDays).ToList();
    }

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
