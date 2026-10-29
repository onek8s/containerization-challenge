using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Tailspin.CustomerCare.Archive
{
    [DataContract(Name = "case", Namespace = "")]
    public class ArchivedCase
    {
        [DataMember(Name = "caseNumber", Order = 1)] public string CaseNumber { get; set; }
        [DataMember(Name = "accountNumber", Order = 2)] public string AccountNumber { get; set; }
        [DataMember(Name = "customerName", Order = 3)] public string CustomerName { get; set; }
        [DataMember(Name = "queue", Order = 4)] public string Queue { get; set; }
        [DataMember(Name = "category", Order = 5)] public string Category { get; set; }
        [DataMember(Name = "subject", Order = 6)] public string Subject { get; set; }
        [DataMember(Name = "description", Order = 7)] public string Description { get; set; }
        [DataMember(Name = "priority", Order = 8)] public string Priority { get; set; }
        [DataMember(Name = "channel", Order = 9)] public string Channel { get; set; }
        [DataMember(Name = "externalReference", Order = 10)] public string ExternalReference { get; set; }
        [DataMember(Name = "createdUtc", Order = 11)] public DateTime CreatedUtc { get; set; }
        [DataMember(Name = "createdBy", Order = 12)] public string CreatedBy { get; set; }
        [DataMember(Name = "resolvedUtc", Order = 13)] public DateTime? ResolvedUtc { get; set; }
        [DataMember(Name = "closedUtc", Order = 14)] public DateTime? ClosedUtc { get; set; }
        [DataMember(Name = "assignedTo", Order = 15)] public string AssignedTo { get; set; }
        [DataMember(Name = "notes", Order = 16)] public List<ArchivedNote> Notes { get; set; }
        [DataMember(Name = "attachments", Order = 17)] public List<string> Attachments { get; set; }
    }

    [DataContract(Name = "note", Namespace = "")]
    public class ArchivedNote
    {
        [DataMember(Name = "author", Order = 1)] public string Author { get; set; }
        [DataMember(Name = "createdUtc", Order = 2)] public DateTime CreatedUtc { get; set; }
        [DataMember(Name = "internal", Order = 3)] public bool IsInternal { get; set; }
        [DataMember(Name = "body", Order = 4)] public string Body { get; set; }
    }
}
