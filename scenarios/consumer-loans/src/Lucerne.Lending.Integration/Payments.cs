using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.Integration;

public sealed class PaymentsHubOptions
{
    public const string Section = "PaymentsHub";
    public string BaseUrl { get; set; } = "https://payhub.corp.local/";
    public string ClientId { get; set; } = "consumer-lending";
    public string ClientSecret { get; set; } = "";
    /// <summary>Use SEPA Instant for external disbursements up to this amount.</summary>
    public decimal InstantLimit { get; set; } = 15000m;
}

public sealed record ReleasePaymentRequest(string CoreTransactionId, string DebtorAccount, string CreditorIban, string CreditorName,
    decimal Amount, string RemittanceInfo, bool Instant);

public sealed record ReleasePaymentResponse(string PaymentId, string Status);

/// <summary>Internal payments hub: releases the outgoing SEPA transfer the core created for an external disbursement.</summary>
public sealed class PaymentsHubClient
{
    private readonly HttpClient _http;
    private readonly PaymentsHubOptions _options;
    private readonly ILogger<PaymentsHubClient> _log;

    public PaymentsHubClient(HttpClient http, IOptions<PaymentsHubOptions> options, ILogger<PaymentsHubClient> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
        _http.BaseAddress ??= new Uri(_options.BaseUrl);
        _http.DefaultRequestHeaders.Add("X-Client-Id", _options.ClientId);
        _http.DefaultRequestHeaders.Add("X-Client-Secret", _options.ClientSecret);
    }

    public decimal InstantLimit => _options.InstantLimit;

    public async Task<ReleasePaymentResponse> ReleaseAsync(ReleasePaymentRequest request, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("payments/v2/release", request, ct);
                if ((int)response.StatusCode >= 500 && attempt < 3)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
                    continue;
                }
                response.EnsureSuccessStatusCode();
                var result = (await response.Content.ReadFromJsonAsync<ReleasePaymentResponse>(cancellationToken: ct))!;
                _log.LogInformation("Payment {PaymentId} released for core txn {Txn}: {Status}", result.PaymentId, request.CoreTransactionId, result.Status);
                return result;
            }
            catch (HttpRequestException ex) when (attempt < 3)
            {
                _log.LogWarning(ex, "Payments hub call failed, attempt {Attempt}", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
            }
        }
    }
}
