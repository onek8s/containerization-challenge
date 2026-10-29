using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core;

namespace Litware.OrderHub.Integration.Data
{
    public interface IOrderStore
    {
        Task<OrderRecord> FindAsync(Guid orderId);
        Task<OrderRecord> FindByNumberAsync(string orderNumber);
        Task<OrderRecord> FindByExternalRefAsync(string channel, string externalRef);

        /// <summary>Stores a new order; accepted orders get their order number here. Returns false for a duplicate.</summary>
        Task<bool> InsertAsync(OrderRecord order);

        /// <summary>Result of validating an already stored order again.</summary>
        Task UpdateValidationAsync(Guid orderId, string status, string reason);

        Task MarkSentToWarehouseAsync(Guid orderId, string fileName);
        Task MarkShippedAsync(Guid orderId, string carrier, string trackingNumber, DateTime shippedUtc, IEnumerable<ShippedLine> lines);
        Task<string> SaveInvoiceAsync(Guid orderId, byte[] pdf);

        Task AddEventAsync(Guid orderId, string eventType, string detail);
        Task<IList<OrderEvent>> GetEventsAsync(Guid orderId);

        Task<Product> FindProductAsync(string sku);
        Task<IList<Product>> GetProductsAsync();
        Task SetProductActiveAsync(string sku, bool active);

        Task<IList<OrderRecord>> SearchAsync(OrderQuery query);
        Task<IList<SalesLine>> GetInvoicedLinesAsync(DateTime fromUtc, DateTime toUtc);
        Task<IDictionary<string, int>> CountByStatusAsync(DateTime sinceUtc);

        Task<byte[]> GetInvoicePdfAsync(string invoiceNumber);
        Task SaveReportFileAsync(string fileName, DateTime reportDate, byte[] content);
        Task<IList<ReportFileInfo>> GetReportFilesAsync(int top);
        Task<byte[]> GetReportFileAsync(string fileName);
    }

    public class OrderQuery
    {
        public string Text { get; set; }
        public string Status { get; set; }
        public string Channel { get; set; }
        public DateTime? FromUtc { get; set; }
        public int Top { get; set; } = 200;
    }

    public class SalesLine
    {
        public string InvoiceNumber { get; set; }
        public string OrderNumber { get; set; }
        public string Channel { get; set; }
        public DateTime InvoicedUtc { get; set; }
        public string ShipCountry { get; set; }
        public string Currency { get; set; }
        public string Sku { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineAmount { get; set; }
    }

    public class ReportFileInfo
    {
        public string FileName { get; set; }
        public DateTime ReportDate { get; set; }
        public DateTime CreatedUtc { get; set; }
        public long Size { get; set; }
    }
}
