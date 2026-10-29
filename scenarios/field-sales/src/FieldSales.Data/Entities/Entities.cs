using System;
using System.Collections.Generic;

namespace Wingtip.FieldSales.Data.Entities
{
    public class CustomerEntity
    {
        public int CustomerId { get; set; }
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
        public DateTime ModifiedAt { get; set; }
    }

    public class ProductEntity
    {
        public int ProductId { get; set; }
        public string ItemNo { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Unit { get; set; }
        public int CaseSize { get; set; }
        public decimal ListPrice { get; set; }
        public decimal VatRate { get; set; }
        public int MinOrderQty { get; set; }
        public bool Active { get; set; }
        public byte[] RowVer { get; set; }
    }

    public class PriceRuleEntity
    {
        public int PriceRuleId { get; set; }
        public string PriceGroup { get; set; }
        public string Category { get; set; }
        public decimal DiscountPercent { get; set; }
    }

    public class PromotionEntity
    {
        public int PromotionId { get; set; }
        public string ItemNo { get; set; }
        public decimal PromoPrice { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public string CreatedBy { get; set; }
    }

    public class HolidayEntity
    {
        public DateTime Day { get; set; }
        public string Name { get; set; }
    }

    public class SalesOrderEntity
    {
        public SalesOrderEntity()
        {
            Lines = new List<SalesOrderLineEntity>();
        }

        public int SalesOrderId { get; set; }
        public string OrderNo { get; set; }
        public Guid ClientOrderId { get; set; }
        public string RepReference { get; set; }
        public int CustomerId { get; set; }
        public string CustomerNo { get; set; }
        public string RepCode { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ReceivedAt { get; set; }
        public DateTime RequestedDelivery { get; set; }
        public int Status { get; set; }
        public decimal NetTotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal ManualDiscountPercent { get; set; }
        public string Comment { get; set; }
        public string DecidedBy { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string DecisionComment { get; set; }
        public DateTime? ConfirmationSentAt { get; set; }
        public string ErpOrderNo { get; set; }
        public DateTime? ExportedAt { get; set; }
        public string ClientVersion { get; set; }

        public virtual CustomerEntity Customer { get; set; }
        public virtual ICollection<SalesOrderLineEntity> Lines { get; set; }
    }

    public class SalesOrderLineEntity
    {
        public int SalesOrderId { get; set; }
        public int LineNo { get; set; }
        public string ItemNo { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineDiscountPercent { get; set; }
        public decimal VatRate { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class RefreshTokenEntity
    {
        public string TokenHash { get; set; }
        public string UserName { get; set; }
        public string ClientId { get; set; }
        public DateTime IssuedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public string ProtectedTicket { get; set; }
    }

    public class PriceListImportEntity
    {
        public int PriceListImportId { get; set; }
        public string FileName { get; set; }
        public string ImportedBy { get; set; }
        public DateTime ImportedAt { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public long PriceListVersion { get; set; }
    }
}
