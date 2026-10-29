namespace Wingtip.FieldSales.Core
{
    public class Product
    {
        public string ItemNo { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }

        /// <summary>Sales unit: EA (each) or CS (case). Quantities are always entered in sales units.</summary>
        public string Unit { get; set; }

        /// <summary>Number of consumer units in one case, printed on the order confirmation.</summary>
        public int CaseSize { get; set; }

        public decimal ListPrice { get; set; }
        public decimal VatRate { get; set; }
        public int MinOrderQty { get; set; }
        public bool Active { get; set; }
    }
}
