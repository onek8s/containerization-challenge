using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Booking.Services;

public static class CustomsDeclarationBuilder
{
    public const string ProsewareEori = "FI01234567";

    /// <summary>Draft with one goods item; the customs team completes commodity codes before submitting.</summary>
    public static CustomsDeclaration Draft(Shipment shipment, Customer customer, DateTimeOffset now)
    {
        var import = shipment.Destination!.IsEuCustomsTerritory;
        var declaration = new CustomsDeclaration
        {
            Type = import ? DeclarationType.Import : DeclarationType.Export,
            Status = DeclarationStatus.Draft,
            Lrn = $"PF{now:yyMMddHHmmss}{Random.Shared.Next(100, 999)}",
            DeclarantEori = ProsewareEori,
            ConsignorEori = import ? null : customer.Eori,
            ConsigneeEori = import ? customer.Eori : null,
            OfficeOfDeclaration = import ? "FI001000" : "FI004400",
            OfficeOfExit = import ? null : shipment.Mode switch
            {
                TransportMode.Sea => "FI001400",
                TransportMode.Air => "FI003100",
                _ => "SE600001"
            },
            CurrencyCode = shipment.DeclaredValueCurrency,
            CreatedAt = now
        };
        declaration.Lines.Add(new DeclarationLine
        {
            LineNumber = 1,
            CommodityCode = "",
            Description = shipment.GoodsDescription,
            OriginCountry = shipment.Origin!.CountryCode,
            NetMassKg = Math.Round(shipment.GrossWeightKg * 0.92m, 3),
            GrossMassKg = shipment.GrossWeightKg,
            StatisticalValue = shipment.DeclaredValue ?? 0,
            ProcedureCode = import ? "4000" : "1000",
            Packages = shipment.PackageCount,
            PackageKindCode = shipment.Packages.FirstOrDefault()?.Kind switch
            {
                PackageKind.Parcel => "PK",
                PackageKind.Container20 or PackageKind.Container40 or PackageKind.Container40HighCube => "CN",
                _ => "PX"
            }
        });
        return declaration;
    }
}
