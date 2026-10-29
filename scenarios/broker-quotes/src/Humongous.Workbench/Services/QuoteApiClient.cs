using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Humongous.Quoting.Contracts;

namespace Humongous.Workbench.Services;

public sealed class ApiProblemException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

/// <summary>
/// Typed client for the quote API, called with the underwriter's token. The token is added here and
/// not in a message handler: handlers live in the HttpClientFactory's own scope, not the circuit's.
/// </summary>
public sealed class QuoteApiClient(HttpClient http, UserTokens tokens)
{
    private static readonly JsonSerializerOptions Json = QuotingJson.Options;

    public Task<List<ReferralQueueItem>> ReferralsAsync(CancellationToken ct = default) => GetAsync<List<ReferralQueueItem>>("api/referrals", ct);

    public Task<QuoteDetail> QuoteAsync(Guid id, CancellationToken ct = default) => GetAsync<QuoteDetail>($"api/quotes/{id}", ct);

    public Task<List<QuoteSummary>> QuotesAsync(string? status, string? search, int page, CancellationToken ct = default)
    {
        var query = $"api/quotes?page={page}";
        if (!string.IsNullOrEmpty(status)) query += $"&status={Uri.EscapeDataString(status)}";
        if (!string.IsNullOrEmpty(search)) query += $"&search={Uri.EscapeDataString(search)}";
        return GetAsync<List<QuoteSummary>>(query, ct);
    }

    public Task<List<PolicySummary>> PoliciesAsync(string? search, CancellationToken ct = default) =>
        GetAsync<List<PolicySummary>>($"api/policies?search={Uri.EscapeDataString(search ?? "")}", ct);

    public Task<PolicyDetail> PolicyAsync(Guid id, CancellationToken ct = default) => GetAsync<PolicyDetail>($"api/policies/{id}", ct);

    public Task<List<AgencyInfo>> AgenciesAsync(CancellationToken ct = default) => GetAsync<List<AgencyInfo>>("api/agencies", ct);

    public Task<List<BordereauRunInfo>> BordereauxAsync(CancellationToken ct = default) => GetAsync<List<BordereauRunInfo>>("api/bordereaux", ct);

    public Task<Dashboard> DashboardAsync(CancellationToken ct = default) => GetAsync<Dashboard>("api/dashboard", ct);

    public Task<JsonElement> RatingStatusAsync(CancellationToken ct = default) => GetAsync<JsonElement>("api/rating/status", ct);

    public async Task AssignAsync(Guid quoteId, CancellationToken ct = default) =>
        await SendAsync(HttpMethod.Post, $"api/referrals/{quoteId}/assign", new { }, ct);

    public async Task<QuoteDetail> DecideAsync(Guid quoteId, ReferralDecisionRequest decision, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/referrals/{quoteId}/decision", decision, ct);
        return (await response.Content.ReadFromJsonAsync<QuoteDetail>(Json, ct))!;
    }

    public async Task UpdateAgencyAsync(string code, AgencyUpdate update, CancellationToken ct = default) =>
        await SendAsync(HttpMethod.Put, $"api/agencies/{code}", update, ct);

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        using var response = await http.SendAsync(await RequestAsync(HttpMethod.Get, path, null, ct), ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body, CancellationToken ct)
    {
        var response = await http.SendAsync(await RequestAsync(method, path, body, ct), ct);
        await EnsureSuccessAsync(response, ct);
        return response;
    }

    private async Task<HttpRequestMessage> RequestAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        var token = await tokens.GetAccessTokenAsync(ct);
        if (token is not null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var message = response.ReasonPhrase ?? response.StatusCode.ToString();
        try
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (problem.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                message = detail.GetString()!;
            else if (problem.RootElement.TryGetProperty("errors", out var errors))
                message = string.Join(" ", errors.EnumerateObject().SelectMany(e => e.Value.EnumerateArray().Select(v => v.GetString())));
        }
        catch (JsonException)
        {
        }
        throw new ApiProblemException(response.StatusCode, message);
    }
}
