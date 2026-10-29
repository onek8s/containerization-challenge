using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Refit;

namespace VanArsdel.Integration.Payments
{
    /// <summary>
    /// Payment service provider (Klarnet Checkout) merchant API. Amounts are in minor units.
    /// Calls are authenticated with the merchant client certificate (mutual TLS) and a merchant key.
    /// </summary>
    [Headers("Accept: application/json")]
    public interface IPspApi
    {
        [Get("/v2/payments/{paymentId}")]
        Task<PspPayment> GetPaymentAsync(string paymentId);

        [Post("/v2/payments/{paymentId}/captures")]
        Task<PspOperationResult> CaptureAsync(string paymentId, [Body] PspCaptureRequest request, [Header("Idempotency-Key")] string idempotencyKey);

        [Post("/v2/payments/{paymentId}/refunds")]
        Task<PspOperationResult> RefundAsync(string paymentId, [Body] PspRefundRequest request, [Header("Idempotency-Key")] string idempotencyKey);

        [Post("/v2/payments/{paymentId}/cancels")]
        Task<PspOperationResult> CancelAsync(string paymentId, [Header("Idempotency-Key")] string idempotencyKey);
    }

    public class PspPayment
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("merchantReference")] public string MerchantReference { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("amount")] public long Amount { get; set; }
        [JsonProperty("capturedAmount")] public long CapturedAmount { get; set; }
        [JsonProperty("refundedAmount")] public long RefundedAmount { get; set; }
        [JsonProperty("currency")] public string Currency { get; set; }
        [JsonProperty("method")] public string Method { get; set; }
    }

    public class PspCaptureRequest
    {
        [JsonProperty("amount")] public long Amount { get; set; }
        [JsonProperty("reference")] public string Reference { get; set; }
        [JsonProperty("final")] public bool Final { get; set; }
    }

    public class PspRefundRequest
    {
        [JsonProperty("amount")] public long Amount { get; set; }
        [JsonProperty("reference")] public string Reference { get; set; }
        [JsonProperty("reason")] public string Reason { get; set; }
    }

    public class PspOperationResult
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("amount")] public long Amount { get; set; }
    }

    /// <summary>Webhook body posted by the PSP to /webhooks/psp.</summary>
    public class PspWebhook
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("created")] public DateTime Created { get; set; }
        [JsonProperty("paymentId")] public string PaymentId { get; set; }
        [JsonProperty("merchantReference")] public string MerchantReference { get; set; }
        [JsonProperty("amount")] public long Amount { get; set; }
        [JsonProperty("currency")] public string Currency { get; set; }
        [JsonProperty("reason")] public string Reason { get; set; }
    }

    public static class PspWebhookTypes
    {
        public const string Authorized = "payment.authorized";
        public const string Declined = "payment.declined";
        public const string Captured = "payment.captured";
        public const string Refunded = "payment.refunded";
        public const string ChargebackOpened = "chargeback.opened";
    }

    public static class Money
    {
        public static long ToMinor(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
        public static decimal FromMinor(long minor) => minor / 100m;
    }
}
