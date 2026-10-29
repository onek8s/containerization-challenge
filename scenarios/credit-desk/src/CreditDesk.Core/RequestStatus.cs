using System.Runtime.Serialization;

namespace CreditDesk.Core
{
    [DataContract(Namespace = Namespaces.Data)]
    public enum RequestStatus
    {
        [EnumMember] Submitted = 0,
        [EnumMember] Approved = 1,
        [EnumMember] Rejected = 2,
        [EnumMember] Escalated = 3,
        [EnumMember] Withdrawn = 4
    }
}
