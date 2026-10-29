using System;
using System.Runtime.Serialization;
using StaffOnboarding.Core;

namespace StaffOnboarding.Workflows
{
    /// <summary>
    /// Approver routing and timings captured when an instance starts, so a request keeps the rules it was
    /// approved under even if the host configuration changes later.
    /// </summary>
    [DataContract(Namespace = OnboardingNamespaces.Data)]
    public class ProcessSettings
    {
        [DataMember] public string ITApprovers { get; set; }
        [DataMember] public string ITEscalation { get; set; }
        [DataMember] public string HRTeam { get; set; }
        [DataMember] public string ServiceDesk { get; set; }
        [DataMember] public string PortalUrl { get; set; }
        [DataMember] public TimeSpan ReminderInterval { get; set; }
        [DataMember] public int RemindersBeforeEscalation { get; set; }
        [DataMember] public int EscalationsBeforeExpiry { get; set; }
        [DataMember] public TimeSpan RetryDelay { get; set; }
        [DataMember] public int MaxAttempts { get; set; }

        public string TaskUrl(Guid taskId)
        {
            return PortalUrl + "/tasks/" + taskId.ToString("D");
        }

        public string RequestUrl(string requestNumber)
        {
            return PortalUrl + "/requests/" + requestNumber;
        }
    }
}
