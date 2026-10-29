using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Booking.Models;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain.Pricing;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Booking.Services;

public sealed class QuoteService
{
    private readonly ControlTowerDbContext _db;
    private readonly QuoteCalculator _calculator;

    public QuoteService(ControlTowerDbContext db, QuoteCalculator calculator)
    {
        _db = db;
        _calculator = calculator;
    }

    public async Task<IReadOnlyList<QuoteOption>> QuoteAsync(BookingDraft draft, CancellationToken ct)
    {
        if (draft.OriginId is null || draft.DestinationId is null || draft.Packages.Count == 0) return Array.Empty<QuoteOption>();
        var origin = await _db.Locations.AsNoTracking().FirstAsync(l => l.Id == draft.OriginId, ct);
        var destination = await _db.Locations.AsNoTracking().FirstAsync(l => l.Id == draft.DestinationId, ct);
        var customer = draft.CustomerId is { } cid ? await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid, ct) : null;
        var cards = await _db.RateCards.AsNoTracking().Include(r => r.Breaks)
            .Where(r => r.Mode == draft.Mode && r.OriginCountry == origin.CountryCode && r.DestinationCountry == destination.CountryCode)
            .ToListAsync(ct);
        var carriers = await _db.Carriers.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var request = new QuoteRequest(
            draft.Mode, origin.CountryCode, origin.PostalCode, destination.CountryCode,
            ToPackages(draft.Packages), draft.PickupDate.Date,
            draft.CustomsClearance || origin.IsEuCustomsTerritory != destination.IsEuCustomsTerritory,
            draft.Insurance ? draft.DeclaredValue : null,
            customer?.DiscountPercent ?? 0m);

        return _calculator.Calculate(request, cards, draft.IsReefer)
            .Select(q => new QuoteOption(q.RateCardId, q.CarrierId is { } id && carriers.TryGetValue(id, out var n) ? n : "Proseware network",
                q.ChargeableKg, q.TransitDays, q.Total, q.Currency, q.Lines))
            .ToList();
    }

    public static List<Package> ToPackages(IEnumerable<PackageLine> lines) => lines.Select(l => new Package
    {
        Kind = l.Kind,
        Quantity = l.Quantity,
        WeightKg = l.WeightKg,
        LengthCm = l.LengthCm,
        WidthCm = l.WidthCm,
        HeightCm = l.HeightCm,
        Stackable = l.Stackable,
        Marks = l.Marks,
        DangerousGoods = l.HasDangerousGoods
            ? new DangerousGoods
            {
                UnNumber = l.DgUnNumber!,
                ProperShippingName = l.DgShippingName ?? "",
                HazardClass = l.DgClass ?? "",
                PackingGroup = l.DgPackingGroup,
                NetQuantity = l.DgNetQuantity ?? 0,
                LimitedQuantity = l.DgLimitedQuantity
            }
            : null
    }).ToList();
}
