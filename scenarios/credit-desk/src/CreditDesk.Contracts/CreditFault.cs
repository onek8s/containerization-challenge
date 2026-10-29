using System.Runtime.Serialization;
using CreditDesk.Core;

namespace CreditDesk.Contracts
{
    [DataContract(Namespace = Namespaces.Data)]
    public class CreditFault
    {
        public CreditFault() { }

        public CreditFault(string message, params string[] errors)
        {
            Message = message;
            Errors = errors;
        }

        [DataMember] public string Message { get; set; }
        [DataMember] public string[] Errors { get; set; }
    }
}
