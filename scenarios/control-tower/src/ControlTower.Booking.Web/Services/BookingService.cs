using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Booking.Models;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Locations;
using Proseware.ControlTower.Domain.Pricing;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Integration.Messaging;

namespace Proseware.ControlTower.Booking.Services;

public sealed class BookingService
{
    private readonly ControlTowerDbContext _db;
    private readonly ShipmentNumberAllocator _numbers;
    private readonly QuoteService _quotes;
    private readonly LegPlanner _planner;
    private readonly IEventBus _bus;
    private readonly IClock _clock;
    private readonly ILogger<BookingService> _logger;

    public BookingService(ControlTowerDbContext db, ShipmentNumberAllocator numbers, QuoteService quotes, LegPlanner planner, IEventBus bus, IClock clock, ILogger<BookingService> logger)
    {
        _db = db;
        _numbers = numbers;
        _quotes = quotes;
        _planner = planner;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Shipment> CreateAsync(BookingDraft draft, string user, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var customer = await _db.Customers.FirstAsync(c => c.Id == draft.CustomerId, ct);
        var origin = await _db.Locations.FirstAsync(l => l.Id == draft.OriginId, ct);
        var destination = await _db.Locations.FirstAsync(l => l.Id == draft.DestinationId, ct);
        var network = await _db.Locations.Where(l => l.Kind == LocationKind.Seaport || l.Kind == LocationKind.Airport).ToListAsync(ct);

        var quotes = await _quotes.QuoteAsync(draft, ct);
        var quote = quotes.FirstOrDefault(q => q.RateCardId == draft.SelectedRateCardId) ?? quotes.FirstOrDefault();
        var card = quote?.RateCardId is { } rid ? await _db.RateCards.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rid, ct) : null;
        var mainCarrier = card?.CarrierId is { } mc ? await _db.Carriers.FirstOrDefaultAsync(c => c.Id == mc, ct) : null;
        var roadCarrier = await _db.Carriers.Where(c => c.Mode == TransportMode.Road && c.Active && c.Code != "PRCL")
            .OrderBy(c => c.Code == "NRDL" ? 0 : 1).FirstOrDefaultAsync(ct);

        var pickup = LocalTime.FromLocal(draft.PickupDate.Date + draft.PickupTime, origin.TimeZoneId);
        var transitDays = quote?.TransitDays ?? Math.Max(1, customer.DomesticSlaDays);
        var delivery = LocalTime.AddWorkingDays(pickup, transitDays, destination.TimeZoneId);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var shipment = new Shipment
        {
            Number = await _numbers.NextAsync(now, ct),
            CustomerId = customer.Id,
            CustomerReference = draft.CustomerReference,
            Mode = draft.Mode,
            IncotermCode = Incoterm.Parse(draft.IncotermCode).Code,
            Status = ShipmentStatus.Booked,
            OriginId = origin.Id,
            DestinationId = destination.Id,
            ShipperName = draft.ShipperName,
            ConsigneeName = draft.ConsigneeName,
            ConsigneeEmail = draft.ConsigneeEmail,
            NotifyPhone = draft.NotifyPhone,
            PlannedPickup = pickup,
            PlannedDelivery = delivery,
            Eta = delivery,
            EtaCalculatedAt = now,
            GoodsDescription = draft.GoodsDescription,
            DeclaredValue = draft.DeclaredValue,
            TemperatureMin = draft.TemperatureMin,
            TemperatureMax = draft.TemperatureMax,
            CreatedBy = user,
            CreatedAt = now,
            UpdatedAt = now,
            Origin = origin,
            Destination = destination
        };

        var serial = await NextSsccSerialAsync(ct);
        foreach (var package in QuoteService.ToPackages(draft.Packages))
        {
            package.Sscc = Sscc.Create('3', Sscc.DefaultCompanyPrefix, serial++);
            shipment.Packages.Add(package);
        }

        foreach (var leg in _planner.Plan(draft.Mode, origin, destination, network, mainCarrier, roadCarrier, pickup, transitDays))
            shipment.Legs.Add(leg);

        if (quote is not null)
            foreach (var line in quote.Lines)
                shipment.Charges.Add(new Charge { Code = line.Code, Description = line.Description, Amount = line.Amount, Currency = quote.Currency });

        if (shipment.CrossesCustomsBorder && customer.CustomsBrokerageAgreed)
            shipment.CustomsDeclarations.Add(CustomsDeclarationBuilder.Draft(shipment, customer, now));

        shipment.Milestones.Add(new Milestone { Code = MilestoneCodes.Booked, OccurredAt = now, RecordedAt = now, Source = user, LocationText = origin.City });

        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _bus.PublishAsync(new ShipmentBooked(IntegrationEvent.NewId(), now, shipment.Id, shipment.Number, customer.Id, shipment.Mode, user), ct);
        _logger.LogInformation("{User} booked {Number} for {Customer} ({Mode}, {Packages} packages)", user, shipment.Number, customer.Code, shipment.Mode, shipment.PackageCount);
        return shipment;
    }

    /// <summary>Confirms a portal booking request; the customer filled in the same data.</summary>
    public async Task ConfirmRequestAsync(int shipmentId, string user, CancellationToken ct)
    {
        var s = await _db.Shipments.Include(x => x.Milestones).FirstAsync(x => x.Id == shipmentId, ct);
        if (s.Status != ShipmentStatus.Requested) throw new InvalidOperationException($"{s.Number} is not a booking request.");
        var now = _clock.UtcNow;
        ShipmentStateMachine.Apply(s, new Milestone { Code = MilestoneCodes.Booked, OccurredAt = now, RecordedAt = now, Source = user });
        await _db.SaveChangesAsync(ct);
        await _bus.PublishAsync(new ShipmentBooked(IntegrationEvent.NewId(), now, s.Id, s.Number, s.CustomerId, s.Mode, user), ct);
    }

    private async Task<long> NextSsccSerialAsync(CancellationToken ct)
    {
        var prefix = "3" + Sscc.DefaultCompanyPrefix;
        var last = await _db.Packages.Where(p => p.Sscc.StartsWith(prefix)).OrderByDescending(p => p.Sscc).Select(p => p.Sscc).FirstOrDefaultAsync(ct);
        return last is null ? 1 : long.Parse(last.Substring(prefix.Length, 17 - prefix.Length)) + 1;
    }
}
