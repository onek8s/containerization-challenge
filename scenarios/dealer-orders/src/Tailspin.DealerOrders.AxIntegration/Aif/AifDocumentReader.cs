using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using Tailspin.DealerOrders.AxIntegration.QueryService;
using Tailspin.DealerOrders.Contracts.Ax;

namespace Tailspin.DealerOrders.AxIntegration.Aif
{
    /// <summary>
    /// Reads documents through AIF: the sales order (SalesOrderService.read), customers and on-hand. Packing slips and
    /// invoices are not part of the SalesOrder document; they are read from the TSDealerShipments query (Query Service).
    /// </summary>
    public class AifDocumentReader : IAxDocumentReader
    {
        private readonly AxChannels _channels;
        private readonly AxQueryReader _query;

        public AifDocumentReader(AxChannels channels, AxQueryReader query)
        {
            _channels = channels;
            _query = query;
        }

        private static CallContext Context(AxCallContext context) => new CallContext
        {
            Company = context.Company,
            Language = context.Language,
            LogonAsUser = context.RunAsUser,
            MessageId = "{" + context.MessageId + "}"
        };

        private static EntityKey[] Key(string field, string value) =>
            new[] { new EntityKey { KeyData = new[] { new KeyField { Field = field, Value = value } } } };

        public AxSalesOrderSnapshot ReadSalesOrder(string salesId, AxCallContext context)
        {
            var response = _channels.Call<SalesOrderService, SalesOrderServiceReadResponse>(_channels.AifAddress,
                s => s.read(new SalesOrderServiceReadRequest { CallContext = Context(context), EntityKeyList = Key("SalesId", salesId) }),
                "SalesOrderService.read " + salesId);
            var table = response.SalesOrder?.SalesTable?.FirstOrDefault()
                ?? throw new AxBusinessException("SalesOrderService.read returned no order.", new[] { $"Sales order {salesId} not found." });

            var snapshot = new AxSalesOrderSnapshot
            {
                SalesId = table.SalesId,
                CustAccount = table.CustAccount,
                CustomerRef = table.CustomerRef,
                SalesStatus = (AxSalesStatus)(int)table.SalesStatus,
                DocumentStatus = MapDocumentStatus(table.DocumentStatus),
                ConfirmedDeliveryDate = table.ShippingDateConfirmed,
                InProduction = table.TSInProduction == "Yes",
                ModifiedDateTime = table.ModifiedDateTime
            };
            foreach (var line in table.SalesLine ?? new AxdEntity_SalesLine[0])
            {
                var dim = line.InventDim?.FirstOrDefault();
                snapshot.Lines.Add(new AxSalesLineSnapshot
                {
                    InventTransId = line.InventTransId,
                    ItemId = line.ItemId,
                    ConfigId = dim?.configId,
                    SerialId = dim?.inventSerialId,
                    QtyOrdered = line.SalesQty,
                    DeliveredQty = line.SalesQty - line.RemainSalesPhysical,
                    InvoicedQty = line.SalesQty - line.RemainSalesFinancial - line.RemainSalesPhysical,
                    ConfirmedShipDate = line.ShippingDateConfirmed,
                    LineStatus = (AxSalesStatus)(int)line.SalesStatus
                });
            }

            _query.AddShipments(snapshot, context);
            return snapshot;
        }

        private static AxDocumentStatus MapDocumentStatus(AxdEnum_DocumentStatus status)
        {
            switch (status)
            {
                case AxdEnum_DocumentStatus.Quotation: return AxDocumentStatus.Quotation;
                case AxdEnum_DocumentStatus.PurchaseOrder: return AxDocumentStatus.PurchaseOrder;
                case AxdEnum_DocumentStatus.Confirmation: return AxDocumentStatus.Confirmation;
                case AxdEnum_DocumentStatus.PickingList: return AxDocumentStatus.PickingList;
                case AxdEnum_DocumentStatus.PackingSlip: return AxDocumentStatus.PackingSlip;
                case AxdEnum_DocumentStatus.Invoice: return AxDocumentStatus.Invoice;
                default: return AxDocumentStatus.None;
            }
        }

        public AxCustomer ReadCustomer(string accountNum, AxCallContext context)
        {
            var response = _channels.Call<CustomerService, CustomerServiceReadResponse>(_channels.AifAddress,
                s => s.read(new CustomerServiceReadRequest { CallContext = Context(context), EntityKeyList = Key("AccountNum", accountNum) }),
                "CustomerService.read " + accountNum);
            var cust = response.Customer?.CustTable?.FirstOrDefault()
                ?? throw new AxBusinessException("CustomerService.read returned no customer.", new[] { $"Customer {accountNum} not found." });
            return new AxCustomer
            {
                AccountNum = cust.AccountNum,
                Name = cust.DirParty?.FirstOrDefault()?.Name,
                CustGroup = cust.CustGroup,
                Currency = cust.Currency,
                PriceGroup = cust.PriceGroup,
                LineDiscount = cust.LineDisc,
                PaymTermId = cust.PaymTermId,
                DlvTerm = cust.DlvTerm,
                InventLocation = cust.InventLocation,
                CreditMax = cust.CreditMax,
                SalesDistrictId = cust.SalesDistrictId,
                LanguageId = cust.LanguageId,
                Blocked = cust.Blocked == "All" ? AxCustomerBlocked.All : cust.Blocked == "Invoice" ? AxCustomerBlocked.Invoice : AxCustomerBlocked.No
            };
        }

        public IList<AxStockRecord> FindOnHand(string itemId, string inventLocationId, AxCallContext context)
        {
            var criteria = new List<CriteriaElement> { new CriteriaElement { DataSourceName = "InventSum", FieldName = "ItemId", Operator = "Equal", Value1 = itemId } };
            if (!string.IsNullOrEmpty(inventLocationId))
                criteria.Add(new CriteriaElement { DataSourceName = "InventDim", FieldName = "InventLocationId", Operator = "Equal", Value1 = inventLocationId });
            var response = _channels.Call<InventOnhandService, InventOnhandServiceFindResponse>(_channels.AifAddress,
                s => s.find(new InventOnhandServiceFindRequest { CallContext = Context(context), QueryCriteria = new QueryCriteria { CriteriaElement = criteria.ToArray() } }),
                "InventOnhandService.find " + itemId);
            return (response.InventOnhand?.InventSum ?? new AxdEntity_InventSum[0]).Select(s => new AxStockRecord
            {
                ItemId = s.ItemId,
                ConfigId = s.InventDim?.FirstOrDefault()?.configId,
                InventLocationId = s.InventDim?.FirstOrDefault()?.InventLocationId,
                PhysicalInvent = s.PhysicalInvent,
                ReservPhysical = s.ReservPhysical,
                OnOrder = s.OnOrder
            }).ToList();
        }
    }
}
