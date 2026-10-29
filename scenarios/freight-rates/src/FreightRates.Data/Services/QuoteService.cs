using System.Text.Json;
using FreightRates.Core;
using FreightRates.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreightRates.Data.Services;

public sealed class QuoteValidationException : Exception
{
    public QuoteValidationException(IReadOnlyList<string> errors)
        : base(string.Join(" ", errors)) => Errors = errors;

    public IReadOnlyList<string> Errors { get; }
}

public class QuoteService
{
    public const int QuoteValidDays = 30;

    private readonly FreightRatesContext _db;
    private readonly IRatingEngine _engine;
    private readonly TimeProvider _time;

    public QuoteService(FreightRatesContext db, IRatingEngine engine, TimeProvider time)
    {
        _db = db;
        _engine = engine;
        _time = time;
    }

    public async Task<Quote> CreateQuoteAsync(Customer customer, ShipmentRequest shipment, string? customerReference, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        var errors = ShipmentValidator.Validate(shipment, today).ToList();

        var codes = shipment.Accessorials.Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();
        var accessorials = await _db.Accessorials.AsNoTracking()
            .Where(a => codes.Contains(a.Code) && a.Active)
            .ToListAsync(cancellationToken);
        errors.AddRange(codes.Except(accessorials.Select(a => a.Code)).Select(c => $"Unknown accessorial code {c}."));

        if (errors.Count > 0)
            throw new QuoteValidationException(errors);

        var rate = await _engine.RateAsync(shipment, cancellationToken);
        var fuelPercent = await FuelSurchargePercentAsync(shipment.ShipDate, cancellationToken);

        var breakdown = QuoteCalculator.Calculate(
            rate,
            new CustomerPricing(customer.DiscountPercent, customer.MinimumCharge, customer.FuelSurchargeOverridePercent),
            fuelPercent,
            shipment.TotalWeightLbs,
            accessorials.Select(a => new AccessorialRate(a.Code, a.Description, a.Amount, a.PerCwt)));

        var quote = new Quote
        {
            CustomerId = customer.Id,
            CustomerReference = customerReference,
            OriginZip = shipment.OriginZip,
            DestinationZip = shipment.DestinationZip,
            ShipDate = shipment.ShipDate,
            TotalWeightLbs = shipment.TotalWeightLbs,
            LinesJson = JsonSerializer.Serialize(rate.Lines),
            AccessorialCodes = string.Join(',', codes),
            GrossCharge = breakdown.GrossCharge,
            Discount = breakdown.Discount,
            NetLinehaul = breakdown.NetLinehaul,
            MinimumApplied = breakdown.MinimumApplied,
            FuelSurchargePercent = breakdown.FuelSurchargePercent,
            FuelSurcharge = breakdown.FuelSurcharge,
            AccessorialTotal = breakdown.AccessorialTotal,
            Total = breakdown.Total,
            TariffName = rate.TariffName,
            TransitDays = rate.TransitDays,
            OriginTerminal = rate.OriginTerminal,
            DestinationTerminal = rate.DestinationTerminal,
            ValidUntil = today.AddDays(QuoteValidDays),
            CreatedUtc = _time.GetUtcNow().UtcDateTime,
        };

        for (var attempt = 1; ; attempt++)
        {
            quote.Number = await NextNumberAsync(today, cancellationToken);
            _db.Quotes.Add(quote);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return quote;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                _db.Entry(quote).State = EntityState.Detached;
            }
        }
    }

    public async Task<decimal> FuelSurchargePercentAsync(DateOnly shipDate, CancellationToken cancellationToken = default)
    {
        var week = FuelSurcharge.WeekOf(shipDate);
        var price = await _db.FuelPrices.AsNoTracking()
            .Where(p => p.WeekStarting <= week)
            .OrderByDescending(p => p.WeekStarting)
            .Select(p => (decimal?)p.DieselPrice)
            .FirstOrDefaultAsync(cancellationToken);
        if (price is null)
            return 0m;

        var steps = await _db.FuelSurchargeSteps.AsNoTracking().ToListAsync(cancellationToken);
        return FuelSurcharge.PercentFor(price.Value, steps.Select(s => new Core.FuelSurchargeStep(s.DieselPriceFrom, s.Percent)));
    }

    public Task<Quote?> FindAsync(int customerId, string number, CancellationToken cancellationToken = default) =>
        _db.Quotes.AsNoTracking().FirstOrDefaultAsync(q => q.CustomerId == customerId && q.Number == number, cancellationToken);

    public Task<List<Quote>> RecentAsync(int customerId, int take, CancellationToken cancellationToken = default) =>
        _db.Quotes.AsNoTracking()
            .Where(q => q.CustomerId == customerId)
            .OrderByDescending(q => q.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    private async Task<string> NextNumberAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var prefix = $"Q{today:yyMMdd}-";
        var last = await _db.Quotes.AsNoTracking()
            .Where(q => q.Number.StartsWith(prefix))
            .OrderByDescending(q => q.Number)
            .Select(q => q.Number)
            .FirstOrDefaultAsync(cancellationToken);
        var next = last is null ? 1 : int.Parse(last[prefix.Length..]) + 1;
        return $"{prefix}{next:D4}";
    }
}
