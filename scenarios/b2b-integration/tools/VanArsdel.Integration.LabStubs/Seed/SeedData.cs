using System.Text.Json;
using System.Text.Json.Serialization;
using VanArsdel.Integration.Domain.Partners;

namespace VanArsdel.Integration.LabStubs.Seed
{
    public class SeedItem
    {
        public string Item { get; set; }
        public string Description { get; set; }
        public string Uom { get; set; }
        public decimal Price { get; set; }
        public string Gtin { get; set; }
        public bool Hazardous { get; set; }
        public decimal StockQty { get; set; }
    }

    public class ErpAccount
    {
        public string ErpNumber { get; set; }
        public string Name { get; set; }
        public decimal CreditLimit { get; set; }
        public decimal OpenExposure { get; set; }
        public bool Blocked { get; set; }
        public string BlockReason { get; set; }
        public string Currency { get; set; }

        /// <summary>Output determination: DESADV/INVOIC IDocs are only created for customers with EDI output.</summary>
        public bool EdiOutput { get; set; }
    }

    public class ErpPurchaseOrderLine
    {
        public int Line { get; set; }
        public string Item { get; set; }
        public decimal Quantity { get; set; }
        public string Uom { get; set; }
        public decimal Price { get; set; }
    }

    /// <summary>Open purchase orders in the ERP (erp-purchase-orders.json), released to suppliers as ORDERS05.</summary>
    public class ErpPurchaseOrder
    {
        public string PoNumber { get; set; }
        public string Vendor { get; set; }
        public string Currency { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime DeliveryDate { get; set; }
        public List<ErpPurchaseOrderLine> Lines { get; set; } = new List<ErpPurchaseOrderLine>();
    }

    /// <summary>samples/seed: the same partner and item data the SQL seed scripts load (deploy/db).</summary>
    public class SeedData
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public List<TradingPartner> Partners { get; set; } = new List<TradingPartner>();
        public List<PartnerCrossReference> CrossReferences { get; set; } = new List<PartnerCrossReference>();
        public List<SeedItem> Items { get; set; } = new List<SeedItem>();
        public List<ErpAccount> ErpAccounts { get; set; } = new List<ErpAccount>();
        public List<ErpPurchaseOrder> PurchaseOrders { get; set; } = new List<ErpPurchaseOrder>();

        public PartnerDirectory Directory()
        {
            return new PartnerDirectory(Partners, CrossReferences);
        }

        public SeedItem Item(string item)
        {
            return Items.FirstOrDefault(i => string.Equals(i.Item, item, StringComparison.OrdinalIgnoreCase));
        }

        public static SeedData Load(string seedFolder)
        {
            var partners = JsonSerializer.Deserialize<SeedData>(File.ReadAllText(Path.Combine(seedFolder, "partners.json")), Options);
            partners.Items = JsonSerializer.Deserialize<List<SeedItem>>(File.ReadAllText(Path.Combine(seedFolder, "items.json")), Options);
            partners.ErpAccounts = JsonSerializer.Deserialize<List<ErpAccount>>(File.ReadAllText(Path.Combine(seedFolder, "erp-accounts.json")), Options);
            partners.PurchaseOrders = JsonSerializer.Deserialize<List<ErpPurchaseOrder>>(File.ReadAllText(Path.Combine(seedFolder, "erp-purchase-orders.json")), Options);
            return partners;
        }
    }
}
