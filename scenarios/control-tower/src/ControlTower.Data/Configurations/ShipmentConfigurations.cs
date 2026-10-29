using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Pricing;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Data.Configurations;

internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> b)
    {
        b.ToTable("Shipment");
        b.HasIndex(s => s.Number).IsUnique();
        b.HasIndex(s => new { s.CustomerId, s.Status });
        b.HasIndex(s => s.PlannedDelivery);
        b.Property(s => s.Number).HasMaxLength(12).IsUnicode(false);
        b.Property(s => s.CustomerReference).HasMaxLength(35);
        b.Property(s => s.IncotermCode).HasMaxLength(3).IsUnicode(false);
        b.Property(s => s.ShipperName).HasMaxLength(70);
        b.Property(s => s.ConsigneeName).HasMaxLength(70);
        b.Property(s => s.ConsigneeEmail).HasMaxLength(254);
        b.Property(s => s.NotifyPhone).HasMaxLength(30);
        b.Property(s => s.GoodsDescription).HasMaxLength(350);
        b.Property(s => s.DeclaredValue).HasPrecision(18, 2);
        b.Property(s => s.DeclaredValueCurrency).HasMaxLength(3).IsUnicode(false);
        b.Property(s => s.TemperatureMin).HasPrecision(5, 1);
        b.Property(s => s.TemperatureMax).HasPrecision(5, 1);
        b.Property(s => s.CreatedBy).HasMaxLength(100);
        b.Property(s => s.PodSignedBy).HasMaxLength(70);
        b.Property(s => s.CancelReason).HasMaxLength(200);
        b.Property(s => s.RowVersion).IsRowVersion();
        b.HasOne(s => s.Customer).WithMany().HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.Origin).WithMany().HasForeignKey(s => s.OriginId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.Destination).WithMany().HasForeignKey(s => s.DestinationId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(s => s.Packages).WithOne().HasForeignKey(p => p.ShipmentId);
        b.HasMany(s => s.Legs).WithOne().HasForeignKey(l => l.ShipmentId);
        b.HasMany(s => s.Milestones).WithOne().HasForeignKey(m => m.ShipmentId);
        b.HasMany(s => s.Charges).WithOne().HasForeignKey(c => c.ShipmentId);
        b.HasMany(s => s.CustomsDeclarations).WithOne().HasForeignKey(c => c.ShipmentId);
        b.Ignore(s => s.Incoterm);
        b.Ignore(s => s.CurrentLeg);
        b.Ignore(s => s.LastMilestone);
    }
}

internal sealed class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> b)
    {
        b.ToTable("Package");
        b.HasIndex(p => p.Sscc).IsUnique();
        b.Property(p => p.Sscc).HasMaxLength(18).IsUnicode(false);
        b.Property(p => p.WeightKg).HasPrecision(10, 2);
        b.Property(p => p.Marks).HasMaxLength(140);
        b.Property(p => p.ContainerNumber).HasMaxLength(11).IsUnicode(false);
        b.Property(p => p.SealNumber).HasMaxLength(20);
        b.OwnsOne(p => p.DangerousGoods, dg =>
        {
            dg.Property(d => d.UnNumber).HasColumnName("DgUnNumber").HasMaxLength(4).IsUnicode(false);
            dg.Property(d => d.ProperShippingName).HasColumnName("DgShippingName").HasMaxLength(200);
            dg.Property(d => d.HazardClass).HasColumnName("DgClass").HasMaxLength(5).IsUnicode(false);
            dg.Property(d => d.PackingGroup).HasColumnName("DgPackingGroup").HasMaxLength(3).IsUnicode(false);
            dg.Property(d => d.NetQuantity).HasColumnName("DgNetQuantity").HasPrecision(10, 3);
            dg.Property(d => d.NetQuantityUnit).HasColumnName("DgNetQuantityUnit").HasMaxLength(3);
            dg.Property(d => d.LimitedQuantity).HasColumnName("DgLimitedQuantity");
            dg.Property(d => d.TunnelCode).HasColumnName("DgTunnelCode").HasMaxLength(5);
        });
    }
}

