using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Point = NetTopologySuite.Geometries.Point;
using NetTopologySuite.IO;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Documents;
using Proseware.ControlTower.Domain.Fleet;
using Proseware.ControlTower.Domain.Kpi;
using Proseware.ControlTower.Domain.Locations;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Pricing;
using Proseware.ControlTower.Domain.Scanning;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Domain.Tracking;

namespace Proseware.ControlTower.Data;

public class ControlTowerDbContext : DbContext
{
    public ControlTowerDbContext(DbContextOptions<ControlTowerDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Carrier> Carriers => Set<Carrier>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<Leg> Legs => Set<Leg>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<CustomsDeclaration> CustomsDeclarations => Set<CustomsDeclaration>();
    public DbSet<DeclarationLine> DeclarationLines => Set<DeclarationLine>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehiclePosition> VehiclePositions => Set<VehiclePosition>();
    public DbSet<ShipmentAlert> Alerts => Set<ShipmentAlert>();
    public DbSet<ScanEvent> Scans => Set<ScanEvent>();
    public DbSet<StoredDocument> Documents => Set<StoredDocument>();
    public DbSet<DailyKpi> DailyKpis => Set<DailyKpi>();
    public DbSet<RateCard> RateCards => Set<RateCard>();
    public DbSet<EdiInterchange> EdiInterchanges => Set<EdiInterchange>();

    public bool IsSqlite => Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ct");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ControlTowerDbContext).Assembly);

        if (IsSqlite)
        {
            // Development databases: no spatial extension, keep points as WKT text and
            // store DateTimeOffset as ISO text that sorts in UTC order.
            modelBuilder.HasDefaultSchema(null);
            var pointConverter = new ValueConverter<Point, string>(
                p => new WKTWriter().Write(p),
                s => (Point)new WKTReader(GeoPoints.Factory.GeometryServices).Read(s));
            var dtoConverter = new ValueConverter<DateTimeOffset, string>(
                d => d.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'"),
                s => DateTimeOffset.Parse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal));
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    if (property.ClrType == typeof(Point)) property.SetValueConverter(pointConverter);
                    if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?)) property.SetValueConverter(dtoConverter);
                    if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?)) property.SetProviderClrType(typeof(double));
                    if (property.Name == nameof(Shipment.RowVersion))
                    {
                        property.IsConcurrencyToken = false;
                        property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
                    }
                }
            }
        }
    }
}
