using Woodgrove.Statements.Data;
using Woodgrove.Statements.Reporting;

namespace Woodgrove.Statements.Web
{
    /// <summary>Data access objects for controllers and the report viewer page (one connection string).</summary>
    public static class PortalServices
    {
        static readonly Db Database = Db.FromConfig();

        public static Db Db { get { return Database; } }
        public static CustomerRepository Customers { get { return new CustomerRepository(Database); } }
        public static LedgerRepository Ledger { get { return new LedgerRepository(Database); } }
        public static DocumentRepository Documents { get { return new DocumentRepository(Database); } }
        public static SubscriptionRepository Subscriptions { get { return new SubscriptionRepository(Database); } }
        public static DeliveryRepository Deliveries { get { return new DeliveryRepository(Database); } }
        public static ApiClientRepository ApiClients { get { return new ApiClientRepository(Database); } }
        public static DocumentService DocumentService { get { return new DocumentService(Database); } }
    }
}
