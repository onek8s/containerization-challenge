using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Booking.Models;

/// <summary>Booking wizard state, kept in the session between the steps.</summary>
public sealed class BookingDraft
{
    public int? CustomerId { get; set; }
    public string? CustomerReference { get; set; }
    public TransportMode Mode { get; set; } = TransportMode.Road;
    public string IncotermCode { get; set; } = "DAP";
    public int? OriginId { get; set; }
    public int? DestinationId { get; set; }
    public string ShipperName { get; set; } = "";
    public string ConsigneeName { get; set; } = "";
    public string? ConsigneeEmail { get; set; }
    public string? NotifyPhone { get; set; }
    public DateTime PickupDate { get; set; } = DateTime.Today.AddDays(1);
    public TimeSpan PickupTime { get; set; } = new(9, 0, 0);
    public string GoodsDescription { get; set; } = "";
    public decimal? DeclaredValue { get; set; }
    public decimal? TemperatureMin { get; set; }
    public decimal? TemperatureMax { get; set; }
    public bool CustomsClearance { get; set; }
    public bool Insurance { get; set; }
    public List<PackageLine> Packages { get; set; } = new();
    public int? SelectedRateCardId { get; set; }
    public int Step { get; set; } = 1;

    public bool IsReefer => TemperatureMin.HasValue || TemperatureMax.HasValue;
}

public sealed class PackageLine
{
    public PackageKind Kind { get; set; } = PackageKind.Pallet;
    public int Quantity { get; set; } = 1;
    public decimal WeightKg { get; set; }
    public int LengthCm { get; set; } = 120;
    public int WidthCm { get; set; } = 80;
    public int HeightCm { get; set; } = 120;
    public bool Stackable { get; set; } = true;
    public string? Marks { get; set; }
    public string? DgUnNumber { get; set; }
    public string? DgShippingName { get; set; }
    public string? DgClass { get; set; }
    public string? DgPackingGroup { get; set; }
    public decimal? DgNetQuantity { get; set; }
    public bool DgLimitedQuantity { get; set; }

    public bool HasDangerousGoods => !string.IsNullOrWhiteSpace(DgUnNumber);
}
