using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Integration.Messaging;

namespace Proseware.ControlTower.Portal.Services;

public sealed class BookingRequest
{
    public int OriginId { get; set; }
    public int DestinationId { get; set; }
    public TransportMode Mode { get; set; } = TransportMode.Road;
    public string IncotermCode { get; set; } = "DAP";
    public string? CustomerReference { get; set; }
    public string ConsigneeName { get; set; } = "";
    public string? ConsigneeEmail { get; set; }
    public string? NotifyPhone { get; set; }
    public DateTime PickupDate { get; set; } = DateTime.Today.AddDays(1);
    public string GoodsDescription { get; set; } = "";
    public PackageKind PackageKind { get; set; } = PackageKind.Pallet;
    public int PackageCount { get; set; } = 1;
    public decimal WeightPerPackageKg { get; set; }
    public int LengthCm { get; set; } = 120;
    public int WidthCm { get; set; } = 80;
    public int HeightCm { get; set; } = 120;
    public bool Stackable { get; set; } = true;
    public string? Remarks { get; set; }
}

/// <summary>Booking requests from customers. Customer service completes and confirms them in Booking.</summary>
public sealed class BookingRequestService
{
    private readonly ControlTowerDbContext _db;
    private readonly ShipmentNumberAllocator _numbers;
    private readonly IEventBus _bus;
    private readonly ILogger<BookingRequestService> _logger;

    public BookingRequestService(ControlTowerDbContext db, ShipmentNumberAllocator numbers, IEventBus bus, ILogger<BookingRequestService> logger)
    {
        _db = db;
        _numbers = numbers;
        _bus = bus;
        _logger = logger;
    }

    public IReadOnlyList<string> Validate(BookingRequest r)
    {
        var errors = new List<string>();
        if (r.OriginId == r.DestinationId) errors.Add("Pickup and delivery locations must differ.");
        if (!Incoterm.TryParse(r.IncotermCode, out var term)) errors.Add("Unknown delivery terms.");
        else if (!term.IsValidFor(r.Mode)) errors.Add($"{term.Code} can only be used for sea freight.");
        if (string.IsNullOrWhiteSpace(r.ConsigneeName)) errors.Add("Consignee is required.");
        if (string.IsNullOrWhiteSpace(r.GoodsDescription)) errors.Add("Describe the goods.");
        if (r.PickupDate.Date < DateTime.Today) errors.Add("Pickup date is in the past.");
        if (r.PackageCount is < 1 or > 99) errors.Add("Number of packages must be 1-99.");
        if (r.WeightPerPackageKg <= 0) errors.Add("Weight per package is required.");
        if (r.PackageKind == PackageKind.Parcel && r.WeightPerPackageKg > 31.5m) errors.Add("Parcels can weigh at most 31.5 kg.");
        return errors;
    }

    public async Task<Shipment> CreateAsync(int customerId, BookingRequest r, string user, CancellationToken ct)
    {
        var customer = await _db.Customers.FirstAsync(c => c.Id == customerId, ct);
        var origin = await _db.Locations.FirstAsync(l => l.Id == r.OriginId, ct);
        var destination = await _db.Locations.FirstAsync(l => l.Id == r.DestinationId, ct);
        var now = DateTimeOffset.UtcNow;
        var pickup = LocalTime.FromLocal(r.PickupDate.Date.AddHours(9), origin.TimeZoneId);

        var shipment = new Shipment
        {
            Number = await _numbers.NextAsync(now, ct),
            CustomerId = customer.Id,
            CustomerReference = r.CustomerReference,
            Mode = r.Mode,
            IncotermCode = Incoterm.Parse(r.IncotermCode).Code,
            Status = ShipmentStatus.Requested,
            OriginId = origin.Id,
            DestinationId = destination.Id,
            ShipperName = origin.CustomerId == customer.Id ? customer.Name : origin.Name,
            ConsigneeName = r.ConsigneeName,
            ConsigneeEmail = r.ConsigneeEmail,
            NotifyPhone = r.NotifyPhone,
            PlannedPickup = pickup,
            PlannedDelivery = LocalTime.AddWorkingDays(pickup, customer.DomesticSlaDays, destination.TimeZoneId),
            GoodsDescription = r.GoodsDescription,
            CreatedBy = "portal:" + user,
            CreatedAt = now,
            UpdatedAt = now
        };
        var prefix = "3" + Sscc.DefaultCompanyPrefix;
        var last = await _db.Packages.Where(p => p.Sscc.StartsWith(prefix)).OrderByDescending(p => p.Sscc).Select(p => p.Sscc).FirstOrDefaultAsync(ct);
        var serial = last is null ? 1 : long.Parse(last.Substring(prefix.Length, 17 - prefix.Length)) + 1;
        for (var i = 0; i < r.PackageCount; i++)
        {
            shipment.Packages.Add(new Package
            {
                Sscc = Sscc.Create('3', Sscc.DefaultCompanyPrefix, serial++),
                Kind = r.PackageKind,
                WeightKg = r.WeightPerPackageKg,
                LengthCm = r.LengthCm,
                WidthCm = r.WidthCm,
                HeightCm = r.HeightCm,
                Stackable = r.Stackable
            });
        }
        shipment.Milestones.Add(new Milestone
        {
            Code = MilestoneCodes.PickupPlanned, OccurredAt = now, RecordedAt = now, Source = "portal:" + user,
            Remarks = string.IsNullOrWhiteSpace(r.Remarks) ? "Booking request" : "Booking request: " + r.Remarks, VisibleToCustomer = true
        });
        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync(ct);

        await _bus.PublishAsync(new ShipmentChanged(IntegrationEvent.NewId(), now, shipment.Id, shipment.Number, "requested", "portal:" + user), ct);
        _logger.LogInformation("Booking request {Number} from {Customer} by {User}", shipment.Number, customer.Code, user);
        return shipment;
    }
}
