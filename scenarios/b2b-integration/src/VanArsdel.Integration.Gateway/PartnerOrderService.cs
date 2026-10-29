using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using log4net;
using VanArsdel.Integration.Contracts.Gateway;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Gateway.Behaviors;
using VanArsdel.Integration.Gateway.Forwarding;
using VanArsdel.Integration.Monitoring;

namespace VanArsdel.Integration.Gateway
{
    /// <summary>
    /// Facade in front of the BizTalk-published receive location (B2BReceive/PartnerOrders.svc). It authenticates the
    /// partner, checks the batch, answers status queries from the BAM views and forwards accepted batches to BizTalk,
    /// so partners get a synchronous receipt or fault while the orchestrations stay one-way.
    /// </summary>
    [ServiceBehavior(Namespace = GatewayNamespaces.Service, InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    [GatewayErrorHandling]
    public class PartnerOrderService : IPartnerOrderService
    {
        public const int MaxStatusQueries = 100;
        private static readonly ILog Log = LogManager.GetLogger(typeof(PartnerOrderService));
        private readonly GatewayServices _services;

        public PartnerOrderService() : this(GatewayServices.Default)
        {
        }

        public PartnerOrderService(GatewayServices services)
        {
            _services = services;
        }

        /// <summary>Partner id of the caller (the WS-Security user name, checked by PartnerCredentialValidator).</summary>
        protected virtual string CallerPartnerId
        {
            get
            {
                var context = ServiceSecurityContext.Current;
                if (context == null || context.IsAnonymous)
                {
                    throw Fault("NotAuthenticated", "The request has no partner credentials.");
                }

                return context.PrimaryIdentity.Name.ToUpperInvariant();
            }
        }

        public SubmitReceipt SubmitOrders(OrderBatch batch)
        {
            var partnerId = CallerPartnerId;
            var now = _services.Clock();
            var result = _services.Checker.Check(batch, partnerId);
            if (result.Errors.Count > 0)
            {
                Log.InfoFormat("Batch {0} from {1} rejected: {2}", batch == null ? "?" : batch.BatchId, partnerId, string.Join("; ", result.Errors));
                throw Fault("InvalidBatch", string.Join("; ", result.Errors));
            }

            if (!_services.Duplicates.TryRegister(partnerId, "GW|" + batch.BatchId, now, TimeSpan.FromDays(30)))
            {
                throw Fault("DuplicateBatch", "Batch " + batch.BatchId + " was already received. Use a new BatchId for new orders.");
            }

            var receiptId = string.Format(CultureInfo.InvariantCulture, "GW{0:yyyyMMddHHmmss}{1}", now, partnerId);
            _services.Submitter.Submit(batch, receiptId);
            Log.InfoFormat("Batch {0} from {1} accepted with {2} order(s), receipt {3}", batch.BatchId, partnerId, batch.Orders.Length, receiptId);

            return new SubmitReceipt
            {
                BatchId = batch.BatchId,
                ReceiptId = receiptId,
                AcceptedOrders = batch.Orders.Length,
                ReceivedUtc = now,
                Warnings = result.Warnings.Count == 0 ? null : result.Warnings.ToArray()
            };
        }

        public PartnerOrderStatus[] GetOrderStatus(string[] poNumbers)
        {
            var partnerId = CallerPartnerId;
            if (poNumbers == null || poNumbers.Length == 0)
            {
                return new PartnerOrderStatus[0];
            }

            if (poNumbers.Length > MaxStatusQueries)
            {
                throw Fault("TooManyOrders", "Ask for at most " + MaxStatusQueries + " orders per call.");
            }

            var list = new List<PartnerOrderStatus>();
            foreach (var po in poNumbers.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                list.Add(ToStatus(po, _services.Tracking.FindOrder(partnerId, po)));
            }

            return list.ToArray();
        }

        internal static PartnerOrderStatus ToStatus(string poNumber, OrderActivityRow row)
        {
            if (row == null)
            {
                return new PartnerOrderStatus { PoNumber = poNumber, Status = "Unknown" };
            }

            return new PartnerOrderStatus
            {
                PoNumber = row.OrderNumber,
                Status = PartnerStatus(row),
                ErpOrderNumber = row.ErpOrderNumber,
                ReceivedUtc = row.ReceivedUtc,
                DespatchedUtc = row.DespatchedUtc,
                TrackingNumber = row.DespatchNumber,
                InvoiceNumber = row.InvoiceNumber
            };
        }

        /// <summary>Partners see fewer states than customer service: credit holds are shown as "InProgress".</summary>
        internal static string PartnerStatus(OrderActivityRow row)
        {
            switch (row.Status)
            {
                case "Rejected":
                case "Despatched":
                case "Invoiced":
                case "Confirmed":
                    return row.Status;
                default:
                    return "InProgress";
            }
        }

        private static FaultException<GatewayFault> Fault(string code, string message)
        {
            return new FaultException<GatewayFault>(new GatewayFault { Code = code, Message = message }, new FaultReason(message), new FaultCode(code));
        }
    }
}
