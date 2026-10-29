using System;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Activation;
using Tailspin.CustomerCare.Data;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Data.Services;
using Tailspin.CustomerCare.Domain;

namespace Tailspin.CustomerCare.Integration
{
    [ServiceBehavior(Namespace = Namespaces.Service, InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class CaseIntegrationService : ICaseIntegrationService
    {
        private readonly CaseRepository _cases = new CaseRepository();
        private readonly CustomerRepository _customers = new CustomerRepository();

        static CaseIntegrationService()
        {
            CaseService.CaseChanged += (s, e) => PortalNotifier.Notify(e.Case.Id, e.Change);
        }

        private static string Caller
        {
            get
            {
                var ctx = ServiceSecurityContext.Current;
                return ctx != null && ctx.WindowsIdentity != null ? ctx.WindowsIdentity.Name : "integration";
            }
        }

        public CreateCaseResponse CreateCase(CreateCaseRequest request)
        {
            if (request == null) throw Fault("InvalidRequest", "Request is missing.");
            if (string.IsNullOrWhiteSpace(request.Subject)) throw Fault("InvalidRequest", "Subject is required.");
            if (ReferenceData.Categories.All(x => !string.Equals(x.Code, request.CategoryCode, StringComparison.OrdinalIgnoreCase)))
            {
                throw Fault("UnknownCategory", "Category '" + request.CategoryCode + "' does not exist.");
            }

            var customer = _customers.GetByAccount((request.AccountNumber ?? string.Empty).Trim());
            if (customer == null) throw Fault("UnknownAccount", "Account " + request.AccountNumber + " does not exist.");

            if (request.ReuseOpenCase && !string.IsNullOrEmpty(request.ExternalReference))
            {
                var existing = FindByReference(request.ExternalReference).FirstOrDefault(x => x.IsOpen && x.CustomerId == customer.Id);
                if (existing != null)
                {
                    return new CreateCaseResponse
                    {
                        CaseNumber = existing.CaseNumber,
                        QueueCode = existing.Queue.Code,
                        ResponseDueUtc = existing.ResponseDueUtc,
                        ResolveDueUtc = existing.ResolveDueUtc,
                        Existing = true
                    };
                }
            }

            var priority = request.Priority.HasValue && Enum.IsDefined(typeof(CasePriority), request.Priority.Value)
                ? (CasePriority)request.Priority.Value
                : CasePriority.Normal;

            Case c;
            try
            {
                c = new CaseService().Create(new NewCaseRequest
                {
                    CustomerId = customer.Id,
                    CategoryCode = request.CategoryCode.ToUpperInvariant(),
                    Subject = request.Subject.Trim(),
                    Description = request.Description,
                    Priority = priority,
                    Channel = Caller.IndexOf("partner", StringComparison.OrdinalIgnoreCase) >= 0 ? CaseChannel.Partner : CaseChannel.Billing,
                    ExternalReference = request.ExternalReference
                }, Caller);
            }
            catch (ArgumentException ex)
            {
                throw Fault("InvalidRequest", ex.Message);
            }

            Trace.TraceInformation("{0} created case {1} for account {2} (reference {3}).", Caller, c.CaseNumber, customer.AccountNumber, request.ExternalReference);
            return new CreateCaseResponse
            {
                CaseNumber = c.CaseNumber,
                QueueCode = c.Queue.Code,
                ResponseDueUtc = c.ResponseDueUtc,
                ResolveDueUtc = c.ResolveDueUtc
            };
        }

        public CaseInfo GetCase(string caseNumber)
        {
            int year, seq;
            if (!CaseNumber.TryParse(caseNumber, out year, out seq)) throw Fault("InvalidCaseNumber", "Case number must look like CC-2026-000123.");

            var c = _cases.GetByNumber(caseNumber.Trim().ToUpperInvariant());
            if (c == null) throw Fault("NotFound", "Case " + caseNumber + " does not exist.");
            return ToInfo(c);
        }

        public CaseInfo[] FindByExternalReference(string externalReference)
        {
            if (string.IsNullOrWhiteSpace(externalReference)) return new CaseInfo[0];
            return FindByReference(externalReference.Trim()).Select(ToInfo).ToArray();
        }

        public CaseInfo[] GetOpenCasesForAccount(string accountNumber)
        {
            var customer = _customers.GetByAccount((accountNumber ?? string.Empty).Trim());
            if (customer == null) throw Fault("UnknownAccount", "Account " + accountNumber + " does not exist.");

            return _cases.ListForCustomer(customer.Id).Where(c => c.IsOpen).Select(c =>
            {
                c.Customer = customer;
                return ToInfo(c);
            }).ToArray();
        }

        public void AddCustomerMessage(string caseNumber, string text, string source)
        {
            if (string.IsNullOrWhiteSpace(text)) throw Fault("InvalidRequest", "Text is required.");

            var c = _cases.GetByNumber((caseNumber ?? string.Empty).Trim().ToUpperInvariant());
            if (c == null) throw Fault("NotFound", "Case " + caseNumber + " does not exist.");
            if (c.Status == CaseStatus.Closed) throw Fault("CaseClosed", "Case " + caseNumber + " is closed.");

            var service = new CaseService();
            service.AddNote(c.Id, "Message from " + (string.IsNullOrWhiteSpace(source) ? "customer" : source.Trim()) + ":\n" + text.Trim(), true, Caller);

            // A customer reply moves a waiting case back to the agent.
            if (c.Status == CaseStatus.WaitingOnCustomer || c.Status == CaseStatus.Resolved)
            {
                var fresh = _cases.Get(c.Id);
                try
                {
                    service.ChangeStatus(c.Id, fresh.RowVersion, CaseStatus.InProgress, Caller);
                }
                catch (ConcurrencyConflictException)
                {
                    // Agent is working on it right now; the note is enough.
                }
            }
        }

        private static System.Collections.Generic.List<Case> FindByReference(string reference)
        {
            using (var db = new CareDbContext())
            {
                return db.Cases.Include(c => c.Customer).Include(c => c.Queue).AsNoTracking()
                    .Where(c => c.ExternalReference == reference)
                    .OrderByDescending(c => c.CreatedUtc)
                    .Take(50)
                    .ToList();
            }
        }

        private static CaseInfo ToInfo(Case c)
        {
            var queue = c.Queue ?? ReferenceData.QueueById(c.QueueId);
            return new CaseInfo
            {
                CaseNumber = c.CaseNumber,
                AccountNumber = c.Customer != null ? c.Customer.AccountNumber : null,
                Subject = c.Subject,
                Status = c.Status.ToString(),
                QueueCode = queue != null ? queue.Code : null,
                Priority = (int)c.Priority,
                ExternalReference = c.ExternalReference,
                CreatedUtc = c.CreatedUtc,
                ResolveDueUtc = c.ResolveDueUtc,
                ResolvedUtc = c.ResolvedUtc,
                SlaState = ReferenceData.SlaPolicy.Evaluate(c, DateTime.UtcNow).ToString()
            };
        }

        private static FaultException<IntegrationFault> Fault(string code, string message)
        {
            return new FaultException<IntegrationFault>(new IntegrationFault { Code = code, Message = message }, new FaultReason(message), new FaultCode(code));
        }
    }
}
