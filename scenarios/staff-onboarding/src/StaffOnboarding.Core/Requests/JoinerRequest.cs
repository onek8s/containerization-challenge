using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace StaffOnboarding.Core.Requests
{
    [DataContract(Namespace = OnboardingNamespaces.Data)]
    public class JoinerRequest
    {
        [DataMember(Order = 1)] public string RequestNumber { get; set; }
        [DataMember(Order = 2)] public string RequestedBy { get; set; }
        [DataMember(Order = 3)] public PersonDetails Person { get; set; }
        [DataMember(Order = 4)] public PositionDetails Position { get; set; }
        [DataMember(Order = 5)] public DateTime StartDate { get; set; }

        /// <summary>Groups requested in addition to the role defaults.</summary>
        [DataMember(Order = 6)] public List<string> AdditionalGroups { get; set; }

        [DataMember(Order = 7)] public string Notes { get; set; }

        public JoinerRequest()
        {
            AdditionalGroups = new List<string>();
        }
    }
}
