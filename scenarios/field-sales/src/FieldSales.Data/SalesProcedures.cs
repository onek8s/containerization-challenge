using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Wingtip.FieldSales.Core;

namespace Wingtip.FieldSales.Data
{
    public class CatalogChangeRow
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
        public long Version { get; set; }
    }

    public class SubmitOrderResult
    {
        public int SalesOrderId { get; set; }
        public string OrderNo { get; set; }
        public int Status { get; set; }
        public bool AlreadyReceived { get; set; }
    }

    public class ImportResult
    {
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public long PriceListVersion { get; set; }
    }

    public class OrderDecisionResult
    {
        public int Status { get; set; }
        public string OrderNo { get; set; }
        public string CustomerEmail { get; set; }
    }

    /// <summary>
    /// Calls to the stored procedures. Order submission, price list import and approvals run in the database so
    /// that order numbering, idempotency and the price list version stay consistent under concurrent syncs.
    /// </summary>
    public class SalesProcedures
    {
        private readonly FieldSalesDbContext _db;

        public SalesProcedures(FieldSalesDbContext db)
        {
            _db = db;
        }

        public Task<List<CatalogChangeRow>> GetCatalogChangesAsync(long sinceVersion)
        {
            return _db.Database.SqlQuery<CatalogChangeRow>(
                "EXEC dbo.usp_GetCatalogChanges @SinceVersion",
                new SqlParameter("@SinceVersion", sinceVersion)).ToListAsync();
        }

        public Task<long> GetCatalogVersionAsync()
        {
            return _db.Database.SqlQuery<long>("SELECT CAST(MIN_ACTIVE_ROWVERSION() AS bigint) - 1").SingleAsync();
        }

        public Task<long> GetPriceListVersionAsync()
        {
            return _db.Database.SqlQuery<long>("SELECT PriceListVersion FROM dbo.CatalogState WHERE Id = 1").SingleAsync();
        }

        public async Task<SubmitOrderResult> SubmitOrderAsync(SalesOrder order, OrderStatus status, string submittedBy,
            string clientVersion, IDictionary<string, Product> products)
        {
            var lines = new DataTable();
            lines.Columns.Add("LineNo", typeof(int));
            lines.Columns.Add("ItemNo", typeof(string));
            lines.Columns.Add("ItemName", typeof(string));
            lines.Columns.Add("Quantity", typeof(int));
            lines.Columns.Add("UnitPrice", typeof(decimal));
            lines.Columns.Add("LineDiscountPercent", typeof(decimal));
            lines.Columns.Add("VatRate", typeof(decimal));
            lines.Columns.Add("LineTotal", typeof(decimal));
            foreach (var l in order.Lines)
            {
                lines.Rows.Add(l.LineNo, l.ItemNo, products[l.ItemNo].Name, l.Quantity, l.UnitPrice, l.LineDiscountPercent, l.VatRate, l.LineTotal);
            }

            var parameters = new object[]
            {
                new SqlParameter("@ClientOrderId", order.ClientOrderId),
                new SqlParameter("@RepReference", (object)order.RepReference ?? DBNull.Value),
                new SqlParameter("@CustomerNo", order.CustomerNo),
                new SqlParameter("@RepCode", order.RepCode ?? ""),
                new SqlParameter("@SubmittedBy", submittedBy),
                new SqlParameter("@CreatedAt", order.CreatedAt),
                new SqlParameter("@RequestedDelivery", order.RequestedDelivery.Date),
                new SqlParameter("@Status", (int)status),
                new SqlParameter("@NetTotal", order.NetTotal),
                new SqlParameter("@VatTotal", order.VatTotal),
                new SqlParameter("@ManualDiscountPercent", order.ManualDiscountPercent),
                new SqlParameter("@Comment", (object)order.Comment ?? DBNull.Value),
                new SqlParameter("@ClientVersion", (object)clientVersion ?? DBNull.Value),
                new SqlParameter("@Lines", SqlDbType.Structured) { TypeName = "dbo.OrderLineType", Value = lines }
            };

            var result = await _db.Database.SqlQuery<SubmitOrderResult>(
                "EXEC dbo.usp_SubmitOrder @ClientOrderId, @RepReference, @CustomerNo, @RepCode, @SubmittedBy, @CreatedAt, " +
                "@RequestedDelivery, @Status, @NetTotal, @VatTotal, @ManualDiscountPercent, @Comment, @ClientVersion, @Lines",
                parameters).ToListAsync();
            return result.Single();
        }

        public async Task<OrderDecisionResult> DecideOrderAsync(int salesOrderId, bool approve, string decidedBy, string comment)
        {
            var result = await _db.Database.SqlQuery<OrderDecisionResult>(
                "EXEC dbo.usp_DecideOrder @SalesOrderId, @Approve, @DecidedBy, @Comment",
                new SqlParameter("@SalesOrderId", salesOrderId),
                new SqlParameter("@Approve", approve),
                new SqlParameter("@DecidedBy", decidedBy),
                new SqlParameter("@Comment", (object)comment ?? DBNull.Value)).ToListAsync();
            return result.Single();
        }

        public async Task<ImportResult> ImportPriceListAsync(IEnumerable<Product> products, string fileName, string importedBy)
        {
            var table = new DataTable();
            table.Columns.Add("ItemNo", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Category", typeof(string));
            table.Columns.Add("Unit", typeof(string));
            table.Columns.Add("CaseSize", typeof(int));
            table.Columns.Add("ListPrice", typeof(decimal));
            table.Columns.Add("VatRate", typeof(decimal));
            table.Columns.Add("MinOrderQty", typeof(int));
            table.Columns.Add("Active", typeof(bool));
            foreach (var p in products)
            {
                table.Rows.Add(p.ItemNo, p.Name, p.Category, p.Unit, p.CaseSize, p.ListPrice, p.VatRate, p.MinOrderQty, p.Active);
            }

            var result = await _db.Database.SqlQuery<ImportResult>(
                "EXEC dbo.usp_ImportPriceList @FileName, @ImportedBy, @Products",
                new SqlParameter("@FileName", fileName),
                new SqlParameter("@ImportedBy", importedBy),
                new SqlParameter("@Products", SqlDbType.Structured) { TypeName = "dbo.PriceListRowType", Value = table }).ToListAsync();
            return result.Single();
        }

        public Task<int> SavePromotionAsync(string itemNo, decimal promoPrice, DateTime validFrom, DateTime validTo, string createdBy)
        {
            return _db.Database.ExecuteSqlCommandAsync(
                "EXEC dbo.usp_SavePromotion @ItemNo, @PromoPrice, @ValidFrom, @ValidTo, @CreatedBy",
                new SqlParameter("@ItemNo", itemNo),
                new SqlParameter("@PromoPrice", promoPrice),
                new SqlParameter("@ValidFrom", validFrom.Date),
                new SqlParameter("@ValidTo", validTo.Date),
                new SqlParameter("@CreatedBy", createdBy));
        }

        public async Task<bool> MarkExportedAsync(string orderNo, string erpOrderNo)
        {
            var rows = await _db.Database.SqlQuery<int>(
                "EXEC dbo.usp_MarkExported @OrderNo, @ErpOrderNo",
                new SqlParameter("@OrderNo", orderNo),
                new SqlParameter("@ErpOrderNo", erpOrderNo)).ToListAsync();
            return rows.Single() == 1;
        }

        public async Task<ImportResult> UpsertCustomersAsync(IEnumerable<Customer> customers)
        {
            var table = new DataTable();
            table.Columns.Add("CustomerNo", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Street", typeof(string));
            table.Columns.Add("PostalCode", typeof(string));
            table.Columns.Add("City", typeof(string));
            table.Columns.Add("PriceGroup", typeof(string));
            table.Columns.Add("CreditBlocked", typeof(bool));
            foreach (var c in customers) table.Rows.Add(c.CustomerNo, c.Name, c.Street, c.PostalCode, c.City, c.PriceGroup, c.CreditBlocked);

            var result = await _db.Database.SqlQuery<ImportResult>(
                "EXEC dbo.usp_UpsertCustomers @Customers",
                new SqlParameter("@Customers", SqlDbType.Structured) { TypeName = "dbo.CustomerRowType", Value = table }).ToListAsync();
            return result.Single();
        }

        public Task<int> DeletePromotionAsync(int promotionId)
        {
            return _db.Database.ExecuteSqlCommandAsync(
                "EXEC dbo.usp_DeletePromotion @PromotionId", new SqlParameter("@PromotionId", promotionId));
        }
    }
}
