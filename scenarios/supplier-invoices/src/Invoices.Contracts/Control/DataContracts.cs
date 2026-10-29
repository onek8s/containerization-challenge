using System;
using System.Runtime.Serialization;

namespace Wingtip.Invoices.Contracts.Control
{
    [DataContract(Namespace = ControlNamespace.Value)]
    public class QueueStatus
    {
        [DataMember(Order = 1)]
        public string Name { get; set; }

        [DataMember(Order = 2)]
        public string Path { get; set; }

        [DataMember(Order = 3)]
        public int Messages { get; set; }

        [DataMember(Order = 4)]
        public DateTime? OldestMessage { get; set; }
    }

    [DataContract(Namespace = ControlNamespace.Value)]
    public class MatchSummary
    {
        [DataMember(Order = 1)]
        public long InvoiceId { get; set; }

        [DataMember(Order = 2)]
        public bool Matched { get; set; }

        [DataMember(Order = 3)]
        public string Summary { get; set; }

        [DataMember(Order = 4)]
        public string[] Issues { get; set; }
    }

    [DataContract(Namespace = ControlNamespace.Value)]
    public class ControlFault
    {
        [DataMember(Order = 1)]
        public string Code { get; set; }

        [DataMember(Order = 2)]
        public string Message { get; set; }
    }
}
