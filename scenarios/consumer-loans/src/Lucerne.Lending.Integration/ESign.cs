using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestSharp;

namespace Lucerne.Lending.Integration;

public sealed class ESignOptions
{
    public const string Section = "ESign";
    public string BaseUrl { get; set; } = "https://api.signhub.example";
    public string ApiKey { get; set; } = "";
    public string CallbackUrl { get; set; } = "https://lending-api.corp.local/api/esign/callback";
    public string CallbackSecret { get; set; } = "";
    public string ReturnUrl { get; set; } = "https://apply.corp.local/applications";
    public int ValidDays { get; set; } = 14;
}

public sealed class SigningOrder
{
    public string Id { get; set; } = "";
    public string State { get; set; } = "";
    public List<SignerLink> Signers { get; set; } = new();
}

public sealed class SignerLink
{
    public string Email { get; set; } = "";
    public string Url { get; set; } = "";
    public string State { get; set; } = "";
}

public sealed record Signer(string Name, string Email, string PersonalIdentityCode);

public interface IESignClient
{
    Task<SigningOrder> CreateOrderAsync(string externalReference, string fileName, byte[] pdf, IReadOnlyList<Signer> signers, CancellationToken ct = default);
    Task<SigningOrder> GetOrderAsync(string orderId, CancellationToken ct = default);
    Task<byte[]> DownloadSignedAsync(string orderId, CancellationToken ct = default);
    Task CancelAsync(string orderId, CancellationToken ct = default);
}

/// <summary>SignHub REST API v1 (strong electronic identification with bank credentials).</summary>
public sealed class ESignClient : IESignClient
{
    private readonly ESignOptions _options;
    private readonly ILogger<ESignClient> _log;
    private readonly RestClient _client;

    public ESignClient(IOptions<ESignOptions> options, ILogger<ESignClient> log)
    {
        _options = options.Value;
        _log = log;
        _client = new RestClient(_options.BaseUrl) { Timeout = 30000 };
        _client.AddDefaultHeader("X-Api-Key", _options.ApiKey);
    }

    public async Task<SigningOrder> CreateOrderAsync(string externalReference, string fileName, byte[] pdf, IReadOnlyList<Signer> signers,
        CancellationToken ct = default)
    {
        var request = new RestRequest("api/v1/orders", Method.POST, DataFormat.Json);
        request.AddJsonBody(new
        {
            externalReference,
            validUntil = DateTime.UtcNow.AddDays(_options.ValidDays).ToString("O"),
            callbackUrl = _options.CallbackUrl,
            returnUrl = _options.ReturnUrl,
            language = "fi",
            authenticationMethod = "FTN",
            documents = new[] { new { name = fileName, contentType = "application/pdf", content = Convert.ToBase64String(pdf) } },
            signers = signers.Select(s => new { name = s.Name, email = s.Email, identifier = s.PersonalIdentityCode, identifierType = "FI_HETU" })
        });
        var response = await _client.ExecuteAsync<SigningOrder>(request, ct);
        Ensure(response, "create signing order");
        _log.LogInformation("Signing order {OrderId} created for {Reference}", response.Data.Id, externalReference);
        return response.Data;
    }

    public async Task<SigningOrder> GetOrderAsync(string orderId, CancellationToken ct = default)
    {
        var response = await _client.ExecuteAsync<SigningOrder>(new RestRequest($"api/v1/orders/{orderId}", Method.GET), ct);
        Ensure(response, "read signing order");
        return response.Data;
    }

    public async Task<byte[]> DownloadSignedAsync(string orderId, CancellationToken ct = default)
    {
        var response = await _client.ExecuteAsync(new RestRequest($"api/v1/orders/{orderId}/documents/0/signed", Method.GET), ct);
        Ensure(response, "download signed document");
        return response.RawBytes;
    }

    public async Task CancelAsync(string orderId, CancellationToken ct = default)
    {
        var response = await _client.ExecuteAsync(new RestRequest($"api/v1/orders/{orderId}/cancel", Method.POST), ct);
        if (response.StatusCode != HttpStatusCode.Conflict) Ensure(response, "cancel signing order");
    }

    private static void Ensure(IRestResponse response, string what)
    {
        if (response.ErrorException != null)
            throw new IntegrationException($"E-signature: could not {what}: {response.ErrorMessage}", response.ErrorException);
        if ((int)response.StatusCode is < 200 or > 299)
            throw new IntegrationException($"E-signature: could not {what}: HTTP {(int)response.StatusCode} {response.Content}");
    }
}

public sealed class IntegrationException : Exception
{
    public IntegrationException(string message, Exception? inner = null) : base(message, inner) { }
}
