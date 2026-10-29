using System.ServiceModel;

namespace Southridge.Clinic.Contracts
{
    /// <summary>
    /// Internal endpoint on the services host that forwards referral decisions taken in the staff site to the
    /// running referral workflow. Callers send the X-Clinic-Key HTTP header.
    /// </summary>
    [ServiceContract(Namespace = Namespaces.Records, Name = "ReferralWorkflowService")]
    public interface IReferralWorkflowService
    {
        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        void Start(int referralId);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        void Decided(int referralId, bool approved, string user, string note);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        void Scheduled(int referralId, int appointmentId);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        void Cancelled(int referralId, string user, string note);
    }
}
