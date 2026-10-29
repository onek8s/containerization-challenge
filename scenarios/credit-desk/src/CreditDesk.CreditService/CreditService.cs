using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Transactions;
using CreditDesk.Contracts;
using CreditDesk.Core;

namespace CreditDesk.CreditService
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple,
        Namespace = Namespaces.Service)]
    public sealed class CreditService : ICreditService
    {
        private const int MaxSearchResults = 50;
        private const ApproverRoles AnyUser = ApproverRoles.SalesRep | ApproverRoles.CreditController
            | ApproverRoles.FinanceManager | ApproverRoles.CreditCommittee;
        private const ApproverRoles Approvers = ApproverRoles.CreditController | ApproverRoles.FinanceManager
            | ApproverRoles.CreditCommittee;

        private readonly CreditRepository _repository = new CreditRepository(CreditServiceSettings.ConnectionString);
        private readonly ApprovalPolicy _policy = CreditServiceSettings.Policy;

        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        public UserInfo WhoAmI()
        {
            var caller = Caller.Current;
            caller.LookupDirectory(out var displayName, out _);
            return new UserInfo
            {
                AccountName = caller.AccountName,
                DisplayName = displayName,
                Roles = caller.Roles,
                AuthenticationType = caller.Identity.AuthenticationType,
                ImpersonationLevel = caller.Identity.ImpersonationLevel.ToString(),
                ServerName = Environment.MachineName
            };
        }

        [OperationBehavior(Impersonation = ImpersonationOption.Required)]
        public CustomerAccount[] SearchCustomers(string text)
        {
            Caller.Current.Require(AnyUser);
            return Run(() => _repository.SearchCustomers(text, MaxSearchResults).ToArray());
        }

        [OperationBehavior(Impersonation = ImpersonationOption.Required)]
        public CustomerAccount GetCustomer(string accountNumber)
        {
            Caller.Current.Require(AnyUser);
            var account = Run(() => _repository.GetCustomer(accountNumber));
            if (account == null) throw Fault("Customer " + accountNumber + " was not found.");
            return account;
        }

        [OperationBehavior(Impersonation = ImpersonationOption.Required)]
        public CreditRequest SubmitRequest(string accountNumber, decimal requestedLimit, string reason)
        {
            var caller = Caller.Current;
            caller.Require(AnyUser);

            var account = Run(() => _repository.GetCustomer(accountNumber));
            var errors = _policy.Validate(account, requestedLimit, reason, DateTime.Today);
            if (errors.Count > 0)
                throw Fault("The request cannot be submitted.", errors.ToArray());

            var open = Run(() => _repository.GetForCustomer(accountNumber))
                .FirstOrDefault(r => r.Status == RequestStatus.Submitted || r.Status == RequestStatus.Escalated);
            if (open != null)
                throw Fault("Request " + open.RequestId + " for this customer is still waiting for a decision.");

            var level = _policy.RequiredLevel(account.CreditLimit, requestedLimit);
            var request = Run(() => _repository.Insert(account, requestedLimit, reason, level));
            Audit.Write(string.Format("{0} requested credit limit {1:N2} -> {2:N2} for {3} (request {4}, level {5}).",
                caller.AccountName, account.CreditLimit, requestedLimit, account.AccountNumber, request.RequestId, level),
                EventLogEntryType.Information, 2000);

            if (level == ApprovalLevel.Automatic)
                request = Complete(caller, request, account, true, "Applied automatically under policy CP-04.");
            return request;
        }

        [OperationBehavior(Impersonation = ImpersonationOption.Required)]
        public CreditRequest GetRequest(int requestId)
        {
            var caller = Caller.Current;
            caller.Require(AnyUser);
            var request = Run(() => _repository.GetRequest(requestId));
            if (request == null) throw Fault("Request " + requestId + " was not found.");
            if (!caller.IsApprover && !string.Equals(request.RequestedBy, caller.AccountName, StringComparison.OrdinalIgnoreCase))
                throw Fault("You can only view your own requests.");
            return request;
        }

        [OperationBehavior(Impersonation = ImpersonationOption.Required)]
        public CreditRequest[] GetMyRequests()
        {
            Caller.Current.Require(AnyUser);
            return Run(() => _repository.GetRequestsByRequester().ToArray());
        }

        [OperationBehavior(Impersonation = ImpersonationOption.Required)]
        public CreditRequest[] GetPendingApprovals()
        {
            var caller = Caller.Current;
            if (!caller.IsApprover) return new CreditRequest[0];
            return Run(() => _repository.GetPending(ApprovalLevel.CreditCommittee)
                .Where(r => ApprovalPolicy.CanDecide(r.RequiredLevel, caller.Roles))
                .ToArray());
        }

        [OperationBehavior(Impersonation = ImpersonationOption.Required)]
        public CreditRequest[] GetRequestsForCustomer(string accountNumber)
        {
            Caller.Current.Require(AnyUser);
            return Run(() => _repository.GetForCustomer(accountNumber).ToArray());
        }

        [OperationBehavior(Impersonation = ImpersonationOption.Required)]
        public CreditRequest Decide(int requestId, bool approve, string comment)
        {
            var caller = Caller.Current;
            caller.Require(Approvers);

            var request = Run(() => _repository.GetRequest(requestId));
            if (request == null) throw Fault("Request " + requestId + " was not found.");
            if (request.Status != RequestStatus.Submitted && request.Status != RequestStatus.Escalated)
                throw Fault("Request " + requestId + " has already been decided.");
            if (string.Equals(request.RequestedBy, caller.AccountName, StringComparison.OrdinalIgnoreCase))
                throw Fault("You cannot decide a request you submitted yourself.");
            if (!ApprovalPolicy.CanDecide(request.RequiredLevel, caller.Roles))
                throw Fault("This request needs approval at level " + request.RequiredLevel + ".");
            if (!approve && string.IsNullOrWhiteSpace(comment))
                throw Fault("Give a reason for the rejection. It is printed on the customer letter.");

            var account = Run(() => _repository.GetCustomer(request.AccountNumber));
            return Complete(caller, request, account, approve, comment);
        }

        [OperationBehavior(Impersonation = ImpersonationOption.Required)]
        public CreditRequest Withdraw(int requestId)
        {
            var caller = Caller.Current;
            var request = GetRequest(requestId);
            if (request.Status != RequestStatus.Submitted && request.Status != RequestStatus.Escalated)
                throw Fault("Only open requests can be withdrawn.");
            if (!string.Equals(request.RequestedBy, caller.AccountName, StringComparison.OrdinalIgnoreCase))
                throw Fault("Only the requester can withdraw a request.");
            return Run(() => _repository.Decide(requestId, RequestStatus.Withdrawn, "Withdrawn by requester", null));
        }

        /// <summary>Stores the decision and queues the customer letter in one distributed transaction.</summary>
        private CreditRequest Complete(Caller caller, CreditRequest request, CustomerAccount account, bool approve, string comment)
        {
            var now = DateTime.Now;
            DateTime? validUntil = approve ? _policy.ValidUntil(now) : (DateTime?)null;
            caller.LookupDirectory(out var signedBy, out var title);

            var letter = new LetterMessage
            {
                RequestId = request.RequestId,
                AccountNumber = account.AccountNumber,
                CustomerName = account.Name,
                ContactName = account.ContactName,
                AddressLine1 = account.AddressLine1,
                AddressLine2 = account.AddressLine2,
                PostalCode = account.PostalCode,
                City = account.City,
                CountryCode = account.CountryCode,
                PreviousLimit = request.CurrentLimit,
                NewLimit = request.RequestedLimit,
                Approved = approve,
                DecisionComment = comment,
                DecidedAt = now,
                ValidUntil = validUntil,
                SignedBy = signedBy,
                SignedByTitle = string.IsNullOrEmpty(title) ? "Credit Control" : title,
                Language = string.Equals(account.CountryCode, "GB", StringComparison.OrdinalIgnoreCase) ? "en-gb" : "en"
            };

            var options = new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromSeconds(60) };
            CreditRequest result;
            using (var scope = new TransactionScope(TransactionScopeOption.Required, options))
            {
                result = Run(() => _repository.Decide(request.RequestId, approve ? RequestStatus.Approved : RequestStatus.Rejected, comment, validUntil));
                new LetterQueue(CreditServiceSettings.LetterQueue).Send(letter);
                scope.Complete();
            }

            Audit.Write(string.Format("{0} {1} request {2} for {3} ({4:N2} -> {5:N2}).", caller.AccountName,
                approve ? "approved" : "rejected", request.RequestId, account.AccountNumber, request.CurrentLimit, request.RequestedLimit),
                EventLogEntryType.SuccessAudit, approve ? 3000 : 3001);
            return result;
        }

        private static T Run<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (SqlException ex) when (ex.Number == 50001)
            {
                // Business rule raised by a stored procedure.
                throw Fault(ex.Message);
            }
            catch (SqlException ex)
            {
                Audit.Write("Database call failed: " + ex, EventLogEntryType.Error, 5000);
                throw Fault("The credit database is not available. Try again later or contact the service desk.");
            }
        }

        private static FaultException<CreditFault> Fault(string message, params string[] errors) =>
            new FaultException<CreditFault>(new CreditFault(message, errors), message);
    }
}
