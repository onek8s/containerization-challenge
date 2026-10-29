using System;
using System.Collections.Generic;
using VanArsdel.Integration.Domain.Orders;

namespace VanArsdel.Integration.Monitoring
{
    /// <summary>Row of rpt.vOrderLifecycle (BAM OrderLifecycle activity, active and completed).</summary>
    public class OrderActivityRow
    {
        public string ActivityId { get; set; }
        public string PartnerId { get; set; }
        public string OrderNumber { get; set; }
        public string ErpOrderNumber { get; set; }
        public decimal OrderValue { get; set; }
        public int LineCount { get; set; }
        public DateTime? ReceivedUtc { get; set; }
        public DateTime? ValidatedUtc { get; set; }
        public DateTime? CreditHoldUtc { get; set; }
        public DateTime? ReleasedUtc { get; set; }
        public DateTime? SentToErpUtc { get; set; }
        public DateTime? ConfirmedUtc { get; set; }
        public DateTime? DespatchedUtc { get; set; }
        public DateTime? AsnSentUtc { get; set; }
        public DateTime? InvoicedUtc { get; set; }
        public DateTime? RejectedUtc { get; set; }
        public string RejectReason { get; set; }
        public string HoldReason { get; set; }
        public string DespatchNumber { get; set; }
        public string InvoiceNumber { get; set; }

        public string Status
        {
            get
            {
                if (RejectedUtc.HasValue)
                {
                    return "Rejected";
                }

                if (InvoicedUtc.HasValue)
                {
                    return "Invoiced";
                }

                if (DespatchedUtc.HasValue)
                {
                    return "Despatched";
                }

                if (ConfirmedUtc.HasValue)
                {
                    return "Confirmed";
                }

                if (SentToErpUtc.HasValue)
                {
                    return "In ERP";
                }

                if (CreditHoldUtc.HasValue && !ReleasedUtc.HasValue)
                {
                    return "Credit hold";
                }

                return ReceivedUtc.HasValue ? "Received" : "Unknown";
            }
        }
    }

    public class SuspendedInstanceGroup
    {
        public string ServiceName { get; set; }
        public string HostName { get; set; }
        public int Count { get; set; }
        public DateTime OldestSuspendedUtc { get; set; }
        public string ErrorSample { get; set; }
        public bool Resumable { get; set; }
    }

    public enum HostInstanceState
    {
        Unknown = 0,
        Stopped = 1,
        StartPending = 2,
        StopPending = 3,
        Running = 4,
        ContinuePending = 5,
        PausePending = 6,
        Paused = 7,
        NotApplicable = 8
    }

    /// <summary>MSBTS_HostInstance (WMI) row.</summary>
    public class HostInstanceStatus
    {
        public string HostName { get; set; }
        public string ServerName { get; set; }
        public HostInstanceState State { get; set; }
        public bool Disabled { get; set; }

        public string Name
        {
            get { return HostName + "@" + ServerName; }
        }
    }

    public class PartnerActivity
    {
        public string PartnerId { get; set; }
        public DateTime? LastReceivedUtc { get; set; }

        /// <summary>Partners that send at least once per working day (from dbo.TradingPartner.ExpectsDailyTraffic).</summary>
        public bool ExpectsDailyTraffic { get; set; }
    }

    /// <summary>Everything the SLA monitor reads in one polling cycle.</summary>
    public class MonitoringSnapshot
    {
        public DateTime TakenUtc { get; set; }
        public List<InterchangeRecord> OutboundAwaitingAck { get; } = new List<InterchangeRecord>();
        public List<OrderActivityRow> OpenOrders { get; } = new List<OrderActivityRow>();
        public List<SuspendedInstanceGroup> Suspended { get; } = new List<SuspendedInstanceGroup>();
        public List<HostInstanceStatus> HostInstances { get; } = new List<HostInstanceStatus>();
        public List<PartnerActivity> Partners { get; } = new List<PartnerActivity>();
    }
}
