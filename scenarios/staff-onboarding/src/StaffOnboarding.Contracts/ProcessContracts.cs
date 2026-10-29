using System;
using System.ServiceModel;
using StaffOnboarding.Core;
using StaffOnboarding.Core.Requests;

namespace StaffOnboarding.Contracts
{
    // The operations below are implemented by workflow services on the workflow host. Parameter names are
    // part of the message format: the workflows correlate on the request number inside the message body.

    [ServiceContract(Name = ContractNames.Joiner, Namespace = OnboardingNamespaces.Service)]
    public interface IJoinerProcess
    {
        [OperationContract]
        string Submit(JoinerRequest request);

        [OperationContract]
        void Decide(ApprovalDecision decision);

        [OperationContract]
        void ChangeStartDate(string requestNumber, DateTime startDate, string changedBy);

        [OperationContract]
        void Cancel(string requestNumber, string cancelledBy, string reason);
    }

    [ServiceContract(Name = ContractNames.Mover, Namespace = OnboardingNamespaces.Service)]
    public interface IMoverProcess
    {
        [OperationContract]
        string Submit(MoverRequest request);

        [OperationContract]
        void Decide(ApprovalDecision decision);

        [OperationContract]
        void Cancel(string requestNumber, string cancelledBy, string reason);
    }

    [ServiceContract(Name = ContractNames.Leaver, Namespace = OnboardingNamespaces.Service)]
    public interface ILeaverProcess
    {
        [OperationContract]
        string Submit(LeaverRequest request);

        [OperationContract]
        void Decide(ApprovalDecision decision);

        /// <summary>Disables the account now instead of at the end of the last working day.</summary>
        [OperationContract]
        void DisableNow(string requestNumber, string requestedBy, string reason);

        [OperationContract]
        void ChangeLastWorkingDay(string requestNumber, DateTime lastWorkingDay, string changedBy);

        [OperationContract]
        void Cancel(string requestNumber, string cancelledBy, string reason);
    }

    public static class ContractNames
    {
        public const string Joiner = "JoinerProcess";
        public const string Mover = "MoverProcess";
        public const string Leaver = "LeaverProcess";
    }
}