internal sealed class LegConfiguration : IEntityTypeConfiguration<Leg>
{
    public void Configure(EntityTypeBuilder<Leg> b)
    {
        b.ToTable("Leg");
        b.HasIndex(l => new { l.ShipmentId, l.Sequence }).IsUnique();
        b.HasIndex(l => l.CarrierReference);
        b.Property(l => l.CarrierReference).HasMaxLength(35);
        b.Property(l => l.VesselOrFlight).HasMaxLength(35);
        b.HasOne(l => l.Carrier).WithMany().HasForeignKey(l => l.CarrierId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(l => l.From).WithMany().HasForeignKey(l => l.FromId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(l => l.To).WithMany().HasForeignKey(l => l.ToId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(l => l.Vehicle).WithMany().HasForeignKey(l => l.VehicleId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> b)
    {
        b.ToTable("Milestone");
        b.HasIndex(m => new { m.ShipmentId, m.OccurredAt });
        b.Property(m => m.Code).HasMaxLength(3).IsUnicode(false);
        b.Property(m => m.LocationText).HasMaxLength(100);
        b.Property(m => m.Source).HasMaxLength(60);
        b.Property(m => m.Remarks).HasMaxLength(350);
    }
}

internal sealed class ChargeConfiguration : IEntityTypeConfiguration<Charge>
{
    public void Configure(EntityTypeBuilder<Charge> b)
    {
        b.ToTable("Charge");
        b.Property(c => c.Code).HasMaxLength(3).IsUnicode(false);
        b.Property(c => c.Description).HasMaxLength(100);
        b.Property(c => c.Amount).HasPrecision(18, 2);
        b.Property(c => c.Currency).HasMaxLength(3).IsUnicode(false);
        b.Property(c => c.InvoiceNumber).HasMaxLength(20);
    }
}

internal sealed class CustomsDeclarationConfiguration : IEntityTypeConfiguration<CustomsDeclaration>
{
    public void Configure(EntityTypeBuilder<CustomsDeclaration> b)
    {
        b.ToTable("CustomsDeclaration");
        b.HasIndex(d => d.Lrn).IsUnique();
        b.HasIndex(d => d.Mrn);
        b.Property(d => d.Lrn).HasMaxLength(22).IsUnicode(false);
        b.Property(d => d.Mrn).HasMaxLength(18).IsUnicode(false);
        b.Property(d => d.DeclarantEori).HasMaxLength(17).IsUnicode(false);
        b.Property(d => d.ConsignorEori).HasMaxLength(17).IsUnicode(false);
        b.Property(d => d.ConsigneeEori).HasMaxLength(17).IsUnicode(false);
        b.Property(d => d.OfficeOfDeclaration).HasMaxLength(8).IsUnicode(false);
        b.Property(d => d.OfficeOfExit).HasMaxLength(8).IsUnicode(false);
        b.Property(d => d.CurrencyCode).HasMaxLength(3).IsUnicode(false);
        b.Property(d => d.RejectionReason).HasMaxLength(500);
        b.HasMany(d => d.Lines).WithOne().HasForeignKey(l => l.CustomsDeclarationId);
    }
}

internal sealed class DeclarationLineConfiguration : IEntityTypeConfiguration<DeclarationLine>
{
    public void Configure(EntityTypeBuilder<DeclarationLine> b)
    {
        b.ToTable("DeclarationLine");
        b.Property(l => l.CommodityCode).HasMaxLength(10).IsUnicode(false);
        b.Property(l => l.Description).HasMaxLength(280);
        b.Property(l => l.OriginCountry).HasMaxLength(2).IsUnicode(false);
        b.Property(l => l.NetMassKg).HasPrecision(12, 3);
        b.Property(l => l.GrossMassKg).HasPrecision(12, 3);
        b.Property(l => l.StatisticalValue).HasPrecision(18, 2);
        b.Property(l => l.ProcedureCode).HasMaxLength(4).IsUnicode(false);
        b.Property(l => l.PackageKindCode).HasMaxLength(2).IsUnicode(false);
    }
}
