using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Integration.Customs;

public static class DeclarationMapper
{
    public static SubmitDeclarationRequest ToRequest(CustomsDeclaration d, Shipment s, string representativeEori)
    {
        var mainLeg = s.Legs.OrderByDescending(l => l.Mode != TransportMode.Road).ThenBy(l => l.Sequence).FirstOrDefault();
        return new SubmitDeclarationRequest
        {
            MessageId = Guid.NewGuid().ToString("N"),
            DeclarationType = d.Type switch { DeclarationType.Export => "EX", DeclarationType.Import => "IM", _ => "T1" },
            Lrn = d.Lrn,
            DeclarantEori = d.DeclarantEori,
            RepresentativeEori = representativeEori,
            ConsignorEori = d.ConsignorEori,
            ConsigneeEori = d.ConsigneeEori,
            OfficeOfDeclaration = d.OfficeOfDeclaration,
            OfficeOfExit = d.OfficeOfExit,
            TransportDocument = mainLeg?.CarrierReference ?? s.Number,
            MeansOfTransport = mainLeg?.VesselOrFlight ?? mainLeg?.Vehicle?.Plate,
            ModeOfTransportAtBorder = Edifact.IftminBuilder.ModeCode(mainLeg?.Mode ?? s.Mode),
            Currency = d.CurrencyCode,
            TotalInvoiceAmount = s.DeclaredValue ?? d.TotalStatisticalValue,
            DeliveryTerms = s.IncotermCode,
            DispatchCountry = s.Origin?.CountryCode ?? "",
            DestinationCountry = s.Destination?.CountryCode ?? "",
            GoodsItems = d.Lines.OrderBy(l => l.LineNumber).Select(l => new GoodsItem
            {
                ItemNumber = l.LineNumber,
                CommodityCode = l.CommodityCode,
                Description = l.Description,
                OriginCountry = l.OriginCountry,
                NetMass = l.NetMassKg,
                GrossMass = l.GrossMassKg,
                StatisticalValue = l.StatisticalValue,
                ProcedureCode = l.ProcedureCode,
                NumberOfPackages = l.Packages,
                KindOfPackages = l.PackageKindCode
            }).ToList()
        };
    }

    public static DeclarationStatus MapStatus(string status) => status?.ToUpperInvariant() switch
    {
        "ACCEPTED" => DeclarationStatus.Accepted,
        "UNDER_CONTROL" => DeclarationStatus.UnderControl,
        "RELEASED" => DeclarationStatus.Released,
        "REJECTED" => DeclarationStatus.Rejected,
        "INVALIDATED" => DeclarationStatus.Cancelled,
        _ => DeclarationStatus.Submitted
    };

    public static string? MilestoneFor(DeclarationStatus status) => status switch
    {
        DeclarationStatus.Submitted or DeclarationStatus.Accepted => MilestoneCodes.CustomsSubmitted,
        DeclarationStatus.UnderControl => MilestoneCodes.CustomsHeld,
        DeclarationStatus.Released => MilestoneCodes.CustomsReleased,
        DeclarationStatus.Rejected => MilestoneCodes.CustomsHeld,
        _ => null
    };
}
