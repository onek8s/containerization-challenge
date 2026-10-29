using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace VanArsdel.LabStubs.Tools;

/// <summary>Calls the services through the gateway as a lab user, with tokens from the lab issuer.</summary>
public class LabClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(100) };
    private readonly string _issuer;
    private readonly string _gateway;
    private readonly string _user;
    private string _token;

    public LabClient(IConfiguration configuration, string user = "admin")
    {
        _issuer = configuration["Lab:Issuer"] ?? "http://localhost:5100";
        _gateway = (configuration["Lab:Gateway"] ?? "http://localhost:5000").TrimEnd('/');
        _user = user;
    }

    public string Gateway => _gateway;

    private async Task<string> TokenAsync()
    {
        if (_token != null) return _token;
        var response = await _http.PostAsync(_issuer + "/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password", ["client_id"] = "labtools", ["client_secret"] = "dev-secret", ["username"] = _user, ["password"] = "lab",
        }));
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        return _token = doc.GetProperty("access_token").GetString();
    }

    public async Task<JsonElement> GetAsync(string path)
    {
        using var response = await SendAsync(HttpMethod.Get, path, null);
        return await ReadAsync(response, path);
    }

    public async Task<JsonElement> SendJsonAsync(HttpMethod method, string path, object body, params HttpStatusCode[] accepted)
    {
        using var response = await SendAsync(method, path, body);
        if (accepted.Contains(response.StatusCode)) return default;
        return await ReadAsync(response, path);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body)
    {
        var request = new HttpRequestMessage(method, _gateway + "/" + path.TrimStart('/'));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync());
        if (body != null) request.Content = JsonContent.Create(body, options: Json);
        else if (method != HttpMethod.Get) request.Content = new StringContent("");
        return await _http.SendAsync(request);
    }

    public Task<HttpResponseMessage> PostAnonymousAsync(string url, object body) =>
        _http.PostAsJsonAsync(url, body, Json);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, string path)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode} {path}: {(text.Length > 400 ? text[..400] : text)}", null, response.StatusCode);
        }
        return string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }
}
