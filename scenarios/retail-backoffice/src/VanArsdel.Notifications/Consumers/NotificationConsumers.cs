using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Refit;
using VanArsdel.Contracts.Customers;
using VanArsdel.Contracts.Orders;
using VanArsdel.Contracts.Reporting;
using VanArsdel.Contracts.Returns;
using VanArsdel.Notifications.Data;
using VanArsdel.Notifications.Sending;

namespace VanArsdel.Notifications.Consumers;

public interface IStoreOpsApi
{
    [Get("/api/stores/{code}")]
    Task<StoreInfo> GetStoreAsync(string code);
}

public class StoreInfo
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string OpeningHours { get; set; }
}

public class CustomerNotificationConsumers :
    IConsumer<OrderConfirmed>,
    IConsumer<OrderShippedNotice>,
    IConsumer<OrderReadyForCollection>,
    IConsumer<OrderCancelled>,
    IConsumer<ReturnRegistered>,
    IConsumer<ReturnCompleted>,
    IConsumer<LoyaltyTierChanged>
{
    private static readonly Dictionary<string, string> CollectSms = new()
    {
        ["fi"] = "VanArsdel: tilauksesi {0} on noudettavissa myymälästä {1} {2:d.M.} asti.",
        ["sv"] = "VanArsdel: din beställning {0} kan hämtas i butiken {1} till {2:d/M}.",
        ["en"] = "VanArsdel: your order {0} is ready for collection at {1} until {2:d MMM}.",
    };

    private readonly Notifier _notifier;
    private readonly IStoreOpsApi _stores;
    private readonly IMemoryCache _cache;

    public CustomerNotificationConsumers(Notifier notifier, IStoreOpsApi stores, IMemoryCache cache)
    {
        _notifier = notifier;
        _stores = stores;
        _cache = cache;
    }

    public Task Consume(ConsumeContext<OrderConfirmed> context)
    {
        var m = context.Message;
        return _notifier.EmailAsync("order-confirmed", m.OrderNo, m.CustomerEmail, m.Market, new()
        {
            ["OrderNo"] = m.OrderNo, ["Total"] = m.Total, ["Currency"] = m.Currency, ["ShipmentCount"] = m.ShipmentCount,
        }, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<OrderShippedNotice> context)
    {
        var m = context.Message;
        var market = MarketOf(m.OrderNo);
        await _notifier.EmailAsync("order-shipped", m.OrderNo + "/" + m.TrackingNo, m.CustomerEmail, market, new()
        {
            ["OrderNo"] = m.OrderNo, ["Carrier"] = m.Carrier, ["TrackingNo"] = m.TrackingNo, ["TrackingUrl"] = m.TrackingUrl,
        }, context.CancellationToken);
        await _notifier.SmsAsync("order-shipped", m.OrderNo + "/" + m.TrackingNo, m.CustomerPhone,
            $"VanArsdel: {m.OrderNo} -> {m.Carrier} {m.TrackingNo} {m.TrackingUrl}", context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<OrderReadyForCollection> context)
    {
        var m = context.Message;
        var market = m.StoreCode.Substring(0, 2);
        var store = await StoreAsync(m.StoreCode);
        var storeName = store == null ? m.StoreCode : $"{store.Name}, {store.Street}, {store.City}";
        await _notifier.EmailAsync("ready-for-collection", m.OrderNo, m.CustomerEmail, market, new()
        {
            ["OrderNo"] = m.OrderNo, ["StoreName"] = storeName, ["CollectBy"] = m.CollectBy,
        }, context.CancellationToken);
        var lang = TemplateRenderer.LanguageFor(market);
        await _notifier.SmsAsync("ready-for-collection", m.OrderNo, m.CustomerPhone,
            string.Format(System.Globalization.CultureInfo.InvariantCulture, CollectSms[lang], m.OrderNo, store?.Name ?? m.StoreCode, m.CollectBy),
            context.CancellationToken);
    }

    public Task Consume(ConsumeContext<OrderCancelled> context)
    {
        var m = context.Message;
        return _notifier.EmailAsync("order-cancelled", m.OrderNo, m.CustomerEmail, MarketOf(m.OrderNo), new()
        {
            ["OrderNo"] = m.OrderNo, ["Reason"] = m.Reason,
        }, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<ReturnRegistered> context)
    {
        var m = context.Message;
        if (string.IsNullOrEmpty(m.ReturnLabelUrl)) return Task.CompletedTask; // returned in a store
        return _notifier.EmailAsync("return-registered", m.RmaNo, m.CustomerEmail, null, new()
        {
            ["RmaNo"] = m.RmaNo, ["OrderNo"] = m.OrderNo, ["ReturnLabelUrl"] = m.ReturnLabelUrl,
        }, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<ReturnCompleted> context)
    {
        var m = context.Message;
        return _notifier.EmailAsync("refund-issued", m.RmaNo, m.CustomerEmail, null, new()
        {
            ["RmaNo"] = m.RmaNo, ["RefundAmount"] = m.RefundAmount, ["Currency"] = m.Currency,
        }, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<LoyaltyTierChanged> context)
    {
        var m = context.Message;
        return _notifier.EmailAsync("tier-changed", m.CardNo + "/" + m.NewTier, m.Email, null, new()
        {
            ["FirstName"] = m.FirstName, ["PreviousTier"] = m.PreviousTier, ["NewTier"] = m.NewTier,
        }, context.CancellationToken);
    }

    private async Task<StoreInfo> StoreAsync(string code)
    {
        try
        {
            return await _cache.GetOrCreateAsync("store:" + code, e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
                return _stores.GetStoreAsync(code);
            });
        }
        catch (ApiException)
        {
            return null; // the mail goes out with the store code; better than no mail
        }
    }

    /// <summary>Web order numbers are "W" + the web shop number, whose first two letters are the market.</summary>
    private static string MarketOf(string orderNo) =>
        orderNo != null && orderNo.Length > 3 && orderNo[0] == 'W' && char.IsLetter(orderNo[1]) ? orderNo.Substring(1, 2) : null;
}

public class InternalNotificationConsumers : IConsumer<ReportReady>, IConsumer<CustomerErased>
{
    private readonly Notifier _notifier;
    private readonly NotificationsDbContext _db;

    public InternalNotificationConsumers(Notifier notifier, NotificationsDbContext db)
    {
        _notifier = notifier;
        _db = db;
    }

    public async Task Consume(ConsumeContext<ReportReady> context)
    {
        var m = context.Message;
        foreach (var to in m.Recipients)
        {
            await _notifier.EmailAsync("report-ready", m.FileName, to, null, new()
            {
                ["ReportName"] = m.ReportName, ["BusinessDate"] = m.BusinessDate, ["FileName"] = m.FileName, ["DownloadUrl"] = m.DownloadUrl,
            }, context.CancellationToken);
        }
    }

    /// <summary>GDPR erasure: the send log only holds hashes, but those are linkable too, so they go.</summary>
    public async Task Consume(ConsumeContext<CustomerErased> context)
    {
        var rows = _db.Sent.Where(s => s.RecipientHash == context.Message.EmailHash);
        _db.Sent.RemoveRange(rows);
        await _db.SaveChangesAsync(context.CancellationToken);
    }
}
