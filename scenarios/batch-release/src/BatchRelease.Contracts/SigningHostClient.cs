using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BatchRelease.Contracts;

public interface ISigningHostClient
{
    Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken ct = default);
    Task<AccessResponse> GetAccessAsync(int batchId, string sid, CancellationToken ct = default);
    Task<DocumentStored> StoreDocumentAsync(int batchId, string kind, string fileName, string contentType, Stream content, string submittedBy, CancellationToken ct = default);
    Task<DocumentContent?> OpenDocumentAsync(int batchId, int documentId, string sid, CancellationToken ct = default);
    Task<byte[]?> GetCertificateAsync(string certificateNumber, CancellationToken ct = default);
    Task<SigningHostStatus> GetStatusAsync(CancellationToken ct = default);
}

public sealed record DecisionResult(HttpStatusCode Status, DecisionResponse? Decision, DecisionProblem? Problem)
{
    public bool Succeeded => Decision is not null;
}

public sealed record DocumentContent(Stream Content, string ContentType, string FileName) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public sealed class SigningHostException : Exception
{
    public SigningHostException(HttpStatusCode status, string message) : base(message) => Status = status;

    public HttpStatusCode Status { get; }
}

/// <summary>
/// Client for the signing host. Calls are made with the Windows identity of the calling
/// process (application pool account); the signing host only accepts configured callers.
/// </summary>
public sealed class SigningHostClient : ISigningHostClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;

    public SigningHostClient(HttpClient http) => _http = http;

    public async Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync("v1/decisions", request, Json, ct);
        if (response.IsSuccessStatusCode)
            return new DecisionResult(response.StatusCode, await response.Content.ReadFromJsonAsync<DecisionResponse>(Json, ct), null);

        DecisionProblem? problem = null;
        if (response.Content.Headers.ContentType?.MediaType is "application/json" or "application/problem+json")
            problem = await response.Content.ReadFromJsonAsync<DecisionProblem>(Json, ct);
        return new DecisionResult(response.StatusCode, null, problem ?? new DecisionProblem { Code = ((int)response.StatusCode).ToString(), Message = response.ReasonPhrase ?? "Signing host error." });
    }

    public async Task<AccessResponse> GetAccessAsync(int batchId, string sid, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"v1/access/{batchId}?sid={Uri.EscapeDataString(sid)}", ct);
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<AccessResponse>(Json, ct))!;
    }

    public async Task<DocumentStored> StoreDocumentAsync(int batchId, string kind, string fileName, string contentType, Stream content, string submittedBy, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(kind), "kind");
        form.Add(new StringContent(submittedBy), "submittedBy");
        var file = new StreamContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType);
        form.Add(file, "file", fileName);

        using var response = await _http.PostAsync($"v1/batches/{batchId}/documents", form, ct);
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<DocumentStored>(Json, ct))!;
    }

    public async Task<DocumentContent?> OpenDocumentAsync(int batchId, int documentId, string sid, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"v1/batches/{batchId}/documents/{documentId}?sid={Uri.EscapeDataString(sid)}", HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            return null;
        }
        await EnsureAsync(response, ct);
        var name = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"document-{documentId}";
        return new DocumentContent(await response.Content.ReadAsStreamAsync(ct), response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream", name);
    }

    public async Task<byte[]?> GetCertificateAsync(string certificateNumber, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"v1/certificates/{Uri.EscapeDataString(certificateNumber)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureAsync(response, ct);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<SigningHostStatus> GetStatusAsync(CancellationToken ct = default)
    {
        using var response = await _http.GetAsync("v1/status", ct);
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<SigningHostStatus>(Json, ct))!;
    }

    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;
        var body = await response.Content.ReadAsStringAsync(ct);
        throw new SigningHostException(response.StatusCode, $"Signing host returned {(int)response.StatusCode}: {body}");
    }
}

public static class SigningHostClientExtensions
{
    public static IServiceCollection AddSigningHostClient(this IServiceCollection services, IConfiguration configuration)
    {
        var baseAddress = configuration["SigningHost:BaseAddress"]
            ?? throw new InvalidOperationException("SigningHost:BaseAddress is not configured.");

        services.AddHttpClient<ISigningHostClient, SigningHostClient>(c =>
            {
                c.BaseAddress = new Uri(baseAddress.EndsWith('/') ? baseAddress : baseAddress + "/");
                c.Timeout = TimeSpan.FromSeconds(configuration.GetValue("SigningHost:TimeoutSeconds", 60));
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseDefaultCredentials = true,
                PreAuthenticate = true,
            });
        return services;
    }
}
