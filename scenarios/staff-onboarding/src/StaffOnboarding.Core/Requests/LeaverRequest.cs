using System;
using System.Runtime.Serialization;

namespace StaffOnboarding.Core.Requests
{
    [DataContract(Namespace = OnboardingNamespaces.Data)]
    public class LeaverRequest
    {
        [DataMember(Order = 1)] public string RequestNumber { get; set; }
        [DataMember(Order = 2)] public string RequestedBy { get; set; }
        [DataMember(Order = 3)] public string SamAccountName { get; set; }
        [DataMember(Order = 4)] public DateTime LastWorkingDay { get; set; }

        /// <summary>Disable the account as soon as the request is approved.</summary>
        [DataMember(Order = 5)] public bool Immediate { get; set; }

        /// <summary>Account that receives the home folder contents, normally the manager.</summary>
        [DataMember(Order = 6)] public string HandoverTo { get; set; }

        [DataMember(Order = 7)] public string Notes { get; set; }
    }
}
