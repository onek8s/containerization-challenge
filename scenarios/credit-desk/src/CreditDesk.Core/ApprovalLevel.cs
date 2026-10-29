using System.Runtime.Serialization;

namespace CreditDesk.Core
{
    /// <summary>Who has to sign off a credit limit change.</summary>
    [DataContract(Namespace = Namespaces.Data)]
    public enum ApprovalLevel
    {
        /// <summary>Decreases and small increases, applied without review.</summary>
        [EnumMember] Automatic = 0,
        [EnumMember] CreditController = 1,
        [EnumMember] FinanceManager = 2,
        [EnumMember] CreditCommittee = 3
    }
}
