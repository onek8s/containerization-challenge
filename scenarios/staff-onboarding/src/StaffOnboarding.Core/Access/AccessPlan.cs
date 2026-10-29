using System.Collections.Generic;
using System.Runtime.Serialization;

namespace StaffOnboarding.Core.Access
{
    [DataContract(Namespace = OnboardingNamespaces.Data)]
    public class AccessPlan
    {
        public AccessPlan()
        {
            AddGroups = new List<string>();
            RemoveGroups = new List<string>();
            DeferredRemoveGroups = new List<string>();
            Warnings = new List<string>();
        }

        [DataMember(Order = 1)] public string TargetOu { get; set; }
        [DataMember(Order = 2)] public List<string> AddGroups { get; set; }
        [DataMember(Order = 3)] public List<string> RemoveGroups { get; set; }

        /// <summary>Groups removed after the handover period (movers).</summary>
        [DataMember(Order = 4)] public List<string> DeferredRemoveGroups { get; set; }

        [DataMember(Order = 5)] public bool RequiresITApproval { get; set; }
        [DataMember(Order = 6)] public List<string> Warnings { get; set; }
        [DataMember(Order = 7)] public string DepartmentShare { get; set; }
    }
}
