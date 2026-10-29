using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proseware.ControlTower.Domain.Documents;
using Proseware.ControlTower.Domain.Fleet;
using Proseware.ControlTower.Domain.Kpi;
using Proseware.ControlTower.Domain.Locations;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Pricing;
using Proseware.ControlTower.Domain.Scanning;
using Proseware.ControlTower.Domain.Tracking;

namespace Proseware.ControlTower.Data.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customer");
        b.HasIndex(c => c.Code).IsUnique();
        b.Property(c => c.Code).HasMaxLength(10).IsUnicode(false);
        b.Property(c => c.Name).HasMaxLength(70);
        b.Property(c => c.VatNumber).HasMaxLength(20).IsUnicode(false);
        b.Property(c => c.Eori).HasMaxLength(17).IsUnicode(false);
        b.Property(c => c.CountryCode).HasMaxLength(2).IsUnicode(false);
        b.Property(c => c.ContactEmail).HasMaxLength(254);
        b.Property(c => c.ContactPhone).HasMaxLength(30);
        b.Property(c => c.InvoiceCurrency).HasMaxLength(3).IsUnicode(false);
        b.Property(c => c.DiscountPercent).HasPrecision(5, 2);
    }
}

internal sealed class CarrierConfiguration : IEntityTypeConfiguration<Carrier>
{
    public void Configure(EntityTypeBuilder<Carrier> b)
    {
        b.ToTable("Carrier");
        b.HasIndex(c => c.Code).IsUnique();
        b.Property(c => c.Code).HasMaxLength(10).IsUnicode(false);
        b.Property(c => c.Name).HasMaxLength(70);
        b.Property(c => c.EdiPartnerId).HasMaxLength(35).IsUnicode(false);
        b.Property(c => c.EdiQualifier).HasMaxLength(4).IsUnicode(false);
        b.Property(c => c.ApiBaseUrl).HasMaxLength(200);
        b.Property(c => c.TrackingUrlTemplate).HasMaxLength(200);
        b.Property(c => c.OnTimeTargetPercent).HasPrecision(5, 2);
    }
}

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> b)
    {
        b.ToTable("Location");
        b.HasIndex(l => l.Code).IsUnique();
        b.Property(l => l.Code).HasMaxLength(10).IsUnicode(false);
        b.Property(l => l.Name).HasMaxLength(70);
        b.Property(l => l.Street).HasMaxLength(70);
        b.Property(l => l.PostalCode).HasMaxLength(10);
        b.Property(l => l.City).HasMaxLength(35);
        b.Property(l => l.CountryCode).HasMaxLength(2).IsUnicode(false);
        b.Property(l => l.TimeZoneId).HasMaxLength(50).IsUnicode(false);
        b.Property(l => l.Position).HasColumnType("geography");
        b.Ignore(l => l.DisplayAddress);
    }
}

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> b)
    {
        b.ToTable("Vehicle");
        b.HasIndex(v => v.Plate).IsUnique();
        b.HasIndex(v => v.TelematicsUnitId).IsUnique().HasFilter("[TelematicsUnitId] IS NOT NULL");
        b.Property(v => v.Plate).HasMaxLength(12);
        b.Property(v => v.TelematicsUnitId).HasMaxLength(32).IsUnicode(false);
        b.Property(v => v.MaxPayloadKg).HasPrecision(10, 0);
        b.Property(v => v.LoadingMeters).HasPrecision(4, 1);
        b.Property(v => v.LastTemperature).HasPrecision(5, 1);
        b.Property(v => v.LastPosition).HasColumnType("geography");
    }
}

internal sealed class VehiclePositionConfiguration : IEntityTypeConfiguration<VehiclePosition>
{
    public void Configure(EntityTypeBuilder<VehiclePosition> b)
    {
        b.ToTable("VehiclePosition");
        b.HasIndex(p => new { p.VehicleId, p.RecordedAt });
        b.Property(p => p.Position).HasColumnType("geography");
        b.Property(p => p.ReeferTemperature).HasPrecision(5, 1);
        b.Property(p => p.ReeferSetpoint).HasPrecision(5, 1);
    }
}

internal sealed class AlertConfiguration : IEntityTypeConfiguration<ShipmentAlert>
{
    public void Configure(EntityTypeBuilder<ShipmentAlert> b)
    {
        b.ToTable("Alert");
        b.HasIndex(a => new { a.ShipmentId, a.RuleCode, a.ResolvedAt });
        b.Property(a => a.RuleCode).HasMaxLength(20).IsUnicode(false);
        b.Property(a => a.Message).HasMaxLength(400);
        b.Property(a => a.AcknowledgedBy).HasMaxLength(100);
        b.Property(a => a.Resolution).HasMaxLength(400);
        b.Ignore(a => a.IsOpen);
    }
}

