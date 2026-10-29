using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Integration.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Fabrikam.Housing.Integration.Simulated
{
    /// <summary>
    /// Stands in for Paystream in development. State is kept in payments.json in the simulator folder.
    /// Hosted pages are skipped: the redirect and card URLs point straight back to the caller's return URL.
    /// Payments are confirmed once their charge date has passed; amounts whose pence are divisible by 97 fail.
    /// </summary>
    public class SimulatedPaymentsGateway : IPaymentsGateway
    {
        private static readonly object Gate = new object();
        private readonly string _file;
        private readonly ILogger<SimulatedPaymentsGateway> _logger;

        public SimulatedPaymentsGateway(IOptions<IntegrationOptions> options, ILogger<SimulatedPaymentsGateway> logger)
        {
            _file = Path.Combine(SimulatorFolder.Ensure(options.Value.SimulatorFolder, "payments"), "payments.json");
            _logger = logger;
        }

        public class State
        {
            public int Sequence { get; set; } = 1000;
            public Dictionary<string, Flow> Flows { get; set; } = new Dictionary<string, Flow>();
            public Dictionary<string, Mandate> Mandates { get; set; } = new Dictionary<string, Mandate>();
            public Dictionary<string, Payment> Payments { get; set; } = new Dictionary<string, Payment>();
            public Dictionary<string, CardSession> CardSessions { get; set; } = new Dictionary<string, CardSession>();
            public List<PaymentEvent> Events { get; set; } = new List<PaymentEvent>();
        }

        public class Flow { public string Id; public string TenancyReference; public string Email; public string SessionToken; public string MandateId; }
        public class Mandate { public string Id; public string CustomerId; public string TenancyReference; public string Status; }
        public class Payment { public string Id; public string MandateId; public decimal Amount; public DateTime ChargeDate; public string Reference; public string Status; public string IdempotencyKey; }
        public class CardSession { public string Id; public string TenancyReference; public decimal Amount; public string Status; public string PaymentId; public DateTime? CompletedAt; }

        public Task<RedirectFlow> StartMandateAsync(string tenancyReference, string description, string email, string successUrl, string sessionToken)
        {
            return Update(s =>
            {
                var id = "RE" + Next(s);
                s.Flows[id] = new Flow { Id = id, TenancyReference = tenancyReference, Email = email, SessionToken = sessionToken };
                return new RedirectFlow { FlowId = id, RedirectUrl = SimulatorFolder.Append(successUrl, "redirect_flow_id", id) };
            });
        }

        public Task<CompletedMandate> CompleteMandateAsync(string flowId, string sessionToken)
        {
            return Update(s =>
            {
                if (!s.Flows.TryGetValue(flowId, out var flow) || flow.SessionToken != sessionToken)
                    throw new IntegrationException("Paystream", 422, "Redirect flow not found or session token does not match.");
                if (flow.MandateId == null)
                {
                    var mandate = new Mandate { Id = "MD" + Next(s), CustomerId = "CU" + Next(s), TenancyReference = flow.TenancyReference, Status = "active" };
                    s.Mandates[mandate.Id] = mandate;
                    flow.MandateId = mandate.Id;
                    AddEvent(s, "mandates", "created", mandate.Id, "mandate_created");
                    AddEvent(s, "mandates", "active", mandate.Id, "mandate_activated");
                }
                var m = s.Mandates[flow.MandateId];
                return new CompletedMandate { CustomerId = m.CustomerId, MandateId = m.Id, AccountHolderName = "SIMULATED ACCOUNT", AccountNumberEnding = (Math.Abs(m.Id.GetHashCode()) % 100).ToString("00"), BankName = "SIMULATED BANK PLC" };
            });
        }

        public Task CancelMandateAsync(string mandateId)
        {
            return Update(s =>
            {
                if (s.Mandates.TryGetValue(mandateId, out var m) && m.Status != "cancelled")
                {
                    m.Status = "cancelled";
                    AddEvent(s, "mandates", "cancelled", mandateId, "mandate_cancelled");
                }
                return true;
            });
        }

        public Task<string> CreatePaymentAsync(string mandateId, decimal amount, DateTime chargeDate, string reference, string idempotencyKey)
        {
            return Update(s =>
            {
                var existing = s.Payments.Values.FirstOrDefault(p => p.IdempotencyKey == idempotencyKey && idempotencyKey != null);
                if (existing != null) return existing.Id;
                if (!s.Mandates.TryGetValue(mandateId, out var m) && mandateId != null && mandateId.StartsWith("MD", StringComparison.Ordinal))
                {
                    // Mandates from the development data set were never set up through the simulator.
                    m = new Mandate { Id = mandateId, Status = "active" };
                    s.Mandates[mandateId] = m;
                }
                if (m == null || m.Status != "active")
                    throw new IntegrationException("Paystream", 422, "Mandate " + mandateId + " is not active.");
                var p = new Payment { Id = "PM" + Next(s), MandateId = mandateId, Amount = amount, ChargeDate = chargeDate.Date, Reference = reference, Status = "pending_submission", IdempotencyKey = idempotencyKey };
                s.Payments[p.Id] = p;
                AddEvent(s, "payments", "created", p.Id, "payment_created");
                _logger.LogInformation("Simulated payment {Payment} of {Amount} on {Date}", p.Id, amount, chargeDate);
                return p.Id;
            });
        }

        public Task<CardPaymentSession> StartCardPaymentAsync(string tenancyReference, decimal amount, string returnUrl, string email)
        {
            return Update(s =>
            {
                var id = "CS" + Next(s);
                s.CardSessions[id] = new CardSession { Id = id, TenancyReference = tenancyReference, Amount = amount, Status = "pending" };
                return new CardPaymentSession { SessionId = id, PaymentUrl = SimulatorFolder.Append(returnUrl, "session_id", id) };
            });
        }

        public Task<CardPaymentResult> GetCardPaymentAsync(string sessionId)
        {
            return Update(s =>
            {
                if (!s.CardSessions.TryGetValue(sessionId, out var c)) return null;
                if (c.Status == "pending")
                {
                    // Returning from the (skipped) hosted page counts as paying.
                    c.Status = "succeeded";
                    c.PaymentId = "CP" + Next(s);
                    c.CompletedAt = DateTime.Now;
                    AddEvent(s, "card_payments", "succeeded", c.PaymentId, "card_payment_succeeded");
                }
                return new CardPaymentResult { SessionId = c.Id, PaymentId = c.PaymentId, Status = c.Status, Amount = c.Amount, Reference = c.TenancyReference, CompletedAt = c.CompletedAt };
            });
        }

        public Task<IList<PaymentEvent>> ListEventsAsync(DateTime since)
        {
            return Update<IList<PaymentEvent>>(s =>
            {
                foreach (var p in s.Payments.Values.Where(p => p.Status == "pending_submission" && p.ChargeDate <= DateTime.Today))
                {
                    if (((int)Math.Round(p.Amount * 100m)) % 97 == 0)
                    {
                        p.Status = "failed";
                        AddEvent(s, "payments", "failed", p.Id, "insufficient_funds");
                    }
                    else
                    {
                        p.Status = "confirmed";
                        AddEvent(s, "payments", "confirmed", p.Id, "payment_confirmed");
                    }
                }
                return s.Events.Where(e => e.CreatedAt > since).OrderBy(e => e.CreatedAt).ToList();
            });
        }

        private static string Next(State s) => (++s.Sequence).ToString("0000000");

        private static void AddEvent(State s, string resourceType, string action, string resourceId, string cause)
        {
            s.Events.Add(new PaymentEvent { Id = "EV" + Next(s), CreatedAt = DateTime.Now, ResourceType = resourceType, Action = action, ResourceId = resourceId, Cause = cause, Description = "Simulated " + resourceType + " " + action });
        }

        private Task<T> Update<T>(Func<State, T> change)
        {
            lock (Gate)
            {
                var state = File.Exists(_file) ? JsonConvert.DeserializeObject<State>(File.ReadAllText(_file)) ?? new State() : new State();
                var result = change(state);
                var tmp = _file + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(state, Formatting.Indented));
                if (File.Exists(_file)) File.Delete(_file);
                File.Move(tmp, _file);
                return Task.FromResult(result);
            }
        }
    }
}
