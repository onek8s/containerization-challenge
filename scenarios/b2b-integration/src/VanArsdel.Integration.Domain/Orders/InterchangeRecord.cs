using System;

namespace VanArsdel.Integration.Domain.Orders
{
    public enum InterchangeDirection
    {
        Inbound = 1,
        Outbound = 2
    }

    public enum AckStatus
    {
        NotRequired = 0,
        Pending = 1,
        Accepted = 2,
        AcceptedWithErrors = 3,
        Rejected = 4,
        Overdue = 5
    }

    /// <summary>One EDI interchange in or out; outbound ones wait for a 997/CONTRL (dbo.Interchange).</summary>
    public class InterchangeRecord
    {
        public long InterchangeKey { get; set; }
        public string PartnerId { get; set; }
        public InterchangeDirection Direction { get; set; }
        public string ControlNumber { get; set; }
        public string GroupControlNumber { get; set; }
        public string TransactionType { get; set; }
        public int TransactionCount { get; set; }
        public DateTime CreatedUtc { get; set; }
        public AckStatus AckStatus { get; set; }
        public DateTime? AckReceivedUtc { get; set; }
        public DateTime? AckDueUtc { get; set; }
        public string FileName { get; set; }
        public string BodyHash { get; set; }

        public bool IsAckOverdue(DateTime nowUtc)
        {
            return AckStatus == AckStatus.Pending && AckDueUtc.HasValue && nowUtc > AckDueUtc.Value;
        }

        public void ApplyAck(AckStatus status, DateTime receivedUtc)
        {
            if (status == AckStatus.Pending || status == AckStatus.NotRequired || status == AckStatus.Overdue)
            {
                throw new ArgumentException("Not an acknowledgement result: " + status, "status");
            }

            AckStatus = status;
            AckReceivedUtc = receivedUtc;
        }
    }
}
