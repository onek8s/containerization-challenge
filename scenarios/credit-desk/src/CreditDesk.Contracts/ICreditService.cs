using System.ServiceModel;
using CreditDesk.Core;

namespace CreditDesk.Contracts
{
    [ServiceContract(Namespace = Namespaces.Service, Name = "CreditService")]
    public interface ICreditService
    {
        [OperationContract]
        UserInfo WhoAmI();

        [OperationContract]
        CustomerAccount[] SearchCustomers(string text);

        [OperationContract]
        [FaultContract(typeof(CreditFault))]
        CustomerAccount GetCustomer(string accountNumber);

        [OperationContract]
        [FaultContract(typeof(CreditFault))]
        CreditRequest SubmitRequest(string accountNumber, decimal requestedLimit, string reason);

        [OperationContract]
        [FaultContract(typeof(CreditFault))]
        CreditRequest GetRequest(int requestId);

        [OperationContract]
        CreditRequest[] GetMyRequests();

        [OperationContract]
        CreditRequest[] GetPendingApprovals();

        [OperationContract]
        CreditRequest[] GetRequestsForCustomer(string accountNumber);

        [OperationContract]
        [FaultContract(typeof(CreditFault))]
        CreditRequest Decide(int requestId, bool approve, string comment);

        [OperationContract]
        [FaultContract(typeof(CreditFault))]
        CreditRequest Withdraw(int requestId);
    }
}
