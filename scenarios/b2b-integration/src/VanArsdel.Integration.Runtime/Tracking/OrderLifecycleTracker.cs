using System;
using Microsoft.BizTalk.Bam.EventObservation;
using VanArsdel.Integration.Domain.Orders;
using VanArsdel.Integration.Runtime.Configuration;

namespace VanArsdel.Integration.Runtime.Tracking
{
    /// <summary>
    /// BAM activity OrderLifecycle (bam\OrderLifecycle.xml), written from the orchestrations with the
    /// OrchestrationEventStream so the events commit with the orchestration's persistence points. The activity id
    /// is "&lt;PartnerId&gt;|&lt;OrderNumber&gt;"; the receive pipeline starts it and enables a continuation on the
    /// interchange id, ERP and WMS feedback continue it on the ERP order number.
    /// </summary>
    [Serializable]
    public static class OrderLifecycleTracker
    {
        public const string ActivityName = "OrderLifecycle";

        public static string ActivityId(string partnerId, string orderNumber)
        {
            return partnerId + "|" + orderNumber;
        }

        public static void Received(string partnerId, string orderNumber, string standard, string interchangeId, DateTime receivedUtc, int lineCount)
        {
            if (!Enabled)
            {
                return;
            }

            var id = ActivityId(partnerId, orderNumber);
            OrchestrationEventStream.BeginActivity(ActivityName, id);
            OrchestrationEventStream.UpdateActivity(ActivityName, id, new object[]
            {
                "PartnerId", partnerId,
                "OrderNumber", orderNumber,
                "Standard", standard,
                "InterchangeId", interchangeId,
                "ReceivedUtc", receivedUtc,
                "LineCount", lineCount,
                "Status", OrderMilestone.Received.ToString()
            });
            OrchestrationEventStream.AddReference(ActivityName, id, "Interchange", "InterchangeId", interchangeId);
        }

        public static void Milestone(string partnerId, string orderNumber, OrderMilestone milestone, DateTime atUtc)
        {
            if (!Enabled)
            {
                return;
            }

            var id = ActivityId(partnerId, orderNumber);
            OrchestrationEventStream.UpdateActivity(ActivityName, id, new object[] { MilestoneColumn(milestone), atUtc, "Status", milestone.ToString() });
        }

        public static void SentToErp(string partnerId, string orderNumber, decimal orderValue, string currency, DateTime atUtc)
        {
            if (!Enabled)
            {
                return;
            }

            var id = ActivityId(partnerId, orderNumber);
            OrchestrationEventStream.UpdateActivity(ActivityName, id, new object[]
            {
                "SentToErpUtc", atUtc, "OrderValue", orderValue, "Currency", currency, "Status", OrderMilestone.SentToErp.ToString()
            });
        }

        /// <summary>The ERP confirmation carries only the ERP order number: continue the activity on it.</summary>
        public static void ConfirmedByErp(string partnerId, string orderNumber, string erpOrderNumber, DateTime atUtc)
        {
            if (!Enabled)
            {
                return;
            }

            var id = ActivityId(partnerId, orderNumber);
            OrchestrationEventStream.UpdateActivity(ActivityName, id, new object[]
            {
                "ErpOrderNumber", erpOrderNumber, "ConfirmedUtc", atUtc, "Status", OrderMilestone.ConfirmedByErp.ToString()
            });
            OrchestrationEventStream.EnableContinuation(ActivityName, id, ContinuationToken(erpOrderNumber));
        }

        public static void Rejected(string partnerId, string orderNumber, string reason, DateTime atUtc)
        {
            if (!Enabled)
            {
                return;
            }

            var id = ActivityId(partnerId, orderNumber);
            OrchestrationEventStream.UpdateActivity(ActivityName, id, new object[]
            {
                "RejectedUtc", atUtc, "RejectReason", Truncate(reason, 512), "Status", OrderMilestone.Rejected.ToString()
            });
            OrchestrationEventStream.EndActivity(ActivityName, id);
        }

        public static void CreditHold(string partnerId, string orderNumber, string reason, DateTime atUtc)
        {
            if (!Enabled)
            {
                return;
            }

            var id = ActivityId(partnerId, orderNumber);
            OrchestrationEventStream.UpdateActivity(ActivityName, id, new object[]
            {
                "CreditHoldUtc", atUtc, "HoldReason", Truncate(reason, 256), "Status", OrderMilestone.CreditHold.ToString()
            });
        }

        /// <summary>Despatch and invoice orchestrations only know the ERP order number.</summary>
        public static void DespatchReceived(string erpOrderNumber, string despatchNumber, DateTime atUtc)
        {
            if (!Enabled)
            {
                return;
            }

            OrchestrationEventStream.UpdateActivity(ActivityName, ContinuationToken(erpOrderNumber), new object[]
            {
                "DespatchNumber", despatchNumber, "DespatchedUtc", atUtc
            });
        }

        public static void DespatchSent(string erpOrderNumber, string despatchNumber, DateTime atUtc)
        {
            if (!Enabled)
            {
                return;
            }

            var token = ContinuationToken(erpOrderNumber);
            OrchestrationEventStream.UpdateActivity(ActivityName, token, new object[]
            {
                "DespatchNumber", despatchNumber, "AsnSentUtc", atUtc, "Status", OrderMilestone.Despatched.ToString()
            });
        }

        public static void InvoiceSent(string erpOrderNumber, string invoiceNumber, decimal amount, DateTime atUtc)
        {
            if (!Enabled)
            {
                return;
            }

            var token = ContinuationToken(erpOrderNumber);
            OrchestrationEventStream.UpdateActivity(ActivityName, token, new object[]
            {
                "InvoiceNumber", invoiceNumber, "InvoiceAmount", amount, "InvoicedUtc", atUtc, "Status", OrderMilestone.Invoiced.ToString()
            });
            OrchestrationEventStream.EndActivity(ActivityName, token);
        }

        public static string ContinuationToken(string erpOrderNumber)
        {
            return "ERP|" + erpOrderNumber;
        }

        private static string MilestoneColumn(OrderMilestone milestone)
        {
            switch (milestone)
            {
                case OrderMilestone.ConfirmedByErp:
                    return "ConfirmedUtc";
                default:
                    return milestone + "Utc";
            }
        }

        private static string Truncate(string value, int max)
        {
            return string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max);
        }

        private static bool Enabled
        {
            get { return SsoSettings.GetBool(SettingNames.BamEnabled, true); }
        }
    }
}
