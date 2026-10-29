using System.Runtime.Serialization;

namespace StaffOnboarding.Core.Requests
{
    [DataContract(Namespace = OnboardingNamespaces.Data)]
    public enum RequestKind
    {
        [EnumMember] Joiner = 1,
        [EnumMember] Mover = 2,
        [EnumMember] Leaver = 3
    }

    [DataContract(Namespace = OnboardingNamespaces.Data)]
    public enum RequestStatus
    {
        [EnumMember] Submitted = 0,
        [EnumMember] AwaitingManager = 1,
        [EnumMember] AwaitingIT = 2,
        [EnumMember] Scheduled = 3,
        [EnumMember] Provisioning = 4,
        [EnumMember] WaitingForStartDate = 5,
        [EnumMember] Completed = 6,
        [EnumMember] Rejected = 7,
        [EnumMember] Cancelled = 8,
        [EnumMember] Failed = 9,
        [EnumMember] Disabled = 10,
        [EnumMember] AwaitingCleanup = 11
    }

    [DataContract(Namespace = OnboardingNamespaces.Data)]
    public enum EmploymentType
    {
        [EnumMember] Permanent = 0,
        [EnumMember] FixedTerm = 1,
        [EnumMember] Contractor = 2,
        [EnumMember] Trainee = 3
    }

    [DataContract(Namespace = OnboardingNamespaces.Data)]
    public enum TaskKind
    {
        [EnumMember] ManagerApproval = 1,
        [EnumMember] ITApproval = 2,
        [EnumMember] HomeFolderHandover = 3
    }

    [DataContract(Namespace = OnboardingNamespaces.Data)]
    public enum DecisionOutcome
    {
        [EnumMember] Approved = 1,
        [EnumMember] Rejected = 2
    }
}
