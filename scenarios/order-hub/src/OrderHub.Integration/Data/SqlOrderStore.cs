using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core;

namespace Litware.OrderHub.Integration.Data
{
    public class SqlOrderStore : IOrderStore
    {
        private const string OrderColumns = @"OrderId, OrderNumber, Channel, ExternalRef, Status, StatusReason, ReceivedUtc, UpdatedUtc,
            WarehouseFile, Carrier, TrackingNumber, ShippedUtc, InvoiceNumber,
            CustomerName, CustomerEmail, Phone, CustomerReference, Currency, ShippingMethod, ShippingAmount,
            ShipName, ShipStreet, ShipStreet2, ShipPostalCode, ShipCity, ShipCountry";

        private readonly string _connectionString;

        public SqlOrderStore(string connectionString)
        {
            _connectionString = connectionString;
        }

        private SqlConnection Open()
        {
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        public Task<OrderRecord> FindAsync(Guid orderId)
        {
            return FindOneAsync("OrderId = @orderId", new { orderId });
        }

        public Task<OrderRecord> FindByNumberAsync(string orderNumber)
        {
            return FindOneAsync("OrderNumber = @orderNumber", new { orderNumber });
        }

        public Task<OrderRecord> FindByExternalRefAsync(string channel, string externalRef)
        {
            return FindOneAsync("Channel = @channel AND ExternalRef = @externalRef", new { channel, externalRef });
        }

        private async Task<OrderRecord> FindOneAsync(string where, object args)
        {
            using (var db = Open())
            {
                var row = await db.QuerySingleOrDefaultAsync<OrderRow>(
                    "SELECT " + OrderColumns + " FROM hub.Orders WHERE " + where, args);
                if (row == null) return null;

                var lines = await db.QueryAsync<OrderLineDocument>(
                    "SELECT Sku, Description, Quantity, UnitPrice FROM hub.OrderLines WHERE OrderId = @OrderId ORDER BY LineNumber",
                    new { row.OrderId });
                var order = row.ToRecord();
                order.Document.Lines = lines.ToList();
                return order;
            }
        }

        public async Task<bool> InsertAsync(OrderRecord order)
        {
            var doc = order.Document;
            using (var db = Open())
            using (var tx = db.BeginTransaction())
            {
                try
                {
                    if (order.Status == OrderStatus.Accepted && order.OrderNumber == null)
                    {
                        order.OrderNumber = await NextOrderNumberAsync(db, tx);
                    }

                    await db.ExecuteAsync(@"
INSERT INTO hub.Orders (OrderId, OrderNumber, Channel, ExternalRef, Status, StatusReason, CustomerName, CustomerEmail, Phone,
    CustomerReference, ShipName, ShipStreet, ShipStreet2, ShipPostalCode, ShipCity, ShipCountry, Currency, ShippingMethod,
    ShippingAmount, ReceivedUtc, UpdatedUtc)
VALUES (@OrderId, @OrderNumber, @Channel, @ExternalRef, @Status, @StatusReason, @CustomerName, @CustomerEmail, @Phone,
    @CustomerReference, @ShipName, @ShipStreet, @ShipStreet2, @ShipPostalCode, @ShipCity, @ShipCountry, @Currency, @ShippingMethod,
    @ShippingAmount, @ReceivedUtc, SYSUTCDATETIME())",
                        new
                        {
                            order.OrderId, order.OrderNumber, order.Channel, order.ExternalRef, order.Status, order.StatusReason,
                            doc.CustomerName, doc.CustomerEmail, doc.Phone, doc.CustomerReference,
                            ShipName = doc.ShipTo?.Name, ShipStreet = doc.ShipTo?.Street, ShipStreet2 = doc.ShipTo?.Street2,
                            ShipPostalCode = doc.ShipTo?.PostalCode, ShipCity = doc.ShipTo?.City,
                            ShipCountry = Truncate(doc.ShipTo?.CountryCode, 2), Currency = Truncate(doc.Currency, 3),
                            doc.ShippingMethod, doc.ShippingAmount, order.ReceivedUtc
                        }, tx);

                    int lineNumber = 0;
                    foreach (var line in doc.Lines)
                    {
                        await db.ExecuteAsync(@"
INSERT INTO hub.OrderLines (OrderId, LineNumber, Sku, Description, Quantity, UnitPrice)
VALUES (@OrderId, @LineNumber, @Sku, @Description, @Quantity, @UnitPrice)",
                            new { order.OrderId, LineNumber = ++lineNumber, line.Sku, line.Description, line.Quantity, line.UnitPrice }, tx);
                    }

                    tx.Commit();
                    return true;
                }
                catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                {
                    tx.Rollback();
                    order.OrderNumber = null;
                    return false;
                }
            }
        }

        public async Task UpdateValidationAsync(Guid orderId, string status, string reason)
        {
            using (var db = Open())
            using (var tx = db.BeginTransaction())
            {
                string number = await db.ExecuteScalarAsync<string>(
                    "SELECT OrderNumber FROM hub.Orders WITH (UPDLOCK) WHERE OrderId = @orderId", new { orderId }, tx);
                if (status == OrderStatus.Accepted && number == null)
                {
                    number = await NextOrderNumberAsync(db, tx);
                }
                await db.ExecuteAsync(@"
UPDATE hub.Orders SET Status = @status, StatusReason = @reason, OrderNumber = @number, UpdatedUtc = SYSUTCDATETIME()
WHERE OrderId = @orderId", new { orderId, status, reason, number }, tx);
                tx.Commit();
            }
        }

        private static async Task<string> NextOrderNumberAsync(IDbConnection db, IDbTransaction tx)
        {
            int next = await db.ExecuteScalarAsync<int>("SELECT NEXT VALUE FOR hub.OrderNumbers", transaction: tx);
            return "LW" + next.ToString("D7");
        }

        public async Task MarkSentToWarehouseAsync(Guid orderId, string fileName)
        {
            using (var db = Open())
            {
                await db.ExecuteAsync(@"
UPDATE hub.Orders SET Status = CASE WHEN Status = 'Accepted' THEN 'SentToWarehouse' ELSE Status END,
    WarehouseFile = @fileName, UpdatedUtc = SYSUTCDATETIME()
WHERE OrderId = @orderId", new { orderId, fileName });
            }
        }

        public async Task MarkShippedAsync(Guid orderId, string carrier, string trackingNumber, DateTime shippedUtc, IEnumerable<ShippedLine> lines)
        {
            using (var db = Open())
            using (var tx = db.BeginTransaction())
            {
                await db.ExecuteAsync(@"
UPDATE hub.Orders SET Status = CASE WHEN Status IN ('Accepted', 'SentToWarehouse') THEN 'Shipped' ELSE Status END,
    Carrier = @carrier, TrackingNumber = @trackingNumber, ShippedUtc = @shippedUtc, UpdatedUtc = SYSUTCDATETIME()
WHERE OrderId = @orderId", new { orderId, carrier, trackingNumber, shippedUtc }, tx);

                foreach (var group in lines.GroupBy(l => l.Sku))
                {
                    await db.ExecuteAsync(@"
UPDATE hub.OrderLines SET ShippedQuantity = COALESCE(ShippedQuantity, 0) + @qty
WHERE OrderId = @orderId AND Sku = @sku", new { orderId, sku = group.Key, qty = group.Sum(l => l.Quantity) }, tx);
                }
                tx.Commit();
            }
        }

        public async Task<string> SaveInvoiceAsync(Guid orderId, byte[] pdf)
        {
            using (var db = Open())
            using (var tx = db.BeginTransaction())
            {
                string number = await db.ExecuteScalarAsync<string>(
                    "SELECT InvoiceNumber FROM hub.Orders WITH (UPDLOCK) WHERE OrderId = @orderId", new { orderId }, tx);
                if (number == null)
                {
                    int next = await db.ExecuteScalarAsync<int>("SELECT NEXT VALUE FOR hub.InvoiceNumbers", transaction: tx);
                    number = "INV" + next.ToString("D7");
                    await db.ExecuteAsync(@"
UPDATE hub.Orders SET InvoiceNumber = @number, InvoicedUtc = SYSUTCDATETIME(), Status = 'Invoiced', UpdatedUtc = SYSUTCDATETIME()
WHERE OrderId = @orderId", new { orderId, number }, tx);
                }

                if (pdf != null)
                {
                    await db.ExecuteAsync(@"
MERGE hub.InvoiceDocuments AS t
USING (SELECT @number AS InvoiceNumber) AS s ON t.InvoiceNumber = s.InvoiceNumber
WHEN MATCHED THEN UPDATE SET Pdf = @pdf, CreatedUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (InvoiceNumber, OrderId, CreatedUtc, Pdf) VALUES (@number, @orderId, SYSUTCDATETIME(), @pdf);",
                        new { number, orderId, pdf }, tx);
                }
                tx.Commit();
                return number;
            }
        }

        public async Task AddEventAsync(Guid orderId, string eventType, string detail)
        {
            using (var db = Open())
            {
                await db.ExecuteAsync(
                    "INSERT INTO hub.OrderEvents (OrderId, AtUtc, EventType, Detail) VALUES (@orderId, SYSUTCDATETIME(), @eventType, @detail)",
                    new { orderId, eventType, detail = Truncate(detail, 2000) });
            }
        }

        public async Task<IList<OrderEvent>> GetEventsAsync(Guid orderId)
        {
            using (var db = Open())
            {
                return (await db.QueryAsync<OrderEvent>(
                    "SELECT AtUtc, EventType, Detail FROM hub.OrderEvents WHERE OrderId = @orderId ORDER BY AtUtc, EventId",
                    new { orderId })).ToList();
            }
        }

        public async Task<Product> FindProductAsync(string sku)
        {
            using (var db = Open())
            {
                return await db.QuerySingleOrDefaultAsync<Product>(
                    "SELECT Sku, Description, Ean, Active FROM hub.Products WHERE Sku = @sku", new { sku });
            }
        }

        public async Task<IList<Product>> GetProductsAsync()
        {
            using (var db = Open())
            {
                return (await db.QueryAsync<Product>("SELECT Sku, Description, Ean, Active FROM hub.Products ORDER BY Sku")).ToList();
            }
        }

        public async Task SetProductActiveAsync(string sku, bool active)
        {
            using (var db = Open())
            {
                await db.ExecuteAsync("UPDATE hub.Products SET Active = @active WHERE Sku = @sku", new { sku, active });
            }
        }

        public async Task<IList<OrderRecord>> SearchAsync(OrderQuery query)
        {
            var sql = "SELECT TOP (@Top) " + OrderColumns + " FROM hub.Orders WHERE 1 = 1";
            if (!string.IsNullOrWhiteSpace(query.Status)) sql += " AND Status = @Status";
            if (!string.IsNullOrWhiteSpace(query.Channel)) sql += " AND Channel = @Channel";
            if (query.FromUtc.HasValue) sql += " AND ReceivedUtc >= @FromUtc";
            if (!string.IsNullOrWhiteSpace(query.Text))
            {
                sql += " AND (OrderNumber = @Text OR ExternalRef = @Text OR InvoiceNumber = @Text OR TrackingNumber = @Text" +
                       " OR CustomerName LIKE @Like OR CustomerEmail LIKE @Like OR CustomerReference LIKE @Like)";
            }
            sql += " ORDER BY ReceivedUtc DESC";

            using (var db = Open())
            {
                var rows = await db.QueryAsync<OrderRow>(sql, new
                {
                    query.Top, query.Status, query.Channel, query.FromUtc,
                    Text = query.Text?.Trim(), Like = "%" + query.Text?.Trim() + "%"
                });
                return rows.Select(r => r.ToRecord()).ToList();
            }
        }

        public async Task<IList<SalesLine>> GetInvoicedLinesAsync(DateTime fromUtc, DateTime toUtc)
        {
            using (var db = Open())
            {
                return (await db.QueryAsync<SalesLine>(@"
SELECT InvoiceNumber, OrderNumber, Channel, InvoicedUtc, ShipCountry, Currency, Sku, Description, Quantity, UnitPrice, LineAmount
FROM hub.InvoiceLines
WHERE InvoicedUtc >= @fromUtc AND InvoicedUtc < @toUtc
ORDER BY InvoiceNumber, LineNumber", new { fromUtc, toUtc })).ToList();
            }
        }

        public async Task<IDictionary<string, int>> CountByStatusAsync(DateTime sinceUtc)
        {
            using (var db = Open())
            {
                var rows = await db.QueryAsync<(string Status, int Count)>(
                    "SELECT Status, COUNT(*) FROM hub.Orders WHERE UpdatedUtc >= @sinceUtc GROUP BY Status", new { sinceUtc });
                return rows.ToDictionary(r => r.Status, r => r.Count);
            }
        }

        public async Task<byte[]> GetInvoicePdfAsync(string invoiceNumber)
        {
            using (var db = Open())
            {
                return await db.ExecuteScalarAsync<byte[]>(
                    "SELECT Pdf FROM hub.InvoiceDocuments WHERE InvoiceNumber = @invoiceNumber", new { invoiceNumber });
            }
        }

        public async Task SaveReportFileAsync(string fileName, DateTime reportDate, byte[] content)
        {
            using (var db = Open())
            {
                await db.ExecuteAsync(@"
MERGE hub.ReportFiles AS t
USING (SELECT @fileName AS FileName) AS s ON t.FileName = s.FileName
WHEN MATCHED THEN UPDATE SET Content = @content, ReportDate = @reportDate, CreatedUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (FileName, ReportDate, CreatedUtc, Content) VALUES (@fileName, @reportDate, SYSUTCDATETIME(), @content);",
                    new { fileName, reportDate = reportDate.Date, content });
            }
        }

        public async Task<IList<ReportFileInfo>> GetReportFilesAsync(int top)
        {
            using (var db = Open())
            {
                return (await db.QueryAsync<ReportFileInfo>(
                    "SELECT TOP (@top) FileName, ReportDate, CreatedUtc, DATALENGTH(Content) AS Size FROM hub.ReportFiles ORDER BY ReportDate DESC, FileName",
                    new { top })).ToList();
            }
        }

        public async Task<byte[]> GetReportFileAsync(string fileName)
        {
            using (var db = Open())
            {
                return await db.ExecuteScalarAsync<byte[]>(
                    "SELECT Content FROM hub.ReportFiles WHERE FileName = @fileName", new { fileName });
            }
        }

        private static string Truncate(string value, int length)
        {
            if (value == null) return null;
            return value.Length <= length ? value : value.Substring(0, length);
        }

        private class OrderRow
        {
            public Guid OrderId { get; set; }
            public string OrderNumber { get; set; }
            public string Channel { get; set; }
            public string ExternalRef { get; set; }
            public string Status { get; set; }
            public string StatusReason { get; set; }
            public DateTime ReceivedUtc { get; set; }
            public DateTime UpdatedUtc { get; set; }
            public string WarehouseFile { get; set; }
            public string Carrier { get; set; }
            public string TrackingNumber { get; set; }
            public DateTime? ShippedUtc { get; set; }
            public string InvoiceNumber { get; set; }
            public string CustomerName { get; set; }
            public string CustomerEmail { get; set; }
            public string Phone { get; set; }
            public string CustomerReference { get; set; }
            public string Currency { get; set; }
            public string ShippingMethod { get; set; }
            public decimal ShippingAmount { get; set; }
            public string ShipName { get; set; }
            public string ShipStreet { get; set; }
            public string ShipStreet2 { get; set; }
            public string ShipPostalCode { get; set; }
            public string ShipCity { get; set; }
            public string ShipCountry { get; set; }

            public OrderRecord ToRecord()
            {
                return new OrderRecord
                {
                    OrderId = OrderId, OrderNumber = OrderNumber, Channel = Channel, ExternalRef = ExternalRef,
                    Status = Status, StatusReason = StatusReason, ReceivedUtc = ReceivedUtc, UpdatedUtc = UpdatedUtc,
                    WarehouseFile = WarehouseFile, Carrier = Carrier, TrackingNumber = TrackingNumber,
                    ShippedUtc = ShippedUtc, InvoiceNumber = InvoiceNumber,
                    Document = new OrderDocument
                    {
                        CustomerName = CustomerName, CustomerEmail = CustomerEmail, Phone = Phone,
                        CustomerReference = CustomerReference, Currency = Currency?.Trim(),
                        ShippingMethod = ShippingMethod, ShippingAmount = ShippingAmount,
                        ShipTo = new Address
                        {
                            Name = ShipName, Street = ShipStreet, Street2 = ShipStreet2,
                            PostalCode = ShipPostalCode, City = ShipCity, CountryCode = ShipCountry?.Trim()
                        }
                    }
                };
            }
        }
    }
}
