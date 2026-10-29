using System;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Domain.Orders;
using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.Runtime.Configuration;

namespace VanArsdel.Integration.Runtime.Interchanges
{
    /// <summary>
    /// Outbound interchange register (dbo.Interchange) used by the send orchestrations and the ack correlation:
    /// every document we send to a partner that expects a 997 / CONTRL gets an ack due time; the SLA monitor
    /// raises AckOverdue when it passes.
    /// </summary>
    [Serializable]
    public static class InterchangeRegistry
    {
        public static long RegisterOutbound(string partnerId, DocumentKind kind, string controlNumber, string groupControlNumber, int transactionCount, string fileName, DateTime sentUtc)
        {
            var partner = Partners.PartnerCache.Get(partnerId);
            var record = new InterchangeRecord
            {
                PartnerId = partnerId,
                Direction = InterchangeDirection.Outbound,
                TransactionType = DocumentKinds.ToTransactionType(kind, partner.Standard),
                ControlNumber = controlNumber,
                GroupControlNumber = groupControlNumber,
                TransactionCount = transactionCount,
                CreatedUtc = sentUtc,
                FileName = fileName,
                AckStatus = partner.AckRequired ? AckStatus.Pending : AckStatus.NotRequired,
                AckDueUtc = partner.AckRequired ? sentUtc.AddMinutes(partner.AckTimeoutMinutes) : (DateTime?)null
            };

            return Repository().RegisterOutbound(record);
        }

        public static bool ApplyAck(string partnerId, string acknowledgedControlNumber, string status, DateTime receivedUtc)
        {
            AckStatus parsed;
            if (!Enum.TryParse(status, out parsed))
            {
                throw new ArgumentException("Unknown acknowledgement status " + status + ".", "status");
            }

            return Repository().ApplyAck(partnerId, acknowledgedControlNumber, parsed, receivedUtc);
        }

        public static string NextControlNumber(string partnerId, string level, int width)
        {
            return Repository().Next(partnerId, level).ToString("D" + width, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static InterchangeRepository Repository()
        {
            return new InterchangeRepository(IntegrationDatabases.FromSso());
        }
    }
}
