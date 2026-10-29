namespace Wingtip.FieldSales.Core
{
    /// <summary>Discount from list price for a customer price group and product category.
    /// A rule with an empty category applies to every category of the group.</summary>
    public class PriceRule
    {
        public string PriceGroup { get; set; }
        public string Category { get; set; }
        public decimal DiscountPercent { get; set; }
    }
}
