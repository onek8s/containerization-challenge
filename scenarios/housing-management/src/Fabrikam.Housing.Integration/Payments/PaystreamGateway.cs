using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Authenticators;

namespace Fabrikam.Housing.Integration.Payments
{
    /// <summary>
    /// Paystream v2 API: direct debit mandates through hosted redirect flows, payments against mandates,
    /// hosted card payment sessions and the events feed.
    /// </summary>
    public class PaystreamGateway : IPaymentsGateway
    {
        private readonly IRestClient _client;
        private readonly PaymentsOptions _options;
        private readonly ILogger<PaystreamGateway> _logger;

        public PaystreamGateway(RestClientFactory factory, IOptions<IntegrationOptions> options, ILogger<PaystreamGateway> logger)
        {
            _options = options.Value.Payments;
            _logger = logger;
            _client = factory.Create(_options.BaseUrl);
            _client.Authenticator = new JwtAuthenticator(_options.AccessToken);
            _client.AddDefaultHeader("Paystream-Version", "2020-10-01");
        }

        public async Task<RedirectFlow> StartMandateAsync(string tenancyReference, string description, string email, string successUrl, string sessionToken)
        {
            var body = new
            {
                redirect_flows = new
                {
                    description,
                    session_token = sessionToken,
                    success_redirect_url = successUrl,
                    prefilled_customer = new { email },
                    links = new { creditor = _options.CreditorId },
                    metadata = new { tenancy_ref = tenancyReference }
                }
            };
            var json = await SendAsync(Method.POST, "redirect_flows", body, "flow-" + sessionToken);
            var flow = json["redirect_flows"];
            return new RedirectFlow { FlowId = (string)flow["id"], RedirectUrl = (string)flow["redirect_url"] };
        }

        public async Task<CompletedMandate> CompleteMandateAsync(string flowId, string sessionToken)
        {
            var json = await SendAsync(Method.POST, $"redirect_flows/{flowId}/actions/complete", new { data = new { session_token = sessionToken } }, "complete-" + flowId);
            var links = json["redirect_flows"]["links"];
            var mandateId = (string)links["mandate"];
            var bank = await SendAsync(Method.GET, $"customer_bank_accounts/{(string)links["customer_bank_account"]}", null, null);
            var account = bank["customer_bank_accounts"];
            return new CompletedMandate
            {
                CustomerId = (string)links["customer"],
                MandateId = mandateId,
                AccountHolderName = (string)account["account_holder_name"],
                AccountNumberEnding = (string)account["account_number_ending"],
                BankName = (string)account["bank_name"]
            };
        }

        public Task CancelMandateAsync(string mandateId)
        {
            return SendAsync(Method.POST, $"mandates/{mandateId}/actions/cancel", new { data = new { } }, "cancel-" + mandateId);
        }

        public async Task<string> CreatePaymentAsync(string mandateId, decimal amount, DateTime chargeDate, string reference, string idempotencyKey)
        {
            var body = new
            {
                payments = new
                {
                    amount = (int)Math.Round(amount * 100m),
                    currency = "GBP",
                    charge_date = chargeDate.ToString("yyyy-MM-dd"),
                    reference,
                    links = new { mandate = mandateId }
                }
            };
            var json = await SendAsync(Method.POST, "payments", body, idempotencyKey);
            return (string)json["payments"]["id"];
        }

        public async Task<CardPaymentSession> StartCardPaymentAsync(string tenancyReference, decimal amount, string returnUrl, string email)
        {
            var body = new
            {
                card_sessions = new
                {
                    amount = (int)Math.Round(amount * 100m),
                    currency = "GBP",
                    description = "Rent payment " + tenancyReference,
                    reference = tenancyReference,
                    return_url = returnUrl,
                    email
                }
            };
            var json = await SendAsync(Method.POST, "card_sessions", body, Guid.NewGuid().ToString("N"));
            var s = json["card_sessions"];
            return new CardPaymentSession { SessionId = (string)s["id"], PaymentUrl = (string)s["payment_url"] };
        }

        public async Task<CardPaymentResult> GetCardPaymentAsync(string sessionId)
        {
            var json = await SendAsync(Method.GET, $"card_sessions/{sessionId}", null, null);
            var s = json["card_sessions"];
            return new CardPaymentResult
            {
                SessionId = sessionId,
                PaymentId = (string)s["payment_id"],
                Status = (string)s["status"],
                Amount = (decimal)(int)s["amount"] / 100m,
                Reference = (string)s["reference"],
                CompletedAt = (DateTime?)s["completed_at"]
            };
        }

        public async Task<IList<PaymentEvent>> ListEventsAsync(DateTime since)
        {
            var events = new List<PaymentEvent>();
            string after = null;
            do
            {
                var resource = "events?limit=500&created_at[gt]=" + Uri.EscapeDataString(since.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                if (after != null) resource += "&after=" + after;
                var json = await SendAsync(Method.GET, resource, null, null);
                events.AddRange(PaystreamWebhookParser.ParseArray((JArray)json["events"]));
                after = (string)json["meta"]?["cursors"]?["after"];
            } while (!string.IsNullOrEmpty(after));
            return events.OrderBy(e => e.CreatedAt).ToList();
        }

        private async Task<JObject> SendAsync(Method method, string resource, object body, string idempotencyKey)
        {
            var request = new RestRequest(resource, method);
            if (body != null) request.AddJsonBody(body);
            if (idempotencyKey != null) request.AddHeader("Idempotency-Key", idempotencyKey);

            var response = await RestClientFactory.RetryPolicy(_logger, "Paystream").ExecuteAsync(() => _client.ExecuteAsync(request));
            RestClientFactory.EnsureSuccess(response, "Paystream");
            return JObject.Parse(response.Content);
        }
    }
}
