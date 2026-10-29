using System;
using System.Collections.Generic;
using System.Linq;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.LabStubs
{
    /// <summary>
    /// In-memory implementation of all stores, used by unit tests and the lab "flow" run. Objects are kept by
    /// reference, so Save only has to remember new ids; it mirrors what the SQL stores do with the same calls.
    /// </summary>
    public class InMemoryStores : IDealerStore, IQuoteStore, IOrderStore, IApprovalStore, IShipmentStore, IAxCommandQueue, INotificationStore, IEdiStore, ICacheStore, IPriceSource
    {
        private readonly object _gate = new object();
        private readonly Dictionary<int, Dealer> _dealers = new Dictionary<int, Dealer>();
        private readonly Dictionary<int, Quote> _quotes = new Dictionary<int, Quote>();
        private readonly Dictionary<int, SalesOrder> _orders = new Dictionary<int, SalesOrder>();
        private readonly Dictionary<int, ApprovalRequest> _approvals = new Dictionary<int, ApprovalRequest>();
        private readonly List<Shipment> _shipments = new List<Shipment>();
        private readonly Dictionary<long, AxCommand> _commands = new Dictionary<long, AxCommand>();
        private readonly List<Notification> _notifications = new List<Notification>();
        private readonly Dictionary<int, EdiPartner> _partners = new Dictionary<int, EdiPartner>();
        private readonly Dictionary<int, EdiMessage> _messages = new Dictionary<int, EdiMessage>();
        private readonly Dictionary<string, long> _sequences = new Dictionary<string, long>();
        private readonly Dictionary<string, Tuple<DateTime, int>> _runs = new Dictionary<string, Tuple<DateTime, int>>();
        private List<PriceEntry> _prices = new List<PriceEntry>();
        private List<StockLevel> _stock = new List<StockLevel>();
        private List<ExchangeRate> _rates = new List<ExchangeRate>();
        private List<CachedItem> _items = new List<CachedItem>();
        private int _nextId = 1000;

        public static InMemoryStores WithLabData()
        {
            var stores = new InMemoryStores();
            foreach (var dealer in LabData.Dealers()) stores._dealers[dealer.Id] = dealer;
            foreach (var partner in LabData.EdiPartners()) stores._partners[partner.Id] = partner;
            stores._prices = LabData.Prices().ToList();
            stores._rates = LabData.ExchangeRates().ToList();
            return stores;
        }

        public IList<Notification> Notifications { get { lock (_gate) return _notifications.ToList(); } }
        public IList<EdiMessage> Messages { get { lock (_gate) return _messages.Values.OrderBy(m => m.Id).ToList(); } }
        public IList<AxCommand> Commands { get { lock (_gate) return _commands.Values.OrderBy(c => c.Id).ToList(); } }
        public IList<SalesOrder> Orders { get { lock (_gate) return _orders.Values.OrderBy(o => o.Id).ToList(); } }
        public IList<ApprovalRequest> Approvals { get { lock (_gate) return _approvals.Values.OrderBy(a => a.Id).ToList(); } }

        private int NewId() => ++_nextId;

        // Dealers
        public Dealer Get(int id) { lock (_gate) return _dealers.TryGetValue(id, out var d) ? d : null; }
        public Dealer FindByAccount(string accountNumber) { lock (_gate) return _dealers.Values.FirstOrDefault(d => d.AccountNumber == accountNumber); }
        public IList<Dealer> All() { lock (_gate) return _dealers.Values.ToList(); }
        public void Save(Dealer dealer) { lock (_gate) { if (dealer.Id == 0) dealer.Id = NewId(); _dealers[dealer.Id] = dealer; } }

        // Quotes
        Quote IQuoteStore.Get(int id) { lock (_gate) return _quotes.TryGetValue(id, out var q) ? q : null; }
        public Quote FindByNumber(string quoteNumber) { lock (_gate) return _quotes.Values.FirstOrDefault(q => q.QuoteNumber == quoteNumber); }
        IList<Quote> IQuoteStore.ForDealer(int dealerId, int max) { lock (_gate) return _quotes.Values.Where(q => q.DealerId == dealerId).OrderByDescending(q => q.Id).Take(max).ToList(); }
        public IList<Quote> Open() { lock (_gate) return _quotes.Values.Where(q => q.Status != QuoteStatus.Converted && q.Status != QuoteStatus.Expired && q.Status != QuoteStatus.Cancelled && q.Status != QuoteStatus.Rejected).ToList(); }
        public void Add(Quote quote) { lock (_gate) { quote.Id = NewId(); foreach (var l in quote.Lines) l.QuoteId = quote.Id; _quotes[quote.Id] = quote; } }
        public void Save(Quote quote) { lock (_gate) { foreach (var l in quote.Lines) l.QuoteId = quote.Id; _quotes[quote.Id] = quote; } }
        long IQuoteStore.NextNumber(int year) => Next("quote-" + year);

        // Orders
        SalesOrder IOrderStore.Get(int id) { lock (_gate) return _orders.TryGetValue(id, out var o) ? o : null; }
        SalesOrder IOrderStore.FindByNumber(string orderNumber) { lock (_gate) return _orders.Values.FirstOrDefault(o => o.OrderNumber == orderNumber); }
        public SalesOrder FindByAxSalesId(string salesId) { lock (_gate) return _orders.Values.FirstOrDefault(o => o.AxSalesId == salesId); }
        public SalesOrder FindByDealerPo(int dealerId, string dealerPoNumber) { lock (_gate) return _orders.Values.Where(o => o.DealerId == dealerId && o.DealerPoNumber == dealerPoNumber).OrderByDescending(o => o.Id).FirstOrDefault(); }
        IList<SalesOrder> IOrderStore.ForDealer(int dealerId, int max) { lock (_gate) return _orders.Values.Where(o => o.DealerId == dealerId).OrderByDescending(o => o.Id).Take(max).ToList(); }
        public IList<SalesOrder> OpenInAx() { lock (_gate) return _orders.Values.Where(o => o.AxSalesId != null && o.IsOpen).ToList(); }

        public decimal OpenOrderValue(int dealerId, int? excludeOrderId)
        {
            lock (_gate)
                return _orders.Values.Where(o => o.DealerId == dealerId && o.Id != excludeOrderId && o.IsOpen && o.Status >= OrderStatus.Approved && o.Status != OrderStatus.Error)
                    .Sum(o => o.OrderValue);
        }

        public void Add(SalesOrder order)
        {
            lock (_gate)
            {
                order.Id = NewId();
                foreach (var l in order.Lines) { l.OrderId = order.Id; if (l.Id == 0) l.Id = NewId(); }
                foreach (var h in order.History) h.OrderId = order.Id;
                _orders[order.Id] = order;
            }
        }

        public void Save(SalesOrder order)
        {
            lock (_gate)
            {
                foreach (var l in order.Lines) { l.OrderId = order.Id; if (l.Id == 0) l.Id = NewId(); }
                foreach (var h in order.History) { h.OrderId = order.Id; if (h.Id == 0) h.Id = NewId(); }
                _orders[order.Id] = order;
            }
        }

        long IOrderStore.NextNumber(int year) => Next("order-" + year);

        // Approvals
        ApprovalRequest IApprovalStore.Get(int id) { lock (_gate) return _approvals.TryGetValue(id, out var a) ? a : null; }
        public IList<ApprovalRequest> Pending() { lock (_gate) return _approvals.Values.Where(a => a.Decision == ApprovalDecision.Pending).ToList(); }
        public IList<ApprovalRequest> ForOrder(int orderId) { lock (_gate) return _approvals.Values.Where(a => a.OrderId == orderId).ToList(); }
        public IList<ApprovalRequest> ForQuote(int quoteId) { lock (_gate) return _approvals.Values.Where(a => a.QuoteId == quoteId).ToList(); }
        public void Add(ApprovalRequest request) { lock (_gate) { request.Id = NewId(); _approvals[request.Id] = request; } }
        public void Save(ApprovalRequest request) { lock (_gate) _approvals[request.Id] = request; }

        // Shipments
        public Shipment Find(int orderId, string packingSlipId) { lock (_gate) return _shipments.FirstOrDefault(s => s.OrderId == orderId && s.PackingSlipId == packingSlipId); }
        IList<Shipment> IShipmentStore.ForOrder(int orderId) { lock (_gate) return _shipments.Where(s => s.OrderId == orderId).ToList(); }
        public void Add(Shipment shipment) { lock (_gate) { shipment.Id = NewId(); _shipments.Add(shipment); } }
        public void Save(Shipment shipment) { }

        // AX command queue
        public void Enqueue(AxCommand command) { lock (_gate) { command.Id = NewId(); _commands[command.Id] = command; } }

        public AxCommand ClaimNext(DateTime utcNow)
        {
            lock (_gate)
            {
                var unfinished = _commands.Values.Where(c => c.State == AxCommandState.Pending || c.State == AxCommandState.Retry || c.State == AxCommandState.Running || c.State == AxCommandState.Failed).ToList();
                var blockedKeys = new HashSet<string>(unfinished.Where(c => c.State == AxCommandState.Running || c.State == AxCommandState.Failed).Select(c => c.SequenceKey));
                foreach (var candidate in unfinished.Where(c => c.State == AxCommandState.Pending || c.State == AxCommandState.Retry).OrderBy(c => c.Id))
                {
                    if (blockedKeys.Contains(candidate.SequenceKey)) continue;
                    var older = unfinished.Any(c => c.SequenceKey == candidate.SequenceKey && c.Id < candidate.Id);
                    blockedKeys.Add(candidate.SequenceKey);
                    if (older || candidate.NextAttemptUtc > utcNow) continue;
                    candidate.State = AxCommandState.Running;
                    return candidate;
                }
                return null;
            }
        }

        public void Save(AxCommand command) { lock (_gate) _commands[command.Id] = command; }
        IList<AxCommand> IAxCommandQueue.ForOrder(int orderId) { lock (_gate) return _commands.Values.Where(c => c.OrderId == orderId).OrderBy(c => c.Id).ToList(); }
        public IList<AxCommand> Recent(int max) { lock (_gate) return _commands.Values.OrderByDescending(c => c.Id).Take(max).ToList(); }
        AxCommand IAxCommandQueue.Get(long id) { lock (_gate) return _commands.TryGetValue(id, out var c) ? c : null; }

        // Notifications
        public void Add(Notification notification) { lock (_gate) { notification.Id = NewId(); _notifications.Add(notification); } }
        public IList<Notification> ClaimPending(int max, DateTime utcNow) { lock (_gate) return _notifications.Where(n => (n.MailSentUtc == null || (n.PushPayload != null && n.PushedUtc == null)) && n.Attempts < 5).Take(max).ToList(); }
        public void Save(Notification notification) { }

        // EDI
        public EdiPartner FindPartner(string interchangeQualifier, string interchangeId) { lock (_gate) return _partners.Values.FirstOrDefault(p => p.InterchangeQualifier == interchangeQualifier && p.InterchangeId == interchangeId); }
        public EdiPartner FindPartnerByAs2Id(string as2Id) { lock (_gate) return _partners.Values.FirstOrDefault(p => string.Equals(p.As2Id, as2Id, StringComparison.OrdinalIgnoreCase)); }
        public EdiPartner GetPartner(int id) { lock (_gate) return _partners.TryGetValue(id, out var p) ? p : null; }
        public EdiPartner PartnerForDealer(int dealerId) { lock (_gate) return _partners.Values.FirstOrDefault(p => p.DealerId == dealerId); }
        public IList<EdiPartner> Partners() { lock (_gate) return _partners.Values.ToList(); }
        public void SavePartner(EdiPartner partner) { lock (_gate) _partners[partner.Id] = partner; }
        public int NextControlNumber(int partnerId) { lock (_gate) return ++_partners[partnerId].LastInterchangeControl; }
        public EdiMessage GetMessage(int id) { lock (_gate) return _messages.TryGetValue(id, out var m) ? m : null; }
        public void AddMessage(EdiMessage message) { lock (_gate) { message.Id = NewId(); _messages[message.Id] = message; } }
        public void SaveMessage(EdiMessage message) { lock (_gate) _messages[message.Id] = message; }
        public IList<EdiMessage> Outbound(EdiMessageState state, int max) { lock (_gate) return _messages.Values.Where(m => m.Direction == EdiDirection.Outbound && m.State == state).OrderBy(m => m.Id).Take(max).ToList(); }
        IList<EdiMessage> IEdiStore.ForOrder(int orderId) { lock (_gate) return _messages.Values.Where(m => m.OrderId == orderId).ToList(); }
        public EdiMessage FindOutbound(int partnerId, string groupControl) { lock (_gate) return _messages.Values.FirstOrDefault(m => m.PartnerId == partnerId && m.Direction == EdiDirection.Outbound && m.GroupControl == groupControl); }

        // Cache
        public void ReplacePrices(IList<PriceEntry> prices) { lock (_gate) _prices = prices.ToList(); }
        public void ReplaceStock(IList<StockLevel> stock) { lock (_gate) _stock = stock.ToList(); }

        public void UpsertExchangeRates(IList<ExchangeRate> rates)
        {
            lock (_gate)
                foreach (var rate in rates)
                {
                    _rates.RemoveAll(r => r.FromCurrency == rate.FromCurrency && r.ToCurrency == rate.ToCurrency && r.ValidFrom == rate.ValidFrom);
                    _rates.Add(rate);
                }
        }

        public void UpsertItems(IList<CachedItem> items) { lock (_gate) _items = items.ToList(); }
        public IList<StockLevel> Stock(string itemId, string configurationId) { lock (_gate) return _stock.Where(s => s.ItemId == itemId && (configurationId == null || s.ConfigurationId == configurationId)).ToList(); }
        public DateTime? LastRun(string job) { lock (_gate) return _runs.TryGetValue(job, out var r) ? r.Item1 : (DateTime?)null; }
        public void SetLastRun(string job, DateTime utc, int records) { lock (_gate) _runs[job] = Tuple.Create(utc, records); }

        // Price source
        public IEnumerable<PriceEntry> PricesFor(string itemId) { lock (_gate) return _prices.Where(p => string.Equals(p.ItemId, itemId, StringComparison.OrdinalIgnoreCase)).ToList(); }
        public IEnumerable<DiscountEntry> Discounts() => LabData.Discounts();
        public IEnumerable<Campaign> Campaigns(DateTime date) => LabData.Campaigns().Where(c => c.FromDate <= date && c.ToDate >= date);
        public IEnumerable<FreightRate> FreightRates(string countryCode) => LabData.FreightRates().Where(f => f.CountryCode == countryCode);
        public IEnumerable<ExchangeRate> ExchangeRates() { lock (_gate) return _rates.ToList(); }

        private long Next(string key)
        {
            lock (_gate)
            {
                _sequences.TryGetValue(key, out var value);
                _sequences[key] = ++value;
                return value;
            }
        }
    }

    public class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow) { UtcNow = utcNow; }
        public DateTime UtcNow { get; set; }
        public DateTime Today => UtcNow.AddHours(3).Date;
        public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
    }
}
