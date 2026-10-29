using System;
using System.Collections.Generic;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.Processing.Ports
{
    public interface IClock
    {
        DateTime UtcNow { get; }

        /// <summary>Today in the factory time zone (Finland); order and quote dates follow the factory calendar.</summary>
        DateTime Today { get; }
    }

    public interface IDealerStore
    {
        Dealer Get(int id);
        Dealer FindByAccount(string accountNumber);
        IList<Dealer> All();
        void Save(Dealer dealer);
    }

    public interface ICatalog
    {
        ProductModel Get(string modelCode);
        ProductModel FindByItemId(string itemId);
        IList<ProductModel> All();
        string PartDescription(string itemId);
    }

    public interface IQuoteStore
    {
        Quote Get(int id);
        Quote FindByNumber(string quoteNumber);
        IList<Quote> ForDealer(int dealerId, int max);
        IList<Quote> Open();
        void Add(Quote quote);
        void Save(Quote quote);
        long NextNumber(int year);
    }

    public interface IOrderStore
    {
        SalesOrder Get(int id);
        SalesOrder FindByNumber(string orderNumber);
        SalesOrder FindByAxSalesId(string salesId);
        SalesOrder FindByDealerPo(int dealerId, string dealerPoNumber);
        IList<SalesOrder> ForDealer(int dealerId, int max);
        IList<SalesOrder> OpenInAx();

        /// <summary>Net value of the dealer's orders that are approved but not yet invoiced.</summary>
        decimal OpenOrderValue(int dealerId, int? excludeOrderId);

        void Add(SalesOrder order);
        void Save(SalesOrder order);
        long NextNumber(int year);
    }

    public interface IApprovalStore
    {
        ApprovalRequest Get(int id);
        IList<ApprovalRequest> Pending();
        IList<ApprovalRequest> ForOrder(int orderId);
        IList<ApprovalRequest> ForQuote(int quoteId);
        void Add(ApprovalRequest request);
        void Save(ApprovalRequest request);
    }

    public interface IShipmentStore
    {
        Shipment Find(int orderId, string packingSlipId);
        IList<Shipment> ForOrder(int orderId);
        void Add(Shipment shipment);
        void Save(Shipment shipment);
    }

    public interface IAxCommandQueue
    {
        void Enqueue(AxCommand command);

        /// <summary>
        /// Claims the next due command whose sequence key has no older unfinished command, and marks it Running.
        /// Returns null when nothing is due.
        /// </summary>
        AxCommand ClaimNext(DateTime utcNow);

        void Save(AxCommand command);
        IList<AxCommand> ForOrder(int orderId);
        IList<AxCommand> Recent(int max);
        AxCommand Get(long id);
    }

    public interface INotificationStore
    {
        void Add(Notification notification);
        IList<Notification> ClaimPending(int max, DateTime utcNow);
        void Save(Notification notification);
    }

    public interface IEdiStore
    {
        EdiPartner FindPartner(string interchangeQualifier, string interchangeId);
        EdiPartner FindPartnerByAs2Id(string as2Id);
        EdiPartner GetPartner(int id);
        EdiPartner PartnerForDealer(int dealerId);
        IList<EdiPartner> Partners();
        void SavePartner(EdiPartner partner);

        /// <summary>Atomically increments and returns the partner's interchange control number.</summary>
        int NextControlNumber(int partnerId);

        EdiMessage GetMessage(int id);
        void AddMessage(EdiMessage message);
        void SaveMessage(EdiMessage message);
        IList<EdiMessage> Outbound(EdiMessageState state, int max);
        IList<EdiMessage> ForOrder(int orderId);
        EdiMessage FindOutbound(int partnerId, string groupControl);
    }

    /// <summary>Write side of the price and stock cache filled by the sync service.</summary>
    public interface ICacheStore
    {
        void ReplacePrices(IList<PriceEntry> prices);
        void ReplaceStock(IList<StockLevel> stock);
        void UpsertExchangeRates(IList<ExchangeRate> rates);
        void UpsertItems(IList<CachedItem> items);
        IList<StockLevel> Stock(string itemId, string configurationId);
        DateTime? LastRun(string job);
        void SetLastRun(string job, DateTime utc, int records);
    }

    public class CachedItem
    {
        public string ItemId { get; set; }
        public string Name { get; set; }
        public string ItemGroup { get; set; }
        public decimal NetWeightKg { get; set; }
        public bool Stopped { get; set; }
    }
}
