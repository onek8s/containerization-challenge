using System;
using System.Collections.Generic;

namespace Tailspin.DealerOrders.Contracts.Ax
{
    /// <summary>
    /// Writes to Dynamics AX: customers, configurations, quotations, sales orders and reservations. Implemented on top of
    /// the .NET Business Connector calling the TSDealerOrderService X++ class.
    /// </summary>
    public interface IAxSalesGateway
    {
        AxCustomerResult EnsureCustomer(AxCustomer customer, AxCallContext context);

        /// <summary>Creates the predefined variant (configuration id) of a product master if it does not exist yet.</summary>
        void EnsureConfiguration(string itemId, string configId, IList<string> optionItemIds, AxCallContext context);

        AxDocumentResult CreateQuotation(AxSalesDocument quotation, AxCallContext context);
        AxDocumentResult ConvertQuotation(string quotationId, string customerRef, AxCallContext context);
        AxDocumentResult CreateSalesOrder(AxSalesDocument order, AxCallContext context);
        AxDocumentResult UpdateSalesOrder(string salesId, AxSalesDocument changes, AxCallContext context);
        void CancelSalesOrder(string salesId, string reason, AxCallContext context);
        IList<AxReservationResult> Reserve(string salesId, AxCallContext context);
        void ReleaseReservation(string salesId, AxCallContext context);
    }

    /// <summary>Reads single documents from AX through AIF document services.</summary>
    public interface IAxDocumentReader
    {
        AxSalesOrderSnapshot ReadSalesOrder(string salesId, AxCallContext context);
        AxCustomer ReadCustomer(string accountNum, AxCallContext context);
        IList<AxStockRecord> FindOnHand(string itemId, string inventLocationId, AxCallContext context);
    }

    /// <summary>Paged bulk reads from AX through the Query Service.</summary>
    public interface IAxQueryReader
    {
        AxPage<AxPriceRecord> ReadSalesPrices(int startPosition, int pageSize, AxCallContext context);
        AxPage<AxStockRecord> ReadAvailability(int startPosition, int pageSize, AxCallContext context);
        AxPage<AxCustomerBalance> ReadCustomerBalances(int startPosition, int pageSize, AxCallContext context);
        AxPage<AxOrderHeaderStatus> ReadChangedOrders(DateTime changedSinceUtc, int startPosition, int pageSize, AxCallContext context);
        AxPage<AxExchangeRateRecord> ReadExchangeRates(int startPosition, int pageSize, AxCallContext context);
        AxPage<AxItemRecord> ReadItems(int startPosition, int pageSize, AxCallContext context);
    }

    /// <summary>AOS not reachable, session limit reached, or the call timed out. The command can be retried.</summary>
    [Serializable]
    public class AxUnavailableException : Exception
    {
        public AxUnavailableException(string message, Exception inner = null) : base(message, inner) { }
        protected AxUnavailableException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    /// <summary>AX refused the request (X++ error, validation); retrying will not help until the data is fixed.</summary>
    [Serializable]
    public class AxBusinessException : Exception
    {
        public AxBusinessException(string message, IEnumerable<string> infolog = null, Exception inner = null) : base(message, inner)
        {
            Infolog = new List<string>(infolog ?? new string[0]);
        }

        protected AxBusinessException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
            Infolog = new List<string>();
        }

        public List<string> Infolog { get; }

        public override string Message => Infolog.Count == 0 ? base.Message : base.Message + " " + string.Join(" | ", Infolog);
    }
}
