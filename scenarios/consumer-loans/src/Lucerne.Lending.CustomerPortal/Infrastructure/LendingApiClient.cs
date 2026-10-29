using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Lucerne.Lending.CustomerPortal.Infrastructure;

public sealed class LendingApiOptions
{
    public string BaseUrl { get; set; } = "https://lending-api.corp.local/";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>Server side client of the lending API. The portal never exposes the API key to the browser.</summary>
public sealed class LendingApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public LendingApiClient(HttpClient http) => _http = http;

    public Task<HttpResponseMessage> GetAsync(string path, string? onBehalfOf, CancellationToken ct) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, path), onBehalfOf, ct);

    public Task<HttpResponseMessage> PostAsync<T>(string path, T body, string? onBehalfOf, CancellationToken ct) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: Json) }, onBehalfOf, ct);

    public Task<HttpResponseMessage> PostAsync(string path, string? onBehalfOf, CancellationToken ct) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, path), onBehalfOf, ct);

    private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string? onBehalfOf, CancellationToken ct)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrEmpty(onBehalfOf)) request.Headers.Add("X-On-Behalf-Of", onBehalfOf);
        return _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    public static void Configure(HttpClient http, LendingApiOptions options)
    {
        http.BaseAddress = new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");
        http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        http.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
    }
}

/// <summary>Streams the API response (status, content type, body) back to the browser unchanged.</summary>
public sealed class ForwardedResult : IResult
{
    private readonly HttpResponseMessage _response;

    public ForwardedResult(HttpResponseMessage response) => _response = response;

    public async Task ExecuteAsync(HttpContext context)
    {
        using (_response)
        {
            context.Response.StatusCode = (int)_response.StatusCode;
            var headers = _response.Content.Headers;
            if (headers.ContentType != null) context.Response.ContentType = headers.ContentType.ToString();
            if (headers.ContentDisposition != null) context.Response.Headers["Content-Disposition"] = headers.ContentDisposition.ToString();
            if (_response.StatusCode == System.Net.HttpStatusCode.NoContent) return;
            await _response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
    }
}
