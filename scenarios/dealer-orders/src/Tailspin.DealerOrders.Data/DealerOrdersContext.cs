using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using Microsoft.AspNet.Identity.EntityFramework;
using Tailspin.DealerOrders.Data.Mapping;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.Data
{
    /// <summary>
    /// EF6 model of the DealerOrders database (schemas dlr, ax, edi, cache) plus the ASP.NET Identity tables of the
    /// dealer portal. The schema is created by the scripts in the db folder; EF never creates or migrates it.
    /// </summary>
    public class DealerOrdersContext : IdentityDbContext<PortalUser>
    {
        static DealerOrdersContext()
        {
            Database.SetInitializer<DealerOrdersContext>(null);
        }

        public DealerOrdersContext() : this("DealerOrders")
        {
        }

        public DealerOrdersContext(string nameOrConnectionString) : base(nameOrConnectionString, throwIfV1Schema: false)
        {
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }

        public static DealerOrdersContext Create() => new DealerOrdersContext();

        public DbSet<Dealer> Dealers { get; set; }
        public DbSet<DealerAddress> DealerAddresses { get; set; }
        public DbSet<Quote> Quotes { get; set; }
        public DbSet<QuoteLine> QuoteLines { get; set; }
        public DbSet<SalesOrder> Orders { get; set; }
        public DbSet<OrderLine> OrderLines { get; set; }
        public DbSet<OrderStatusChange> OrderHistory { get; set; }
        public DbSet<ApprovalRequest> Approvals { get; set; }
        public DbSet<Shipment> Shipments { get; set; }
        public DbSet<ShipmentLine> ShipmentLines { get; set; }
        public DbSet<AxCommand> AxCommands { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<EdiPartner> EdiPartners { get; set; }
        public DbSet<EdiMessage> EdiMessages { get; set; }
        public DbSet<StoredDocument> Documents { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
            modelBuilder.Properties<decimal>().Configure(p => p.HasPrecision(19, 4));
            modelBuilder.Properties<System.DateTime>().Configure(p => p.HasColumnType("datetime2"));

            modelBuilder.Entity<PortalUser>().ToTable("AspNetUsers");
            modelBuilder.Configurations.Add(new DealerMap());
            modelBuilder.Configurations.Add(new DealerAddressMap());
            modelBuilder.Configurations.Add(new QuoteMap());
            modelBuilder.Configurations.Add(new QuoteLineMap());
            modelBuilder.Configurations.Add(new SalesOrderMap());
            modelBuilder.Configurations.Add(new OrderLineMap());
            modelBuilder.Configurations.Add(new OrderStatusChangeMap());
            modelBuilder.Configurations.Add(new ApprovalMap());
            modelBuilder.Configurations.Add(new ShipmentMap());
            modelBuilder.Configurations.Add(new ShipmentLineMap());
            modelBuilder.Configurations.Add(new AxCommandMap());
            modelBuilder.Configurations.Add(new NotificationMap());
            modelBuilder.Configurations.Add(new EdiPartnerMap());
            modelBuilder.Configurations.Add(new EdiMessageMap());
            modelBuilder.Configurations.Add(new StoredDocumentMap());
        }
    }
}