internal sealed class ScanConfiguration : IEntityTypeConfiguration<ScanEvent>
{
    public void Configure(EntityTypeBuilder<ScanEvent> b)
    {
        b.ToTable("Scan");
        b.HasIndex(s => s.ClientScanId).IsUnique();
        b.HasIndex(s => new { s.Sscc, s.ScannedAt });
        b.Property(s => s.Sscc).HasMaxLength(18).IsUnicode(false);
        b.Property(s => s.DoorOrBay).HasMaxLength(10);
        b.Property(s => s.VehiclePlate).HasMaxLength(12);
        b.Property(s => s.DeviceId).HasMaxLength(40).IsUnicode(false);
        b.Property(s => s.UserName).HasMaxLength(100);
        b.Property(s => s.Remarks).HasMaxLength(200);
    }
}

internal sealed class StoredDocumentConfiguration : IEntityTypeConfiguration<StoredDocument>
{
    public void Configure(EntityTypeBuilder<StoredDocument> b)
    {
        // Content is in the FILESTREAM table ct.DocumentContent, maintained by the document store.
        b.ToTable("Document");
        b.HasIndex(d => d.ShipmentId);
        b.Property(d => d.FileName).HasMaxLength(200);
        b.Property(d => d.ContentType).HasMaxLength(100);
        b.Property(d => d.Sha256).HasMaxLength(64).IsUnicode(false);
        b.Property(d => d.CreatedBy).HasMaxLength(100);
    }
}

internal sealed class DailyKpiConfiguration : IEntityTypeConfiguration<DailyKpi>
{
    public void Configure(EntityTypeBuilder<DailyKpi> b)
    {
        b.ToTable("DailyKpi");
        b.HasIndex(k => new { k.Day, k.CustomerId, k.CarrierId, k.Mode });
        b.Property(k => k.Day).HasColumnType("date");
        b.Property(k => k.Revenue).HasPrecision(18, 2);
        b.Property(k => k.ChargeableKg).HasPrecision(14, 1);
        b.Ignore(k => k.OnTimeDeliveryPercent);
        b.Ignore(k => k.OnTimePickupPercent);
    }
}

internal sealed class RateCardConfiguration : IEntityTypeConfiguration<RateCard>
{
    public void Configure(EntityTypeBuilder<RateCard> b)
    {
        b.ToTable("RateCard");
        b.Property(r => r.OriginCountry).HasMaxLength(2).IsUnicode(false);
        b.Property(r => r.DestinationCountry).HasMaxLength(2).IsUnicode(false);
        b.Property(r => r.OriginPostalPrefix).HasMaxLength(5).IsUnicode(false);
        b.Property(r => r.Currency).HasMaxLength(3).IsUnicode(false);
        b.Property(r => r.MinimumCharge).HasPrecision(18, 2);
        b.Property(r => r.FuelSurchargePercent).HasPrecision(5, 2);
        b.Property(r => r.ValidFrom).HasColumnType("date");
        b.Property(r => r.ValidTo).HasColumnType("date");
        b.HasMany(r => r.Breaks).WithOne().HasForeignKey(x => x.RateCardId);
    }
}

internal sealed class RateBreakConfiguration : IEntityTypeConfiguration<RateBreak>
{
    public void Configure(EntityTypeBuilder<RateBreak> b)
    {
        b.ToTable("RateBreak");
        b.Property(r => r.FromKg).HasPrecision(10, 1);
        b.Property(r => r.PricePer100Kg).HasPrecision(10, 2);
    }
}

internal sealed class EdiInterchangeConfiguration : IEntityTypeConfiguration<EdiInterchange>
{
    public void Configure(EntityTypeBuilder<EdiInterchange> b)
    {
        b.ToTable("EdiInterchange");
        b.HasIndex(e => new { e.CarrierId, e.Direction, e.ControlReference });
        b.Property(e => e.MessageType).HasMaxLength(6).IsUnicode(false);
        b.Property(e => e.ControlReference).HasMaxLength(14).IsUnicode(false);
        b.Property(e => e.FileName).HasMaxLength(200);
        b.Property(e => e.Error).HasMaxLength(2000);
    }
}
