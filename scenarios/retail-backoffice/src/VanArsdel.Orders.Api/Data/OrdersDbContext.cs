using MassTransit.EntityFrameworkCoreIntegration;
using MassTransit.EntityFrameworkCoreIntegration.Mappings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.Orders.Api.Domain;
using VanArsdel.Orders.Api.Saga;

namespace VanArsdel.Orders.Api.Data;

public class OrdersDbContext : SagaDbContext, IOutboxContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();
    public DbSet<InboundWebhook> InboundWebhooks => Set<InboundWebhook>();
    public DbSet<OrderState> OrderStates => Set<OrderState>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get { yield return new OrderStateMap(); }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("Order");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.OrderNo).IsUnique();
            b.HasIndex(x => x.CustomerEmail);
            b.HasIndex(x => new { x.Status, x.PlacedUtc });
            b.Property(x => x.OrderNo).HasMaxLength(30).IsRequired();
            b.Property(x => x.Channel).HasMaxLength(10);
            b.Property(x => x.Market).HasMaxLength(2);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.Status).HasMaxLength(20);
            b.Property(x => x.CustomerEmail).HasMaxLength(200);
            b.Property(x => x.CustomerFirstName).HasMaxLength(100);
            b.Property(x => x.CustomerLastName).HasMaxLength(100);
            b.Property(x => x.CustomerPhone).HasMaxLength(30);
            b.Property(x => x.LoyaltyCardNo).HasMaxLength(20);
            b.Property(x => x.DeliveryMethod).HasMaxLength(20);
            b.Property(x => x.CollectStoreCode).HasMaxLength(10);
            b.Property(x => x.ShipToName).HasMaxLength(200);
            b.Property(x => x.ShipToStreet).HasMaxLength(200);
            b.Property(x => x.ShipToPostalCode).HasMaxLength(20);
            b.Property(x => x.ShipToCity).HasMaxLength(100);
            b.Property(x => x.ShipToCountry).HasMaxLength(2);
            b.Property(x => x.PromotionCode).HasMaxLength(30);
            b.Property(x => x.PaymentReference).HasMaxLength(100);
            b.Property(x => x.PaymentMethod).HasMaxLength(30);
            b.Property(x => x.PaymentStatus).HasMaxLength(20);
            b.Property(x => x.CreatedBy).HasMaxLength(100);
            b.Property(x => x.CancelReason).HasMaxLength(500);
            b.Property(x => x.ShippingFee).HasColumnType("decimal(18,2)");
            b.Property(x => x.Total).HasColumnType("decimal(18,2)");
            b.Property(x => x.CapturedAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.RowVersion).IsRowVersion();
            b.Ignore(x => x.LinesTotal);
            b.Ignore(x => x.CanCancel);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId);
            b.HasMany(x => x.Shipments).WithOne().HasForeignKey(x => x.OrderId);
            b.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.OrderId);
        });

        modelBuilder.Entity<OrderLine>(b =>
        {
            b.ToTable("OrderLine");
            b.Property(x => x.Sku).HasMaxLength(40);
            b.Property(x => x.Description).HasMaxLength(200);
            b.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            b.Property(x => x.Discount).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Shipment>(b =>
        {
            b.ToTable("Shipment");
            b.Property(x => x.FromLocation).HasMaxLength(10);
            b.Property(x => x.FromLocationType).HasMaxLength(10);
            b.Property(x => x.Status).HasMaxLength(20);
            b.Property(x => x.Carrier).HasMaxLength(30);
            b.Property(x => x.CarrierShipmentId).HasMaxLength(60);
            b.HasIndex(x => x.CarrierShipmentId);
            b.Property(x => x.TrackingNo).HasMaxLength(60);
            b.Property(x => x.TrackingUrl).HasMaxLength(300);
            b.Property(x => x.LabelUrl).HasMaxLength(300);
            b.Property(x => x.PackedBy).HasMaxLength(100);
            b.Property(x => x.WeightKg).HasColumnType("decimal(9,3)");
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.ShipmentId);
        });

        modelBuilder.Entity<ShipmentLine>(b =>
        {
            b.ToTable("ShipmentLine");
            b.Property(x => x.Sku).HasMaxLength(40);
        });

        modelBuilder.Entity<OrderEvent>(b =>
        {
            b.ToTable("OrderEvent");
            b.Property(x => x.Type).HasMaxLength(40);
            b.Property(x => x.Text).HasMaxLength(1000);
            b.Property(x => x.By).HasMaxLength(100);
        });

        modelBuilder.Entity<InboundWebhook>(b =>
        {
            b.ToTable("InboundWebhook");
            b.Property(x => x.Source).HasMaxLength(20);
            b.Property(x => x.ExternalId).HasMaxLength(100);
            b.Property(x => x.Type).HasMaxLength(50);
            b.HasIndex(x => new { x.Source, x.ExternalId }).IsUnique();
        });

        modelBuilder.AddOutbox();
    }
}

public class OrderStateMap : SagaClassMap<OrderState>
{
    protected override void Configure(EntityTypeBuilder<OrderState> entity, ModelBuilder model)
    {
        entity.ToTable("OrderState");
        entity.Property(x => x.CurrentState).HasMaxLength(40);
        entity.Property(x => x.OrderNo).HasMaxLength(30);
        entity.Property(x => x.DeliveryMethod).HasMaxLength(20);
        entity.Property(x => x.RowVersion).IsRowVersion();
    }
}
