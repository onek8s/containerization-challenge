using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using Dapper;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Data.Stores
{
    /// <summary>Base for the EF stores: one context per request or per unit of work in the services.</summary>
    public abstract class EfStore
    {
        protected EfStore(DealerOrdersContext db)
        {
            Db = db;
        }

        protected DealerOrdersContext Db { get; }

        protected void Commit()
        {
            try
            {
                Db.SaveChanges();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new ConcurrencyException("The record was changed by someone else. Reload and try again.", ex);
            }
        }

        /// <summary>Per-year document numbers from dlr.NumberSequence (UPDATE ... OUTPUT, no gaps on rollback).</summary>
        protected long NextNumber(string sequence, int year)
        {
            const string sql = @"
MERGE dlr.NumberSequence WITH (HOLDLOCK) AS t
USING (SELECT @sequence AS Sequence, @year AS [Year]) AS s ON t.Sequence = s.Sequence AND t.[Year] = s.[Year]
WHEN MATCHED THEN UPDATE SET LastValue = t.LastValue + 1
WHEN NOT MATCHED THEN INSERT (Sequence, [Year], LastValue) VALUES (s.Sequence, s.[Year], 1)
OUTPUT inserted.LastValue;";
            var connection = Db.Database.Connection;
            var transaction = Db.Database.CurrentTransaction?.UnderlyingTransaction;
            return connection.ExecuteScalar<long>(sql, new { sequence, year }, transaction);
        }
    }

    [Serializable]
    public class ConcurrencyException : Exception
    {
        public ConcurrencyException(string message, Exception inner) : base(message, inner) { }
        protected ConcurrencyException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    public class EfDealerStore : EfStore, IDealerStore
    {
        public EfDealerStore(DealerOrdersContext db) : base(db) { }

        public Dealer Get(int id) => Db.Dealers.Include(d => d.Addresses).SingleOrDefault(d => d.Id == id);

        public Dealer FindByAccount(string accountNumber) => Db.Dealers.Include(d => d.Addresses).SingleOrDefault(d => d.AccountNumber == accountNumber);

        public IList<Dealer> All() => Db.Dealers.Include(d => d.Addresses).OrderBy(d => d.AccountNumber).ToList();

        public void Save(Dealer dealer)
        {
            if (dealer.Id == 0) Db.Dealers.Add(dealer);
            else if (Db.Entry(dealer).State == EntityState.Detached) Db.Dealers.Attach(dealer);
            Commit();
        }
    }

    public class EfQuoteStore : EfStore, IQuoteStore
    {
        public EfQuoteStore(DealerOrdersContext db) : base(db) { }

        private IQueryable<Quote> Quotes => Db.Quotes.Include(q => q.Lines).Include(q => q.Dealer);

        public Quote Get(int id) => Quotes.SingleOrDefault(q => q.Id == id);

        public Quote FindByNumber(string quoteNumber) => Quotes.SingleOrDefault(q => q.QuoteNumber == quoteNumber);

        public IList<Quote> ForDealer(int dealerId, int max) => Quotes.Where(q => q.DealerId == dealerId).OrderByDescending(q => q.Id).Take(max).ToList();

        public IList<Quote> Open() => Quotes.Where(q => q.Status == QuoteStatus.Draft || q.Status == QuoteStatus.Submitted || q.Status == QuoteStatus.PendingApproval ||
                                                        q.Status == QuoteStatus.Approved || q.Status == QuoteStatus.RegisteredInAx).ToList();

        public void Add(Quote quote)
        {
            Db.Quotes.Add(quote);
            Commit();
        }

        public void Save(Quote quote)
        {
            // Lines removed from the collection are deleted, not orphaned.
            foreach (var removed in Db.QuoteLines.Local.Where(l => l.QuoteId == quote.Id && !quote.Lines.Contains(l)).ToList())
                Db.QuoteLines.Remove(removed);
            Commit();
        }

        public long NextNumber(int year) => NextNumber("Quote", year);
    }

    public class EfOrderStore : EfStore, IOrderStore
    {
        public EfOrderStore(DealerOrdersContext db) : base(db) { }

        private IQueryable<SalesOrder> Orders => Db.Orders.Include(o => o.Lines).Include(o => o.History).Include(o => o.DeliveryAddress).Include(o => o.Dealer);

        public SalesOrder Get(int id) => Orders.SingleOrDefault(o => o.Id == id);

        public SalesOrder FindByNumber(string orderNumber) => Orders.SingleOrDefault(o => o.OrderNumber == orderNumber);

        public SalesOrder FindByAxSalesId(string salesId) => Orders.SingleOrDefault(o => o.AxSalesId == salesId);

        public SalesOrder FindByDealerPo(int dealerId, string dealerPoNumber) =>
            Orders.Where(o => o.DealerId == dealerId && o.DealerPoNumber == dealerPoNumber).OrderByDescending(o => o.Id).FirstOrDefault();

        public IList<SalesOrder> ForDealer(int dealerId, int max) => Orders.Where(o => o.DealerId == dealerId).OrderByDescending(o => o.Id).Take(max).ToList();

        public IList<SalesOrder> OpenInAx() =>
            Orders.Where(o => o.AxSalesId != null && o.Status != OrderStatus.Invoiced && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Rejected).ToList();

        public decimal OpenOrderValue(int dealerId, int? excludeOrderId)
        {
            const string sql = @"
SELECT ISNULL(SUM(l.NetPrice * l.Quantity), 0) + ISNULL((SELECT SUM(o2.FreightCharge) FROM dlr.SalesOrder o2
        WHERE o2.DealerId = @dealerId AND o2.Status BETWEEN @approved AND @shipped AND o2.Status <> @error AND (@exclude IS NULL OR o2.Id <> @exclude)), 0)
FROM dlr.SalesOrder o
JOIN dlr.OrderLine l ON l.OrderId = o.Id AND l.Cancelled = 0
WHERE o.DealerId = @dealerId AND o.Status BETWEEN @approved AND @shipped AND o.Status <> @error AND (@exclude IS NULL OR o.Id <> @exclude);";
            return Db.Database.Connection.ExecuteScalar<decimal>(sql, new
            {
                dealerId,
                exclude = excludeOrderId,
                approved = (int)OrderStatus.Approved,
                shipped = (int)OrderStatus.Shipped,
                error = (int)OrderStatus.Error
            }, Db.Database.CurrentTransaction?.UnderlyingTransaction);
        }

        public void Add(SalesOrder order)
        {
            if (order.DeliveryAddress != null) Db.Entry(order.DeliveryAddress).State = EntityState.Unchanged;
            Db.Orders.Add(order);
            Commit();
        }

        public void Save(SalesOrder order) => Commit();

        public long NextNumber(int year) => NextNumber("Order", year);
    }

    public class EfApprovalStore : EfStore, IApprovalStore
    {
        public EfApprovalStore(DealerOrdersContext db) : base(db) { }

        public ApprovalRequest Get(int id) => Db.Approvals.Find(id);
        public IList<ApprovalRequest> Pending() => Db.Approvals.Where(a => a.Decision == ApprovalDecision.Pending).OrderBy(a => a.RequestedUtc).ToList();
        public IList<ApprovalRequest> ForOrder(int orderId) => Db.Approvals.Where(a => a.OrderId == orderId).ToList();
        public IList<ApprovalRequest> ForQuote(int quoteId) => Db.Approvals.Where(a => a.QuoteId == quoteId).ToList();

        public void Add(ApprovalRequest request)
        {
            Db.Approvals.Add(request);
            Commit();
        }

        public void Save(ApprovalRequest request) => Commit();
    }

    public class EfShipmentStore : EfStore, IShipmentStore
    {
        public EfShipmentStore(DealerOrdersContext db) : base(db) { }

        public Shipment Find(int orderId, string packingSlipId) => Db.Shipments.Include(s => s.Lines).SingleOrDefault(s => s.OrderId == orderId && s.PackingSlipId == packingSlipId);
        public IList<Shipment> ForOrder(int orderId) => Db.Shipments.Include(s => s.Lines).Where(s => s.OrderId == orderId).OrderBy(s => s.ShipDate).ToList();

        public void Add(Shipment shipment)
        {
            Db.Shipments.Add(shipment);
            Commit();
        }

        public void Save(Shipment shipment) => Commit();
    }

    public class EfNotificationStore : EfStore, INotificationStore
    {
        public EfNotificationStore(DealerOrdersContext db) : base(db) { }

        public void Add(Notification notification)
        {
            Db.Notifications.Add(notification);
            Commit();
        }

        public IList<Notification> ClaimPending(int max, DateTime utcNow)
        {
            var retryBefore = utcNow.AddMinutes(-5);
            return Db.Notifications
                .Where(n => (n.MailSentUtc == null || (n.PushPayload != null && n.PushedUtc == null)) && n.Attempts < 5 && n.CreatedUtc > System.Data.Entity.DbFunctions.AddDays(utcNow, -3))
                .Where(n => n.Attempts == 0 || n.CreatedUtc < retryBefore)
                .OrderBy(n => n.Id).Take(max).ToList();
        }

        public void Save(Notification notification) => Commit();
    }

    public class EfEdiStore : EfStore, IEdiStore
    {
        public EfEdiStore(DealerOrdersContext db) : base(db) { }

        public EdiPartner FindPartner(string interchangeQualifier, string interchangeId) =>
            Db.EdiPartners.SingleOrDefault(p => p.InterchangeQualifier == interchangeQualifier && p.InterchangeId == interchangeId);

        public EdiPartner FindPartnerByAs2Id(string as2Id) => Db.EdiPartners.SingleOrDefault(p => p.As2Id == as2Id);
        public EdiPartner GetPartner(int id) => Db.EdiPartners.Find(id);
        public EdiPartner PartnerForDealer(int dealerId) => Db.EdiPartners.FirstOrDefault(p => p.DealerId == dealerId);
        public IList<EdiPartner> Partners() => Db.EdiPartners.OrderBy(p => p.Name).ToList();
        public void SavePartner(EdiPartner partner) => Commit();

        public int NextControlNumber(int partnerId)
        {
            var value = Db.Database.Connection.ExecuteScalar<int>(
                "UPDATE edi.Partner SET LastInterchangeControl = CASE WHEN LastInterchangeControl >= 999999999 THEN 1 ELSE LastInterchangeControl + 1 END OUTPUT inserted.LastInterchangeControl WHERE Id = @partnerId",
                new { partnerId }, Db.Database.CurrentTransaction?.UnderlyingTransaction);
            var tracked = Db.EdiPartners.Local.FirstOrDefault(p => p.Id == partnerId);
            if (tracked != null) Db.Entry(tracked).Property(p => p.LastInterchangeControl).OriginalValue = tracked.LastInterchangeControl = value;
            return value;
        }

        public EdiMessage GetMessage(int id) => Db.EdiMessages.Find(id);

        public void AddMessage(EdiMessage message)
        {
            Db.EdiMessages.Add(message);
            Commit();
        }

        public void SaveMessage(EdiMessage message) => Commit();

        public IList<EdiMessage> Outbound(EdiMessageState state, int max) =>
            Db.EdiMessages.Where(m => m.Direction == EdiDirection.Outbound && m.State == state).OrderBy(m => m.Id).Take(max).ToList();

        public IList<EdiMessage> ForOrder(int orderId) => Db.EdiMessages.Where(m => m.OrderId == orderId).OrderBy(m => m.Id).ToList();

        public EdiMessage FindOutbound(int partnerId, string groupControl) =>
            Db.EdiMessages.Where(m => m.PartnerId == partnerId && m.Direction == EdiDirection.Outbound && m.GroupControl == groupControl).OrderByDescending(m => m.Id).FirstOrDefault();
    }
}
