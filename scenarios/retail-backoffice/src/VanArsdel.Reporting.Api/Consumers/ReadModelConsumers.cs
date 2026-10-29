using System.Data.Common;
using Dapper;
using MassTransit;
using VanArsdel.Contracts;
using VanArsdel.Contracts.Catalog;
using VanArsdel.Contracts.Inventory;
using VanArsdel.Contracts.Orders;
using VanArsdel.Contracts.Pos;
using VanArsdel.Contracts.Returns;
using VanArsdel.Contracts.Stores;
using VanArsdel.Reporting.Api.Data;

namespace VanArsdel.Reporting.Api.Consumers;

/// <summary>Base for consumers that write one message in one transaction, at most once.</summary>
public abstract class ReadModelConsumer
{
    protected ReadModelConsumer(ReportingDb db)
    {
        Db = db;
    }

    protected ReportingDb Db { get; }

    protected async Task WriteOnceAsync(ConsumeContext context, Func<DbConnection, DbTransaction, Task> write)
    {
        await using var connection = await Db.OpenAsync(context.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(context.CancellationToken);
        if (!await Db.TryMarkProcessedAsync(connection, transaction, context.MessageId ?? NewId.NextGuid(), GetType().Name))
        {
            return;
        }
        await write(connection, transaction);
        await transaction.CommitAsync(context.CancellationToken);
    }
}

public class DimensionConsumers : ReadModelConsumer,
    IConsumer<ProductPublished>, IConsumer<ProductUpdated>,
    IConsumer<StoreOpened>, IConsumer<StoreUpdated>, IConsumer<StoreClosed>
{
    public DimensionConsumers(ReportingDb db) : base(db)
    {
    }

    public Task Consume(ConsumeContext<ProductPublished> context) =>
        UpsertProducts(context, context.Message.StyleCode, context.Message.Name, context.Message.Brand, context.Message.CategoryCode, context.Message.Season, context.Message.Skus);

    public Task Consume(ConsumeContext<ProductUpdated> context) =>
        UpsertProducts(context, context.Message.StyleCode, context.Message.Name, null, context.Message.CategoryCode, null, context.Message.Skus);

    private Task UpsertProducts(ConsumeContext context, string style, string name, string brand, string category, string season, List<SkuInfo> skus) =>
        WriteOnceAsync(context, async (c, tx) =>
        {
            var sql = Db.Upsert("DimProduct", new[] { "Sku" }, new[] { "StyleCode", "Name", "Brand", "CategoryCode", "Season", "Colour", "Size", "UpdatedUtc" });
            foreach (var sku in skus)
            {
                // ProductUpdated has no brand or season; keep what ProductPublished wrote.
                var existing = await c.QuerySingleOrDefaultAsync<(string Brand, string Season)>(
                    "SELECT Brand, Season FROM DimProduct WHERE Sku = @Sku", new { sku.Sku }, tx);
                await c.ExecuteAsync(sql, new
                {
                    sku.Sku, StyleCode = style, Name = name, Brand = brand ?? existing.Brand, CategoryCode = category,
                    Season = season ?? existing.Season, sku.Colour, sku.Size, UpdatedUtc = DateTime.UtcNow,
                }, tx);
            }
        });

    public Task Consume(ConsumeContext<StoreOpened> context) => WriteOnceAsync(context, (c, tx) =>
    {
        var m = context.Message;
        var sql = Db.Upsert("DimStore", new[] { "StoreCode" }, new[] { "Name", "LocationType", "Country", "City", "Currency", "OpenedOn" });
        return c.ExecuteAsync(sql, new { m.StoreCode, m.Name, LocationType = m.LocationType ?? "Store", m.Country, m.City, Currency = m.Currency ?? Markets.CurrencyOf(m.Country), OpenedOn = m.OpenedOn.Date }, tx);
    });

    public Task Consume(ConsumeContext<StoreUpdated> context) => WriteOnceAsync(context, (c, tx) =>
        c.ExecuteAsync("UPDATE DimStore SET Name = @Name WHERE StoreCode = @StoreCode", new { context.Message.Name, context.Message.StoreCode }, tx));

    public Task Consume(ConsumeContext<StoreClosed> context) => WriteOnceAsync(context, (c, tx) =>
        c.ExecuteAsync("UPDATE DimStore SET ClosedOn = @ClosedOn WHERE StoreCode = @StoreCode", new { ClosedOn = context.Message.ClosedOn.Date, context.Message.StoreCode }, tx));
}

public class SalesConsumers : ReadModelConsumer,
    IConsumer<StoreSalesReceived>,
    IConsumer<OrderSubmitted>, IConsumer<OrderCompleted>, IConsumer<OrderCancelled>,
    IConsumer<ReturnCompleted>, IConsumer<StockLevelLow>
{
    private const string InsertSale =
        "INSERT INTO FactSales (Channel, LocationCode, Market, BusinessDate, SourceRef, Sku, Quantity, NetAmount, NetAmountEur, Currency, PromotionCode, Loyalty) " +
        "VALUES (@Channel, @LocationCode, @Market, @BusinessDate, @SourceRef, @Sku, @Quantity, @NetAmount, @NetAmountEur, @Currency, @PromotionCode, @Loyalty)";

    private readonly CurrencyOptions _currency;

    public SalesConsumers(ReportingDb db, CurrencyOptions currency) : base(db)
    {
        _currency = currency;
    }

    public Task Consume(ConsumeContext<StoreSalesReceived> context) => WriteOnceAsync(context, async (c, tx) =>
    {
        var m = context.Message;
        var market = m.StoreCode.Substring(0, 2);
        var vat = Markets.StandardVat.TryGetValue(market, out var v) ? v : 0.24m;
        var rows = new List<object>();
        foreach (var receipt in m.Receipts)
        {
            foreach (var line in receipt.Lines)
            {
                // POS line totals include VAT; reporting is on net sales.
                var net = Math.Round(line.LineTotal / (1 + vat), 2);
                var sign = receipt.IsReturn ? -1 : 1;
                rows.Add(new
                {
                    Channel = "Store", LocationCode = m.StoreCode, Market = market, BusinessDate = m.BusinessDate.Date,
                    SourceRef = receipt.TillNo + "/" + receipt.ReceiptNo, line.Sku,
                    Quantity = sign * Math.Abs(line.Quantity), NetAmount = sign * Math.Abs(net), NetAmountEur = sign * Math.Abs(_currency.ToEur(net, m.Currency)),
                    Currency = m.Currency, line.PromotionCode, Loyalty = !string.IsNullOrEmpty(receipt.LoyaltyCardNo),
                });
            }
        }
        await c.ExecuteAsync(InsertSale, rows, tx);
    });

    public Task Consume(ConsumeContext<OrderSubmitted> context) => WriteOnceAsync(context, (c, tx) =>
    {
        var m = context.Message;
        var sql = Db.Upsert("FactWebOrder", new[] { "OrderId" },
            new[] { "OrderNo", "Channel", "Market", "DeliveryMethod", "CollectStoreCode", "Total", "TotalEur", "Currency", "Lines", "Status", "SubmittedUtc" });
        return c.ExecuteAsync(sql, new
        {
            OrderId = Db.Key(m.OrderId), m.OrderNo, m.Channel, m.Market, m.DeliveryMethod, m.CollectStoreCode, m.Total,
            TotalEur = _currency.ToEur(m.Total, m.Currency), m.Currency, Lines = m.Lines.Sum(l => l.Quantity), Status = "Open", SubmittedUtc = DateTime.UtcNow,
        }, tx);
    });

    /// <summary>Web sales are booked on completion (shipped and paid), as finance books them.</summary>
    public Task Consume(ConsumeContext<OrderCompleted> context) => WriteOnceAsync(context, async (c, tx) =>
    {
        var m = context.Message;
        await c.ExecuteAsync("UPDATE FactWebOrder SET Status = 'Completed', CompletedUtc = @CompletedUtc WHERE OrderId = @OrderId",
            new { m.CompletedUtc, OrderId = Db.Key(m.OrderId) }, tx);
        if (m.Channel == "Store")
        {
            return; // paid at the till, already in the POS figures
        }
        var vat = Markets.StandardVat.TryGetValue(m.Market ?? "FI", out var v) ? v : 0.24m;
        var rows = m.Lines.Select(l =>
        {
            var net = Math.Round((l.UnitPrice * l.Quantity - l.Discount) / (1 + vat), 2);
            return new
            {
                Channel = "Web", LocationCode = "WEB-" + m.Market, m.Market, BusinessDate = m.CompletedUtc.Date, SourceRef = m.OrderNo, l.Sku, l.Quantity,
                NetAmount = net, NetAmountEur = _currency.ToEur(net, m.Currency), m.Currency, PromotionCode = (string)null, Loyalty = !string.IsNullOrEmpty(m.LoyaltyCardNo),
            };
        });
        await c.ExecuteAsync(InsertSale, rows, tx);
    });

    public Task Consume(ConsumeContext<OrderCancelled> context) => WriteOnceAsync(context, (c, tx) =>
        c.ExecuteAsync("UPDATE FactWebOrder SET Status = 'Cancelled', CancelReason = @Reason, CancelledUtc = @Now WHERE OrderId = @OrderId",
            new { Reason = Truncate(context.Message.Reason, 200), Now = DateTime.UtcNow, OrderId = Db.Key(context.Message.OrderId) }, tx));

    public Task Consume(ConsumeContext<ReturnCompleted> context) => WriteOnceAsync(context, async (c, tx) =>
    {
        var m = context.Message;
        var units = Math.Max(1, m.Lines.Sum(l => l.Quantity));
        var rows = m.Lines.Select(l => new
        {
            m.RmaNo, m.OrderNo, m.StoreCode, l.Sku, l.Quantity, l.Grade,
            // The refund is per return; spread it over the units for the per-style view.
            RefundAmount = Math.Round(m.RefundAmount * l.Quantity / units, 2),
            RefundAmountEur = Math.Round(_currency.ToEur(m.RefundAmount, m.Currency) * l.Quantity / units, 2),
            m.Currency, m.RefundMethod, BusinessDate = m.CompletedUtc.Date,
        });
        await c.ExecuteAsync(
            "INSERT INTO FactReturn (RmaNo, OrderNo, StoreCode, Sku, Quantity, Grade, RefundAmount, RefundAmountEur, Currency, RefundMethod, BusinessDate) " +
            "VALUES (@RmaNo, @OrderNo, @StoreCode, @Sku, @Quantity, @Grade, @RefundAmount, @RefundAmountEur, @Currency, @RefundMethod, @BusinessDate)", rows, tx);
    });

    public Task Consume(ConsumeContext<StockLevelLow> context) => WriteOnceAsync(context, (c, tx) =>
        c.ExecuteAsync("INSERT INTO FactStockAlert (LocationCode, Sku, OnHand, RaisedUtc) VALUES (@LocationCode, @Sku, @Available, @Now)",
            new { context.Message.LocationCode, context.Message.Sku, context.Message.Available, Now = DateTime.UtcNow }, tx));

    private static string Truncate(string value, int length) => value == null || value.Length <= length ? value : value.Substring(0, length);
}
