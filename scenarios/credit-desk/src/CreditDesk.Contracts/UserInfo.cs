using System.Runtime.Serialization;
using CreditDesk.Core;

namespace CreditDesk.Contracts
{
    [DataContract(Namespace = Namespaces.Data)]
    public class UserInfo
    {
        [DataMember] public string AccountName { get; set; }
        [DataMember] public string DisplayName { get; set; }
        [DataMember] public ApproverRoles Roles { get; set; }
        [DataMember] public string AuthenticationType { get; set; }
        [DataMember] public string ImpersonationLevel { get; set; }
        [DataMember] public string ServerName { get; set; }
    }
}
