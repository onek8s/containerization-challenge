using System.Text.Json;
using FreightRates.Core;
using FreightRates.Data.Entities;

namespace FreightRates.Api.Endpoints;

public sealed record QuoteLineRequest(decimal? FreightClass, int WeightLbs, int Pieces = 1, string? Description = null,
    decimal? LengthIn = null, decimal? WidthIn = null, decimal? HeightIn = null);

public sealed record QuoteRequest(
    string OriginZip,
    string DestinationZip,
    DateOnly ShipDate,
    IReadOnlyList<QuoteLineRequest> Lines,
    IReadOnlyList<string>? Accessorials = null,
    string? CustomerReference = null)
{
    public ShipmentRequest ToShipment() => new()
    {
        OriginZip = OriginZip?.Trim() ?? "",
        DestinationZip = DestinationZip?.Trim() ?? "",
        ShipDate = ShipDate,
        Lines = (Lines ?? Array.Empty<QuoteLineRequest>())
            .Select(l => new ShipmentLine
            {
                FreightClass = l.FreightClass,
                WeightLbs = l.WeightLbs,
                Pieces = l.Pieces,
                Description = l.Description,
                LengthIn = l.LengthIn,
                WidthIn = l.WidthIn,
                HeightIn = l.HeightIn,
            })
            .ToList(),
        Accessorials = Accessorials ?? Array.Empty<string>(),
    };
}

public sealed record QuoteLineResponse(decimal FreightClass, int WeightLbs, decimal RatePerCwt, decimal Charge);

public sealed record QuoteResponse(
    string QuoteNumber,
    string? CustomerReference,
    string OriginZip,
    string DestinationZip,
    DateOnly ShipDate,
    DateOnly ValidUntil,
    int TotalWeightLbs,
    IReadOnlyList<QuoteLineResponse> Lines,
    decimal GrossCharge,
    decimal Discount,
    decimal NetLinehaul,
    bool MinimumApplied,
    decimal FuelSurchargePercent,
    decimal FuelSurcharge,
    IReadOnlyList<string> Accessorials,
    decimal AccessorialTotal,
    decimal Total,
    string Tariff,
    int TransitDays,
    string OriginTerminal,
    string DestinationTerminal)
{
    public static QuoteResponse From(Quote q)
    {
        var lines = JsonSerializer.Deserialize<List<EngineLineRate>>(q.LinesJson) ?? new();
        return new QuoteResponse(
            q.Number, q.CustomerReference, q.OriginZip, q.DestinationZip, q.ShipDate, q.ValidUntil, q.TotalWeightLbs,
            lines.Select(l => new QuoteLineResponse(l.FreightClass, l.WeightLbs, l.RatePerCwt, l.Charge)).ToList(),
            q.GrossCharge, q.Discount, q.NetLinehaul, q.MinimumApplied, q.FuelSurchargePercent, q.FuelSurcharge,
            q.AccessorialCodes.Split(',', StringSplitOptions.RemoveEmptyEntries), q.AccessorialTotal, q.Total,
            q.TariffName, q.TransitDays, q.OriginTerminal, q.DestinationTerminal);
    }
}
