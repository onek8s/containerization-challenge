using System.Collections.Concurrent;
using System.Text;
using Newtonsoft.Json;
using VanArsdel.Integration.Payments;

namespace VanArsdel.LabStubs.Stubs;

/// <summary>Klarnet merchant API with signed webhooks. The web shop simulator registers the authorisations.</summary>
public class PspStub
{
    private readonly ConcurrentDictionary<string, PspPayment> _payments = new();
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PspStub> _logger;

    public PspStub(IHttpClientFactory http, IConfiguration configuration, ILogger<PspStub> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public class LabAuthorization
    {
        public string PaymentId { get; set; }
        public string MerchantReference { get; set; }
        public long Amount { get; set; }
        public string Currency { get; set; }
        public bool Decline { get; set; }
    }

    public void Map(WebApplication app)
    {
        app.MapGet("/psp/v2/payments/{id}", (string id) => _payments.TryGetValue(id, out var p) ? Results.Json(p) : Results.NotFound());
        app.MapPost("/psp/v2/payments/{id}/captures", (string id, PspCaptureRequest r) => Operation(id, "capture", r.Amount, p => p.CapturedAmount += r.Amount));
        app.MapPost("/psp/v2/payments/{id}/refunds", (string id, PspRefundRequest r) => Operation(id, "refund", r.Amount, p => p.RefundedAmount += r.Amount));
        app.MapPost("/psp/v2/payments/{id}/cancels", (string id) => Operation(id, "cancel", 0, p => p.Status = "CANCELLED"));

        // Lab only: the checkout authorised a payment; the webhook goes to orders after a short delay.
        app.MapPost("/psp/lab/authorizations", (LabAuthorization a) =>
        {
            _payments[a.PaymentId] = new PspPayment
            {
                Id = a.PaymentId, MerchantReference = a.MerchantReference, Amount = a.Amount, Currency = a.Currency,
                Status = a.Decline ? "DECLINED" : "AUTHORIZED", Method = "card",
            };
            _ = SendWebhook(a.Decline ? PspWebhookTypes.Declined : PspWebhookTypes.Authorized, a);
            return Results.Accepted();
        });
    }

    private IResult Operation(string id, string kind, long amount, Action<PspPayment> apply)
    {
        // Unknown payments are accepted: store orders and old orders were authorised before the lab started.
        var payment = _payments.GetOrAdd(id, _ => new PspPayment { Id = id, Status = "AUTHORIZED", Currency = "EUR" });
        apply(payment);
        _logger.LogInformation("PSP {Kind} {Amount} on {Payment}", kind, amount, id);
        return Results.Json(new PspOperationResult { Id = kind + "-" + Guid.NewGuid().ToString("N")[..10], Status = "SUCCEEDED", Amount = amount });
    }

    private async Task SendWebhook(string type, LabAuthorization a)
    {
        await Task.Delay(TimeSpan.FromSeconds(_configuration.GetValue("Lab:PspWebhookDelaySeconds", 2)));
        var body = JsonConvert.SerializeObject(new PspWebhook
        {
            Id = "evt_" + Guid.NewGuid().ToString("N")[..16], Type = type, Created = DateTime.UtcNow, PaymentId = a.PaymentId,
            MerchantReference = a.MerchantReference, Amount = a.Amount, Currency = a.Currency, Reason = a.Decline ? "insufficient_funds" : null,
        });
        var url = _configuration["Lab:OrdersPspWebhook"] ?? "http://localhost:5000/webhooks/psp";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Add("Psp-Signature", WebhookSignature.Create(_configuration["Lab:PspWebhookSecret"] ?? "psp-dev-secret", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), body));
            using var response = await _http.CreateClient("webhooks").SendAsync(request);
            _logger.LogInformation("PSP webhook {Type} for {Reference} -> {Status}", type, a.MerchantReference, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PSP webhook for {Reference} failed: {Message}", a.MerchantReference, ex.Message);
        }
    }
}
