using System;

namespace VanArsdel.Integration.Domain.Sla
{
    public enum AlertSeverity
    {
        Information = 1,
        Warning = 2,
        Critical = 3
    }

    public enum SlaKind
    {
        /// <summary>No 997/CONTRL for an outbound interchange within the partner's ack window.</summary>
        AckOverdue = 1,

        /// <summary>Customer order received but not handed to the ERP within the threshold.</summary>
        OrderNotInErp = 2,

        /// <summary>Order on credit hold for longer than the threshold.</summary>
        CreditHoldAging = 3,

        /// <summary>Suspended service instances in the MessageBox for a service above a count or age.</summary>
        SuspendedInstances = 4,

        /// <summary>A BizTalk host instance is not running.</summary>
        HostInstanceDown = 5,

        /// <summary>A partner that sends daily has not sent anything within the threshold (silent partner).</summary>
        PartnerSilent = 6,

        /// <summary>Despatch from the WMS not followed by an ASN to the customer.</summary>
        AsnNotSent = 7
    }

    /// <summary>A row of dbo.SlaDefinition. PartnerId null means the rule applies to all partners.</summary>
    public class SlaDefinition
    {
        public string Code { get; set; }
        public SlaKind Kind { get; set; }
        public string Description { get; set; }
        public string PartnerId { get; set; }
        public int ThresholdMinutes { get; set; }
        public int ThresholdCount { get; set; }
        public AlertSeverity Severity { get; set; }
        public bool BusinessHoursOnly { get; set; }
        public string NotifyGroup { get; set; }
        public bool Enabled { get; set; }

        public TimeSpan Threshold
        {
            get { return TimeSpan.FromMinutes(ThresholdMinutes); }
        }

        public bool AppliesTo(string partnerId)
        {
            return string.IsNullOrEmpty(PartnerId) || string.Equals(PartnerId, partnerId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
