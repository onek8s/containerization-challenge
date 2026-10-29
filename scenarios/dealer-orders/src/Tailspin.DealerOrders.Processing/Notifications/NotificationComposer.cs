using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Tailspin.DealerOrders.Contracts.Portal;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Processing.Notifications
{
    /// <summary>
    /// Creates notification records (mail text from the embedded templates plus a portal push payload). The
    /// notification service sends them.
    /// </summary>
    public class NotificationComposer
    {
        private static readonly Dictionary<string, string> TemplateCache = new Dictionary<string, string>();
        private readonly INotificationStore _store;
        private readonly IClock _clock;
        private readonly ProcessingSettings _settings;

        public NotificationComposer(INotificationStore store, IClock clock, ProcessingSettings settings)
        {
            _store = store;
            _clock = clock;
            _settings = settings;
        }

        public void OrderEvent(NotificationKind kind, SalesOrder order, Dealer dealer, IDictionary<string, string> extra = null)
        {
            var values = OrderValues(order, dealer);
            if (extra != null) foreach (var kv in extra) values[kv.Key] = kv.Value;
            var push = new StatusPushDto
            {
                Kind = kind.ToString(),
                DealerId = dealer.Id,
                OrderId = order.Id,
                Number = order.OrderNumber,
                Status = order.Status.ToString(),
                Text = PushText(kind, order),
                TimestampUtc = _clock.UtcNow
            };
            Add(kind, dealer, dealer.Email, values, push, n => n.OrderId = order.Id);
        }

        public void QuoteEvent(NotificationKind kind, Quote quote, Dealer dealer, string recipient)
        {
            var lang = dealer.LanguageCode;
            var values = new Dictionary<string, string>
            {
                ["DealerName"] = dealer.Name,
                ["QuoteNumber"] = quote.QuoteNumber,
                ["EndCustomer"] = string.IsNullOrEmpty(quote.EndCustomerName) ? "your customer" : quote.EndCustomerName,
                ["Discount"] = quote.ApprovedExtraDiscount.ToString("0.##", CultureInfo.InvariantCulture),
                ["Total"] = Amount(quote.TotalNet * (100m - quote.ApprovedExtraDiscount) / 100m, quote.Currency),
                ["ValidUntil"] = quote.ValidUntil.ToString("yyyy-MM-dd"),
                ["Comment"] = quote.ApprovalComment ?? "",
                ["Link"] = _settings.PortalBaseUrl + "Quotes/Details/" + quote.Id
            };
            var push = new StatusPushDto
            {
                Kind = kind.ToString(),
                DealerId = dealer.Id,
                QuoteId = quote.Id,
                Number = quote.QuoteNumber,
                Status = quote.Status.ToString(),
                Text = kind == NotificationKind.QuoteApproved ? $"Quote {quote.QuoteNumber} approved" : $"Quote {quote.QuoteNumber} not approved",
                TimestampUtc = _clock.UtcNow
            };
            Add(kind, dealer, recipient ?? dealer.Email, values, push, n => n.QuoteId = quote.Id, lang);
        }

        public void ApprovalNeeded(ApprovalRequest request, Dealer dealer, string number)
        {
            var values = new Dictionary<string, string>
            {
                ["DealerName"] = dealer.Name,
                ["Account"] = dealer.AccountNumber,
                ["Reason"] = Describe(request.Reason),
                ["Number"] = number,
                ["Total"] = Amount(request.Amount, request.Currency),
                ["Details"] = request.Details ?? "",
                ["Link"] = _settings.SalesDeskBaseUrl + "Approvals"
            };
            Add(NotificationKind.ApprovalNeeded, dealer, _settings.SalesDeskEmail, values, null, n => { n.OrderId = request.OrderId; n.QuoteId = request.QuoteId; }, "en");
        }

        public void OrderError(SalesOrder order, Dealer dealer, AxCommand command, string error)
        {
            var values = OrderValues(order, dealer);
            values["Command"] = command.CommandType.ToString();
            values["Error"] = error;
            values["Link"] = _settings.SalesDeskBaseUrl + "Orders/Details/" + order.Id;
            Add(NotificationKind.OrderError, dealer, _settings.SalesDeskEmail, values, null, n => n.OrderId = order.Id, "en");
        }

        public void DailyDigest(Dealer dealer, IList<SalesOrder> open, IList<Quote> expiring)
        {
            var today = _clock.Today;
            var values = new Dictionary<string, string>
            {
                ["DealerName"] = dealer.Name,
                ["Date"] = today.ToString("yyyy-MM-dd"),
                ["OpenCount"] = open.Count.ToString(CultureInfo.InvariantCulture),
                ["ShippingSoon"] = open.Count(o => o.ConfirmedDeliveryDate.HasValue && o.ConfirmedDeliveryDate.Value <= today.AddDays(14)).ToString(CultureInfo.InvariantCulture),
                ["ExpiringQuotes"] = expiring.Count.ToString(CultureInfo.InvariantCulture),
                ["Lines"] = string.Join(Environment.NewLine, open.OrderBy(o => o.ConfirmedDeliveryDate ?? DateTime.MaxValue)
                    .Select(o => $"  {o.OrderNumber}  {o.DealerPoNumber,-15} {o.Status,-16} {(o.ConfirmedDeliveryDate.HasValue ? o.ConfirmedDeliveryDate.Value.ToString("yyyy-MM-dd") : "not confirmed")}")),
                ["Link"] = _settings.PortalBaseUrl + "Orders"
            };
            Add(NotificationKind.DailyDigest, dealer, dealer.Email, values, null, n => { });
        }

        private void Add(NotificationKind kind, Dealer dealer, string recipient, IDictionary<string, string> values, StatusPushDto push,
            Action<Notification> link, string language = null)
        {
            var text = Render(kind, language ?? dealer.LanguageCode, values);
            var notification = new Notification
            {
                Kind = kind,
                DealerId = dealer.Id,
                Recipient = recipient,
                Subject = text.Item1,
                Body = text.Item2,
                PushPayload = push == null ? null : JsonConvert.SerializeObject(push),
                CreatedUtc = _clock.UtcNow
            };
            link(notification);
            if (string.IsNullOrEmpty(notification.Recipient)) notification.MailSentUtc = _clock.UtcNow; // nothing to mail, push only
            _store.Add(notification);
        }

        private Dictionary<string, string> OrderValues(SalesOrder order, Dealer dealer) => new Dictionary<string, string>
        {
            ["DealerName"] = dealer.Name,
            ["Account"] = dealer.AccountNumber,
            ["SalesResponsible"] = dealer.SalesResponsible ?? "",
            ["OrderNumber"] = order.OrderNumber,
            ["DealerPo"] = order.DealerPoNumber ?? "-",
            ["AxSalesId"] = order.AxSalesId ?? "",
            ["Status"] = order.Status.ToString(),
            ["Total"] = Amount(order.OrderValue, order.Currency),
            ["DeliveryDate"] = order.ConfirmedDeliveryDate?.ToString("yyyy-MM-dd") ?? "",
            ["Reason"] = order.ErrorMessage ?? "",
            ["Lines"] = string.Join(Environment.NewLine, order.Lines.Where(l => !l.Cancelled).OrderBy(l => l.LineNumber)
                .Select(l => $"  {l.LineNumber,3}  {l.Quantity,4:0.##} x {l.ItemId,-12} {l.Description}")),
            ["Link"] = _settings.PortalBaseUrl + "Orders/Details/" + order.Id
        };

        private static string PushText(NotificationKind kind, SalesOrder order)
        {
            switch (kind)
            {
                case NotificationKind.OrderConfirmed: return $"Order {order.OrderNumber} confirmed for {order.ConfirmedDeliveryDate:yyyy-MM-dd}";
                case NotificationKind.DeliveryDateChanged: return $"Order {order.OrderNumber}: new delivery date {order.ConfirmedDeliveryDate:yyyy-MM-dd}";
                case NotificationKind.OrderShipped: return $"Order {order.OrderNumber} shipped";
                case NotificationKind.OrderInvoiced: return $"Order {order.OrderNumber} invoiced";
                case NotificationKind.OrderRejected: return $"Order {order.OrderNumber} was not accepted";
                case NotificationKind.CreditHold: return $"Order {order.OrderNumber} is waiting for credit review";
                default: return $"Order {order.OrderNumber}: {order.Status}";
            }
        }

        public static string Describe(ApprovalReason reason)
        {
            switch (reason)
            {
                case ApprovalReason.DiscountAboveLimit: return "Discount above dealer limit";
                case ApprovalReason.CreditLimitExceeded: return "Credit limit exceeded";
                case ApprovalReason.CreditHold: return "Dealer on credit hold";
                case ApprovalReason.PriceOverride: return "Price override";
                case ApprovalReason.NonStandardTerms: return "Non-standard terms";
                default: return "Export control check";
            }
        }

        private static string Amount(decimal amount, string currency) =>
            amount.ToString(Currencies.Decimals(currency ?? "EUR") == 0 ? "N0" : "N2", CultureInfo.InvariantCulture) + " " + currency;

        public static Tuple<string, string> Render(NotificationKind kind, string language, IDictionary<string, string> values)
        {
            var template = Load(kind + "." + (language ?? "en").ToLowerInvariant()) ?? Load(kind + ".en")
                ?? throw new InvalidOperationException($"No mail template for {kind}.");
            var sb = new StringBuilder(template);
            foreach (var kv in values) sb.Replace("{" + kv.Key + "}", kv.Value ?? "");
            var text = sb.ToString().Replace("\r\n", "\n");
            var firstLine = text.IndexOf('\n');
            var subject = text.Substring(0, firstLine).Substring("Subject:".Length).Trim();
            return Tuple.Create(subject, text.Substring(firstLine + 1).TrimStart('\n'));
        }

        private static string Load(string name)
        {
            lock (TemplateCache)
            {
                if (TemplateCache.TryGetValue(name, out var cached)) return cached;
                var assembly = typeof(NotificationComposer).GetTypeInfo().Assembly;
                var resource = assembly.GetManifestResourceNames().FirstOrDefault(r => r.EndsWith(".Templates." + name + ".txt", StringComparison.OrdinalIgnoreCase));
                string text = null;
                if (resource != null)
                    using (var reader = new StreamReader(assembly.GetManifestResourceStream(resource), Encoding.UTF8))
                        text = reader.ReadToEnd();
                TemplateCache[name] = text;
                return text;
            }
        }
    }
}
