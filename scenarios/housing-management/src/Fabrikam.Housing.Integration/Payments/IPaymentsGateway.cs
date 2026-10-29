using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Fabrikam.Housing.Integration.Payments
{
    public class RedirectFlow
    {
        public string FlowId { get; set; }
        public string RedirectUrl { get; set; }
    }

    public class CompletedMandate
    {
        public string CustomerId { get; set; }
        public string MandateId { get; set; }
        public string AccountHolderName { get; set; }
        public string AccountNumberEnding { get; set; }
        public string BankName { get; set; }
    }

    public class CardPaymentSession
    {
        public string SessionId { get; set; }
        public string PaymentUrl { get; set; }
    }

    public class CardPaymentResult
    {
        public string SessionId { get; set; }
        public string PaymentId { get; set; }
        /// <summary>pending, succeeded, failed, cancelled</summary>
        public string Status { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// An event from Paystream, delivered by webhook and also listed by the events API.
    /// ResourceType: mandates, payments, card_payments. Action: e.g. active, cancelled, failed, confirmed, paid_out, charged_back.
    /// </summary>
    public class PaymentEvent
    {
        public string Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ResourceType { get; set; }
        public string Action { get; set; }
        public string ResourceId { get; set; }
        public string Cause { get; set; }
        public string Description { get; set; }
    }

    public interface IPaymentsGateway
    {
        Task<RedirectFlow> StartMandateAsync(string tenancyReference, string description, string email, string successUrl, string sessionToken);
        Task<CompletedMandate> CompleteMandateAsync(string flowId, string sessionToken);
        Task CancelMandateAsync(string mandateId);
        Task<string> CreatePaymentAsync(string mandateId, decimal amount, DateTime chargeDate, string reference, string idempotencyKey);
        Task<CardPaymentSession> StartCardPaymentAsync(string tenancyReference, decimal amount, string returnUrl, string email);
        Task<CardPaymentResult> GetCardPaymentAsync(string sessionId);
        Task<IList<PaymentEvent>> ListEventsAsync(DateTime since);
    }
}
