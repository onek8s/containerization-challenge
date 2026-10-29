using System;
using System.Runtime.Serialization;

namespace CreditDesk.Core
{
    [DataContract(Namespace = Namespaces.Data)]
    public class CreditRequest
    {
        [DataMember] public int RequestId { get; set; }
        [DataMember] public string AccountNumber { get; set; }
        [DataMember] public string CustomerName { get; set; }
        [DataMember] public decimal CurrentLimit { get; set; }
        [DataMember] public decimal RequestedLimit { get; set; }
        [DataMember] public string Reason { get; set; }
        [DataMember] public RequestStatus Status { get; set; }
        [DataMember] public ApprovalLevel RequiredLevel { get; set; }
        [DataMember] public string RequestedBy { get; set; }
        [DataMember] public DateTime RequestedAt { get; set; }
        [DataMember] public string DecidedBy { get; set; }
        [DataMember] public DateTime? DecidedAt { get; set; }
        [DataMember] public string DecisionComment { get; set; }
        [DataMember] public DateTime? ValidUntil { get; set; }
        [DataMember] public string LetterPath { get; set; }

        public decimal Change => RequestedLimit - CurrentLimit;
    }
}
