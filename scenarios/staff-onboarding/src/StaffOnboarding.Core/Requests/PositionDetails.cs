using System;
using System.Runtime.Serialization;

namespace StaffOnboarding.Core.Requests
{
    [DataContract(Namespace = OnboardingNamespaces.Data)]
    public class PositionDetails
    {
        /// <summary>Department code from the HR system, for example FIN or LOG.</summary>
        [DataMember(Order = 1)] public string Department { get; set; }

        /// <summary>Job role code from the HR system, for example FIN-AP or LOG-PLANNER.</summary>
        [DataMember(Order = 2)] public string JobRole { get; set; }

        [DataMember(Order = 3)] public string Title { get; set; }
        [DataMember(Order = 4)] public string Office { get; set; }
        [DataMember(Order = 5)] public string CostCenter { get; set; }

        /// <summary>sAMAccountName of the line manager.</summary>
        [DataMember(Order = 6)] public string ManagerAccount { get; set; }

        [DataMember(Order = 7)] public EmploymentType EmploymentType { get; set; }

        /// <summary>Last day of the contract for fixed term, contractor and trainee positions.</summary>
        [DataMember(Order = 8)] public DateTime? ContractEndDate { get; set; }
    }
}
