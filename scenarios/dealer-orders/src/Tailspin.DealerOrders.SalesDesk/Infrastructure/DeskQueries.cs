using System;
using System.Collections.Generic;
using Dapper;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.SalesDesk.Infrastructure
{
    public class EdiMessageRow
    {
        public int Id { get; set; }
        public string PartnerName { get; set; }
        public EdiDirection Direction { get; set; }
        public string TransactionSet { get; set; }
        public string InterchangeControl { get; set; }
        public string Reference { get; set; }
        public EdiMessageState State { get; set; }
        public string MdnStatus { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? SentUtc { get; set; }
        public int Attempts { get; set; }
        public string Error { get; set; }
        public int? OrderId { get; set; }
    }

    public class ApprovalRow
    {
        public int Id { get; set; }
        public int DealerId { get; set; }
        public string DealerAccount { get; set; }
        public string DealerName { get; set; }
        public int? QuoteId { get; set; }
        public string QuoteNumber { get; set; }
        public int? OrderId { get; set; }
        public string OrderNumber { get; set; }
        public ApprovalReason Reason { get; set; }
        public string Details { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public decimal RequestedDiscount { get; set; }
        public DateTime RequestedUtc { get; set; }
        public string RequestedBy { get; set; }
    }

    /// <summary>Lists for the desk pages that the shared stores do not offer (Dapper).</summary>
    public class DeskQueries
    {
        private readonly SqlConnectionFactory _connections;

        public DeskQueries(SqlConnectionFactory connections)
        {
            _connections = connections;
        }

        public IList<ApprovalRow> PendingApprovals()
        {
            using (var c = _connections.Open())
                return c.Query<ApprovalRow>(@"
SELECT a.Id, a.DealerId, d.AccountNumber AS DealerAccount, d.Name AS DealerName, a.QuoteId, q.QuoteNumber, a.OrderId, o.OrderNumber,
       a.Reason, a.Details, a.Amount, a.Currency, a.RequestedDiscount, a.RequestedUtc, a.RequestedBy
FROM dlr.Approval a
JOIN dlr.Dealer d ON d.Id = a.DealerId
LEFT JOIN dlr.Quote q ON q.Id = a.QuoteId
LEFT JOIN dlr.SalesOrder o ON o.Id = a.OrderId
WHERE a.Decision = 0
ORDER BY a.RequestedUtc;").AsList();
        }

        public IList<EdiMessageRow> EdiMessages(EdiMessageState? state, int? partnerId, int max = 200)
        {
            using (var c = _connections.Open())
                return c.Query<EdiMessageRow>(@"
SELECT TOP (@max) m.Id, p.Name AS PartnerName, m.Direction, m.TransactionSet, m.InterchangeControl, m.Reference, m.State, m.MdnStatus,
       m.CreatedUtc, m.SentUtc, m.Attempts, m.Error, m.OrderId
FROM edi.Message m JOIN edi.Partner p ON p.Id = m.PartnerId
WHERE (@state IS NULL OR m.State = @state) AND (@partnerId IS NULL OR m.PartnerId = @partnerId)
ORDER BY m.Id DESC;", new { max, state = (int?)state, partnerId }).AsList();
        }

        public int EdiProblems(DateTime sinceUtc)
        {
            using (var c = _connections.Open())
                return c.ExecuteScalar<int>("SELECT COUNT(*) FROM edi.Message WHERE State IN (2, 6) AND CreatedUtc >= @sinceUtc", new { sinceUtc });
        }

        public IList<AxCommand> AxCommands(AxCommandState? state, int max = 200)
        {
            using (var c = _connections.Open())
                return c.Query<AxCommand>(@"
SELECT TOP (@max) * FROM ax.Command WHERE (@state IS NULL OR State = @state) ORDER BY Id DESC;", new { max, state = (int?)state }).AsList();
        }
    }
}
