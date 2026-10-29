using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using log4net;
using Tailspin.DealerOrders.Contracts.Ax;

namespace Tailspin.DealerOrders.AxIntegration.BusinessConnector
{
    /// <summary>
    /// Writes to AX through the .NET Business Connector. All document changes go through the TSDealerOrderService
    /// X++ class (see ax\Classes) which runs them in one ttsBegin/ttsCommit and validates with the standard AxSalesTable /
    /// AxSalesLine classes, so business logic and number sequences stay in AX.
    /// </summary>
    public class BusinessConnectorSalesGateway : IAxSalesGateway
    {
        private const string ServiceClass = "TSDealerOrderService";
        private static readonly ILog Log = LogManager.GetLogger(typeof(BusinessConnectorSalesGateway));

        private readonly BusinessConnectorPool _pool;

        public BusinessConnectorSalesGateway(BusinessConnectorPool pool)
        {
            _pool = pool;
        }

        public AxCustomerResult EnsureCustomer(AxCustomer customer, AxCallContext context) => _pool.Use(context.RunAsUser, session =>
        {
            var result = AxXml.Result(session.CallStatic(ServiceClass, "ensureCustomer", AxXml.Customer(customer), context.MessageId), "ensureCustomer " + customer.AccountNum);
            return new AxCustomerResult
            {
                AccountNum = (string)result.Attribute("id"),
                Created = (string)result.Attribute("created") == "1",
                LocationIdsByExternalCode = result.Elements("address").ToDictionary(a => (string)a.Attribute("externalCode"), a => (string)a.Attribute("locationId")),
                Infolog = result.Elements("info").Select(i => (string)i).ToList()
            };
        });

        public void EnsureConfiguration(string itemId, string configId, IList<string> optionItemIds, AxCallContext context) => _pool.Use(context.RunAsUser, session =>
        {
            // Fast path: the predefined variant usually exists already (repeat configurations).
            var dim = session.Select("InventDimCombination",
                $"select firstonly RecId from %1 where %1.ItemId == '{Escape(itemId)}' && %1.DataAreaId == curext() " +
                $"exists join InventDim where InventDim.inventDimId == %1.InventDimId && InventDim.configId == '{Escape(configId)}'");
            if ((bool)dim.Found) return true;

            AxXml.Result(session.CallStatic(ServiceClass, "ensureConfiguration", AxXml.Configuration(itemId, configId, optionItemIds)), "ensureConfiguration " + configId);
            Log.InfoFormat("Configuration {0} of {1} created in AX", configId, itemId);
            return true;
        });

        public AxDocumentResult CreateQuotation(AxSalesDocument quotation, AxCallContext context) => _pool.Use(context.RunAsUser, session =>
            AxXml.DocumentResult(AxXml.Result(session.CallStatic(ServiceClass, "createQuotation", AxXml.SalesDocument("quotation", quotation), context.MessageId),
                "createQuotation " + quotation.PortalReference)));

        public AxDocumentResult ConvertQuotation(string quotationId, string customerRef, AxCallContext context) => _pool.Use(context.RunAsUser, session =>
            AxXml.DocumentResult(AxXml.Result(session.CallStatic(ServiceClass, "convertQuotation", quotationId, customerRef ?? "", context.MessageId),
                "convertQuotation " + quotationId)));

        public AxDocumentResult CreateSalesOrder(AxSalesDocument order, AxCallContext context) => _pool.Use(context.RunAsUser, session =>
        {
            CheckCustomer(session, order.CustAccount);
            return AxXml.DocumentResult(AxXml.Result(session.CallStatic(ServiceClass, "createSalesOrder", AxXml.SalesDocument("salesOrder", order), context.MessageId),
                "createSalesOrder " + order.PortalReference));
        });

        public AxDocumentResult UpdateSalesOrder(string salesId, AxSalesDocument changes, AxCallContext context) => _pool.Use(context.RunAsUser, session =>
            AxXml.DocumentResult(AxXml.Result(session.CallStatic(ServiceClass, "updateSalesOrder", salesId, AxXml.SalesDocument("salesOrder", changes), context.MessageId),
                "updateSalesOrder " + salesId)));

        public void CancelSalesOrder(string salesId, string reason, AxCallContext context) => _pool.Use(context.RunAsUser, session =>
            AxXml.Result(session.CallStatic(ServiceClass, "cancelSalesOrder", salesId, reason ?? ""), "cancelSalesOrder " + salesId));

        public IList<AxReservationResult> Reserve(string salesId, AxCallContext context) => _pool.Use(context.RunAsUser, session =>
        {
            var result = AxXml.Result(session.CallStatic(ServiceClass, "reserveSalesOrder", salesId), "reserveSalesOrder " + salesId);
            return (IList<AxReservationResult>)result.Elements("line").Select(l => new AxReservationResult
            {
                InventTransId = (string)l.Attribute("inventTransId"),
                ItemId = (string)l.Attribute("itemId"),
                Requested = decimal.Parse((string)l.Attribute("qty"), CultureInfo.InvariantCulture),
                Reserved = decimal.Parse((string)l.Attribute("reserved"), CultureInfo.InvariantCulture),
                ExpectedDate = string.IsNullOrEmpty((string)l.Attribute("expected")) ? (DateTime?)null
                    : DateTime.ParseExact((string)l.Attribute("expected"), "yyyy-MM-dd", CultureInfo.InvariantCulture)
            }).ToList();
        });

        public void ReleaseReservation(string salesId, AxCallContext context) => _pool.Use(context.RunAsUser, session =>
            AxXml.Result(session.CallStatic(ServiceClass, "releaseReservation", salesId), "releaseReservation " + salesId));

        /// <summary>Refuses orders for customers on hold before AX creates a half-saved order.</summary>
        private static void CheckCustomer(BusinessConnectorSession session, string account)
        {
            var cust = session.CallStaticRecord("CustTable", "find", account);
            if (!(bool)cust.Found)
                throw new AxBusinessException("Customer check failed.", new[] { $"Customer account {account} does not exist." });
            // CustVendorBlocked: 0 = No, 1 = Invoice, 2 = All.
            var blocked = Convert.ToInt32(cust.get_Field("Blocked"), CultureInfo.InvariantCulture);
            if (blocked == 2)
                throw new AxBusinessException("Customer check failed.", new[] { $"Customer {account} is on hold: All." });
        }

        private static string Escape(string value) => (value ?? "").Replace("\\", "\\\\").Replace("'", "\\'");
    }
}
