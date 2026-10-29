using System.Runtime.Serialization;

namespace Southridge.Clinic.Contracts
{
    [DataContract(Namespace = Namespaces.Faults)]
    public class ClinicFault
    {
        [DataMember(Order = 0)]
        public string Code { get; set; }

        [DataMember(Order = 1)]
        public string Message { get; set; }

        [DataMember(Order = 2)]
        public string ReferenceId { get; set; }
    }

    public static class FaultCodes
    {
        public const string Validation = "VALIDATION";
        public const string NotFound = "NOT_FOUND";
        public const string Conflict = "CONFLICT";
        public const string Concurrency = "CONCURRENCY";
        public const string Unavailable = "UNAVAILABLE";
        public const string Internal = "INTERNAL";
    }
}
