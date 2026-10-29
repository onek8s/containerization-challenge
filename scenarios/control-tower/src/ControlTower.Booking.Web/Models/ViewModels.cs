using Microsoft.AspNetCore.Mvc.Rendering;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Documents;
using Proseware.ControlTower.Domain.Pricing;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Domain.Tracking;

namespace Proseware.ControlTower.Booking.Models;

public sealed class DashboardViewModel
{
    public int OpenShipments { get; set; }
    public int PickupsToday { get; set; }
    public int DeliveriesToday { get; set; }
    public int OpenAlerts { get; set; }
    public int Requested { get; set; }
    public int CustomsPending { get; set; }
    public IReadOnlyList<ShipmentRow> Recent { get; set; } = Array.Empty<ShipmentRow>();
}

public sealed class ShipmentSearchViewModel
{
    public string? Q { get; set; }
    public ShipmentStatus? Status { get; set; }
    public TransportMode? Mode { get; set; }
    public int? CustomerId { get; set; }
    public bool OpenOnly { get; set; } = true;
    public int Page { get; set; } = 1;
    public PagedResult<ShipmentRow>? Result { get; set; }
    public IEnumerable<SelectListItem> Customers { get; set; } = Array.Empty<SelectListItem>();
}

public sealed class MilestoneViewModel
{
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public string? Location { get; set; }
    public string Source { get; set; } = "";
    public string? Remarks { get; set; }
    public bool VisibleToCustomer { get; set; }
}

public sealed class LegViewModel
{
    public int Id { get; set; }
    public int Sequence { get; set; }
    public TransportMode Mode { get; set; }
    public string? CarrierName { get; set; }
    public int? CarrierId { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public LegStatus Status { get; set; }
    public DateTimeOffset PlannedDeparture { get; set; }
    public DateTimeOffset PlannedArrival { get; set; }
    public DateTimeOffset? ActualDeparture { get; set; }
    public DateTimeOffset? ActualArrival { get; set; }
    public DateTimeOffset? Eta { get; set; }
    public string? CarrierReference { get; set; }
    public string? VesselOrFlight { get; set; }
    public string? VehiclePlate { get; set; }
    public string? TrackingUrl { get; set; }
    public double? DistanceKm { get; set; }
}

public sealed class ShipmentDetailsViewModel
{
    public Shipment Shipment { get; set; } = null!;
    public IReadOnlyList<LegViewModel> Legs { get; set; } = Array.Empty<LegViewModel>();
    public IReadOnlyList<MilestoneViewModel> Milestones { get; set; } = Array.Empty<MilestoneViewModel>();
    public IReadOnlyList<StoredDocument> Documents { get; set; } = Array.Empty<StoredDocument>();
    public IReadOnlyList<ShipmentAlert> Alerts { get; set; } = Array.Empty<ShipmentAlert>();
    public IReadOnlyList<string> Printers { get; set; } = Array.Empty<string>();
    public IReadOnlyDictionary<int, IReadOnlyList<string>> DeclarationErrors { get; set; } = new Dictionary<int, IReadOnlyList<string>>();
    public decimal ChargeableKg { get; set; }
    public ManualMilestoneInput NewMilestone { get; set; } = new();
}

public sealed class ManualMilestoneInput
{
    public string Code { get; set; } = MilestoneCodes.PickedUp;
    public DateTime OccurredAtLocal { get; set; } = DateTime.Now;
    public string? LocationCode { get; set; }
    public string? Remarks { get; set; }

    public static IEnumerable<SelectListItem> Codes =>
        MilestoneCodes.All.Select(c => new SelectListItem($"{c} - {MilestoneCodes.Describe(c)}", c));
}

public sealed class BookingStepViewModel
{
    public BookingDraft Draft { get; set; } = new();
    public IEnumerable<SelectListItem> Customers { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> Locations { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> Incoterms { get; set; } = Incoterm.All.Select(i => new SelectListItem($"{i.Code} - {i.Name}", i.Code));
    public IReadOnlyList<QuoteOption> Quotes { get; set; } = Array.Empty<QuoteOption>();
    public string? OriginText { get; set; }
    public string? DestinationText { get; set; }
    public string? CustomerName { get; set; }
}

public sealed record QuoteOption(int? RateCardId, string Carrier, decimal ChargeableKg, int TransitDays, decimal Total, string Currency, IReadOnlyList<QuoteLine> Lines);

public sealed class CustomerEditModel
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? VatNumber { get; set; }
    public string? Eori { get; set; }
    public string CountryCode { get; set; } = "FI";
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string InvoiceCurrency { get; set; } = "EUR";
    public bool PortalEnabled { get; set; }
    public bool CustomsBrokerageAgreed { get; set; }
    public int DomesticSlaDays { get; set; } = 2;
    public decimal DiscountPercent { get; set; }
}

public sealed class RateCardEditModel
{
    public int Id { get; set; }
    public int? CarrierId { get; set; }
    public TransportMode Mode { get; set; }
    public string OriginCountry { get; set; } = "";
    public string DestinationCountry { get; set; } = "";
    public string? OriginPostalPrefix { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal MinimumCharge { get; set; }
    public decimal FuelSurchargePercent { get; set; }
    public int TransitDays { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    /// <summary>One break per line: "from kg; price per 100 kg".</summary>
    public string Breaks { get; set; } = "";
    public IEnumerable<SelectListItem> Carriers { get; set; } = Array.Empty<SelectListItem>();
}

public sealed record DeclarationErrors(CustomsDeclaration Declaration, IReadOnlyList<string> Errors);
