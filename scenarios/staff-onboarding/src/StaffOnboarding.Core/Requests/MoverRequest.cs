using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace StaffOnboarding.Core.Requests
{
    [DataContract(Namespace = OnboardingNamespaces.Data)]
    public class MoverRequest
    {
        [DataMember(Order = 1)] public string RequestNumber { get; set; }
        [DataMember(Order = 2)] public string RequestedBy { get; set; }
        [DataMember(Order = 3)] public string SamAccountName { get; set; }
        [DataMember(Order = 4)] public PositionDetails NewPosition { get; set; }
        [DataMember(Order = 5)] public DateTime EffectiveDate { get; set; }

        /// <summary>Days the old role groups are kept after the effective date for handover.</summary>
        [DataMember(Order = 6)] public int HandoverDays { get; set; }

        [DataMember(Order = 7)] public List<string> AdditionalGroups { get; set; }
        [DataMember(Order = 8)] public string Notes { get; set; }

        public MoverRequest()
        {
            AdditionalGroups = new List<string>();
        }
    }
}
