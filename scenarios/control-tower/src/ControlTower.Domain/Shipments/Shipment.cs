using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Locations;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Pricing;

namespace Proseware.ControlTower.Domain.Shipments;

public class Shipment
{
    public int Id { get; set; }
    /// <summary>House reference, e.g. PF24-0012345.</summary>
    public string Number { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? CustomerReference { get; set; }
    public TransportMode Mode { get; set; }
    public string IncotermCode { get; set; } = "DAP";
    public ShipmentStatus Status { get; set; }

    public int OriginId { get; set; }
    public Location? Origin { get; set; }
    public int DestinationId { get; set; }
    public Location? Destination { get; set; }

    public string ShipperName { get; set; } = "";
    public string ConsigneeName { get; set; } = "";
    public string? ConsigneeEmail { get; set; }
    public string? NotifyPhone { get; set; }

    public DateTimeOffset PlannedPickup { get; set; }
    public DateTimeOffset PlannedDelivery { get; set; }
    public DateTimeOffset? ActualPickup { get; set; }
    public DateTimeOffset? ActualDelivery { get; set; }
    public DateTimeOffset? Eta { get; set; }
    public DateTimeOffset? EtaCalculatedAt { get; set; }

    public string GoodsDescription { get; set; } = "";
    public decimal? DeclaredValue { get; set; }
    public string DeclaredValueCurrency { get; set; } = "EUR";

    /// <summary>Required transport temperature range for reefer loads (°C).</summary>
    public decimal? TemperatureMin { get; set; }
    public decimal? TemperatureMax { get; set; }

    public string? PodSignedBy { get; set; }
    public string? CancelReason { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[]? RowVersion { get; set; }

    public List<Package> Packages { get; set; } = new();
    public List<Leg> Legs { get; set; } = new();
    public List<Milestone> Milestones { get; set; } = new();
    public List<Charge> Charges { get; set; } = new();
    public List<CustomsDeclaration> CustomsDeclarations { get; set; } = new();

    public Incoterm Incoterm => Incoterm.Parse(IncotermCode);

    public bool IsReefer => TemperatureMin.HasValue || TemperatureMax.HasValue;

    public bool IsDangerousGoods => Packages.Any(p => p.DangerousGoods is not null);

    public decimal GrossWeightKg => Packages.Sum(p => p.WeightKg * p.Quantity);

    public decimal VolumeM3 => Packages.Sum(p => p.VolumeM3 * p.Quantity);

    public int PackageCount => Packages.Sum(p => p.Quantity);

    public bool CrossesCustomsBorder =>
        Origin is not null && Destination is not null && Origin.IsEuCustomsTerritory != Destination.IsEuCustomsTerritory;

    public Leg? CurrentLeg =>
        Legs.OrderBy(l => l.Sequence).FirstOrDefault(l => l.Status is LegStatus.Departed)
        ?? Legs.OrderBy(l => l.Sequence).FirstOrDefault(l => l.Status is LegStatus.Planned or LegStatus.Confirmed);

    public bool IsOpen => Status is not (ShipmentStatus.Delivered or ShipmentStatus.Cancelled);

    public Milestone? LastMilestone => Milestones.OrderByDescending(m => m.OccurredAt).FirstOrDefault();

    public Money TotalCharges(string currency) =>
        Charges.Where(c => c.Currency == currency).Aggregate(Money.Zero(currency), (sum, c) => sum + new Money(c.Amount, c.Currency));
}
