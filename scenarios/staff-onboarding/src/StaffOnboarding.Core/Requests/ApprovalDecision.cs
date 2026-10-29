using System;
using System.Runtime.Serialization;

namespace StaffOnboarding.Core.Requests
{
    [DataContract(Namespace = OnboardingNamespaces.Data)]
    public class ApprovalDecision
    {
        [DataMember(Order = 1)] public string RequestNumber { get; set; }
        [DataMember(Order = 2)] public Guid TaskId { get; set; }
        [DataMember(Order = 3)] public DecisionOutcome Outcome { get; set; }
        [DataMember(Order = 4)] public string Comment { get; set; }
        [DataMember(Order = 5)] public string DecidedBy { get; set; }
    }
}
