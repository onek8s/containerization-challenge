using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace ExpenseClaims.Jobs.Bank;

public sealed record GatewayUploadResult(string FileReference);

public sealed record GatewayStatus(string FileReference, string Status, string? Reason);

/// <summary>Client for the bank's corporate payment gateway. Authenticates with a TLS client certificate.</summary>
public sealed class BankGatewayClient
{
    private readonly HttpClient _http;
    private readonly BankOptions _options;

    public BankGatewayClient(HttpClient http, IOptions<BankOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<GatewayUploadResult> UploadPain001Async(string fileName, byte[] content, CancellationToken ct)
    {
        using var body = new ByteArrayContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/xml") { CharSet = "utf-8" };
        using var request = new HttpRequestMessage(HttpMethod.Post, "payments/pain001") { Content = body };
        request.Headers.Add("X-Customer-Id", _options.CustomerId);
        request.Headers.Add("X-File-Name", fileName);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Bank gateway rejected {fileName}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");
        return await response.Content.ReadFromJsonAsync<GatewayUploadResult>(cancellationToken: ct)
            ?? throw new HttpRequestException("Bank gateway returned an empty response.");
    }

    public async Task<GatewayStatus> GetStatusAsync(string fileReference, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"payments/{Uri.EscapeDataString(fileReference)}/status");
        request.Headers.Add("X-Customer-Id", _options.CustomerId);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GatewayStatus>(cancellationToken: ct)
            ?? throw new HttpRequestException("Bank gateway returned an empty status.");
    }

    /// <summary>Loads the client certificate from the local machine certificate store.</summary>
    public static X509Certificate2 LoadClientCertificate(string thumbprint)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint.Replace(" ", ""), validOnly: false);
        if (found.Count == 0)
            throw new InvalidOperationException($"Bank client certificate {thumbprint} was not found in LocalMachine\\My.");
        var certificate = found[0];
        if (!certificate.HasPrivateKey)
            throw new InvalidOperationException($"Bank client certificate {thumbprint} has no private key, or the application pool account cannot read it.");
        return certificate;
    }
}
