using System;

namespace Contoso.Purchasing.Core
{
    public class ApprovalRecord
    {
        public int Id { get; set; }
        public ApprovalStep Step { get; set; }
        public string ApproverAccount { get; set; }
        public string ApproverName { get; set; }
        public ApprovalDecision Decision { get; set; }
        public string Comment { get; set; }
        public DateTime DecidedAt { get; set; }
    }
}
