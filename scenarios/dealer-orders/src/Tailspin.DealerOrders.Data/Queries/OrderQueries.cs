using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dapper;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.Data.Queries
{
    public class OrderFilter
    {
        public int? DealerId { get; set; }
        public string Text { get; set; }
        public OrderStatus? Status { get; set; }
        public OrderSource? Source { get; set; }
        public bool OpenOnly { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class OrderListItem
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public int DealerId { get; set; }
        public string DealerAccount { get; set; }
        public string DealerName { get; set; }
        public string DealerPoNumber { get; set; }
        public OrderStatus Status { get; set; }
        public OrderSource Source { get; set; }
        public OrderType OrderType { get; set; }
        public string AxSalesId { get; set; }
        public string Currency { get; set; }
        public decimal NetValue { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? RequestedDeliveryDate { get; set; }
        public DateTime? ConfirmedDeliveryDate { get; set; }
        public string FirstItem { get; set; }
    }

    public class PagedResult<T>
    {
        public IList<T> Items { get; set; }
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Pages => PageSize == 0 ? 0 : (Total + PageSize - 1) / PageSize;
    }

    public class DashboardFigures
    {
        public int OpenOrders { get; set; }
        public int AwaitingApproval { get; set; }
        public int InError { get; set; }
        public int ShippingThisWeek { get; set; }
        public int OpenQuotes { get; set; }
        public decimal OrderIntakeEur30Days { get; set; }
    }

    public class StockRow
    {
        public string ItemId { get; set; }
        public string Name { get; set; }
        public string ConfigurationId { get; set; }
        public string Warehouse { get; set; }
        public decimal Available { get; set; }
        public decimal OnOrder { get; set; }
        public DateTime? NextReceiptDate { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    /// <summary>Read-only list queries for the portal and the sales desk (Dapper, paged in SQL).</summary>
    public class OrderQueries
    {
        private readonly SqlConnectionFactory _connections;

        public OrderQueries(SqlConnectionFactory connections)
        {
            _connections = connections;
        }

        public PagedResult<OrderListItem> Search(OrderFilter filter)
        {
            var where = new StringBuilder("WHERE 1 = 1");
            if (filter.DealerId.HasValue) where.Append(" AND o.DealerId = @DealerId");
            if (filter.Status.HasValue) where.Append(" AND o.Status = @Status");
            if (filter.Source.HasValue) where.Append(" AND o.Source = @Source");
            if (filter.OpenOnly) where.Append(" AND o.Status NOT IN (11, 12, 13)");
            if (filter.FromUtc.HasValue) where.Append(" AND o.CreatedUtc >= @FromUtc");
            if (filter.ToUtc.HasValue) where.Append(" AND o.CreatedUtc < @ToUtc");
            if (!string.IsNullOrWhiteSpace(filter.Text))
                where.Append(" AND (o.OrderNumber LIKE @Like OR o.DealerPoNumber LIKE @Like OR o.AxSalesId LIKE @Like OR d.Name LIKE @Like OR d.AccountNumber LIKE @Like OR o.EndCustomerName LIKE @Like)");

            var sql = $@"
SELECT COUNT(*) FROM dlr.SalesOrder o JOIN dlr.Dealer d ON d.Id = o.DealerId {where};
SELECT o.Id, o.OrderNumber, o.DealerId, d.AccountNumber AS DealerAccount, d.Name AS DealerName, o.DealerPoNumber, o.Status, o.Source, o.OrderType,
       o.AxSalesId, o.Currency, ISNULL(v.NetValue, 0) AS NetValue, o.CreatedUtc, o.RequestedDeliveryDate, o.ConfirmedDeliveryDate, v.FirstItem
FROM dlr.SalesOrder o
JOIN dlr.Dealer d ON d.Id = o.DealerId
OUTER APPLY (SELECT SUM(l.NetPrice * l.Quantity) AS NetValue, MIN(CASE WHEN l.LineNumber = 1 THEN l.Description END) AS FirstItem
             FROM dlr.OrderLine l WHERE l.OrderId = o.Id AND l.Cancelled = 0) v
{where}
ORDER BY o.Id DESC
OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;";

            using (var c = _connections.Open())
            using (var multi = c.QueryMultiple(sql, new
            {
                filter.DealerId,
                Status = (int?)filter.Status,
                Source = (int?)filter.Source,
                filter.FromUtc,
                filter.ToUtc,
                Like = "%" + (filter.Text ?? "").Trim().Replace("[", "[[]").Replace("%", "[%]") + "%",
                Skip = (Math.Max(1, filter.Page) - 1) * filter.PageSize,
                filter.PageSize
            }))
            {
                var total = multi.ReadSingle<int>();
                return new PagedResult<OrderListItem> { Total = total, Items = multi.Read<OrderListItem>().ToList(), Page = filter.Page, PageSize = filter.PageSize };
            }
        }

        public DashboardFigures Dashboard(int? dealerId, DateTime todayLocal)
        {
            using (var c = _connections.Open())
                return c.QueryFirst<DashboardFigures>(@"
SELECT
  (SELECT COUNT(*) FROM dlr.SalesOrder WHERE (@dealerId IS NULL OR DealerId = @dealerId) AND Status NOT IN (0, 11, 12, 13)) AS OpenOrders,
  (SELECT COUNT(*) FROM dlr.SalesOrder WHERE (@dealerId IS NULL OR DealerId = @dealerId) AND Status IN (2, 3)) AS AwaitingApproval,
  (SELECT COUNT(*) FROM dlr.SalesOrder WHERE (@dealerId IS NULL OR DealerId = @dealerId) AND Status = 14) AS InError,
  (SELECT COUNT(*) FROM dlr.SalesOrder WHERE (@dealerId IS NULL OR DealerId = @dealerId) AND ConfirmedDeliveryDate BETWEEN @today AND DATEADD(day, 7, @today) AND Status < 10) AS ShippingThisWeek,
  (SELECT COUNT(*) FROM dlr.Quote WHERE (@dealerId IS NULL OR DealerId = @dealerId) AND Status IN (0, 1, 2, 3, 5)) AS OpenQuotes,
  (SELECT ISNULL(SUM(l.NetPrice * l.Quantity / ISNULL(NULLIF(r.Rate, 0), 1)), 0)
     FROM dlr.SalesOrder o JOIN dlr.OrderLine l ON l.OrderId = o.Id
     OUTER APPLY (SELECT TOP (1) Rate FROM cache.ExchangeRate x WHERE x.FromCurrency = 'EUR' AND x.ToCurrency = o.Currency ORDER BY ValidFrom DESC) r
     WHERE (@dealerId IS NULL OR o.DealerId = @dealerId) AND o.CreatedUtc >= DATEADD(day, -30, @today) AND o.Status NOT IN (0, 12, 13)) AS OrderIntakeEur30Days",
                    new { dealerId, today = todayLocal.Date });
        }

        public IList<StockRow> Stock(string search, string warehouse, int max = 200)
        {
            using (var c = _connections.Open())
                return c.Query<StockRow>(@"
SELECT TOP (@max) s.ItemId, i.Name, s.ConfigurationId, s.Warehouse, s.PhysicalOnHand - s.Reserved AS Available, s.OnOrder, s.NextReceiptDate, s.UpdatedUtc
FROM cache.Stock s LEFT JOIN cache.Item i ON i.ItemId = s.ItemId
WHERE (@search IS NULL OR s.ItemId LIKE @like OR i.Name LIKE @like) AND (@warehouse IS NULL OR s.Warehouse = @warehouse)
ORDER BY s.ItemId, s.Warehouse", new { search, like = "%" + search + "%", warehouse, max }).ToList();
        }
    }
}
