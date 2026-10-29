using System;
using System.Collections.Generic;

namespace Wingtip.FieldSales.Contracts
{
    public class ProductDto
    {
        public string ItemNo { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Unit { get; set; }
        public int CaseSize { get; set; }
        public decimal ListPrice { get; set; }
        public decimal VatRate { get; set; }
        public int MinOrderQty { get; set; }
        public bool Active { get; set; }
    }

    public class PriceRuleDto
    {
        public string PriceGroup { get; set; }
        public string Category { get; set; }
        public decimal DiscountPercent { get; set; }
    }

    public class PromotionDto
    {
        public string ItemNo { get; set; }
        public decimal PromoPrice { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
    }

    /// <summary>
    /// Catalog changes since the version the client already has. Price rules and promotions are always sent in
    /// full because they are small; products are sent incrementally.
    /// </summary>
    public class CatalogResponse
    {
        public long Version { get; set; }
        public long PriceListVersion { get; set; }
        public bool FullRefresh { get; set; }
        public List<ProductDto> Products { get; set; }
        public List<PriceRuleDto> PriceRules { get; set; }
        public List<PromotionDto> Promotions { get; set; }
        public List<DateTime> Holidays { get; set; }
        public string CutOff { get; set; }
    }

    public class CustomerDto
    {
        public string CustomerNo { get; set; }
        public string Name { get; set; }
        public string Street { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string Email { get; set; }
        public string PriceGroup { get; set; }
        public string RepCode { get; set; }
        public int DeliveryDays { get; set; }
        public bool CreditBlocked { get; set; }
    }
}
