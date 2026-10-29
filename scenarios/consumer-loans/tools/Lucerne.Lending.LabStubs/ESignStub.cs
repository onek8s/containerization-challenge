using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lucerne.Lending.LabStubs;

/// <summary>Test double of the SignHub e-signature API. Orders are kept in memory.</summary>
public sealed class ESignStub
{
    private sealed class Order
    {
        public string Id { get; init; } = "";
        public string State { get; set; } = "pending";
        public string ExternalReference { get; init; } = "";
        public string CallbackUrl { get; init; } = "";
        public byte[] Pdf { get; init; } = Array.Empty<byte>();
        public List<(string Name, string Email)> Signers { get; init; } = new();
    }

    private readonly ConcurrentDictionary<string, Order> _orders = new();
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ESignStub> _log;

    public ESignStub(IConfiguration configuration, IHttpClientFactory http, ILogger<ESignStub> log)
    {
        _configuration = configuration;
        _http = http;
        _log = log;
    }

    private string BaseUrl => (_configuration["Stubs:PublicUrl"] ?? "http://localhost:5109").TrimEnd('/');

    public async Task<IResult> CreateAsync(HttpRequest request)
    {
        if (request.Headers["X-Api-Key"] != (_configuration["Stubs:ESignApiKey"] ?? "dev-esign-key")) return Results.Unauthorized();
        var body = JsonNode.Parse(await new StreamReader(request.Body).ReadToEndAsync())!;
        var order = new Order
        {
            Id = "SH-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            ExternalReference = (string?)body["externalReference"] ?? "",
            CallbackUrl = (string?)body["callbackUrl"] ?? "",
            Pdf = Convert.FromBase64String((string)body["documents"]![0]!["content"]!),
            Signers = body["signers"]!.AsArray().Select(s => ((string)s!["name"]!, (string)s["email"]!)).ToList()
        };
        _orders[order.Id] = order;
        _log.LogInformation("Signing order {Id} for {Reference}", order.Id, order.ExternalReference);
        return Results.Json(View(order));
    }

    public IResult Get(string id) => _orders.TryGetValue(id, out var o) ? Results.Json(View(o)) : Results.NotFound();

    public IResult Signed(string id) =>
        _orders.TryGetValue(id, out var o) && o.State == "signed" ? Results.File(o.Pdf, "application/pdf") : Results.NotFound();

    public async Task<IResult> CancelAsync(string id)
    {
        if (!_orders.TryGetValue(id, out var o)) return Results.NotFound();
        if (o.State != "pending") return Results.Conflict();
        o.State = "cancelled";
        await CallbackAsync(o);
        return Results.Ok();
    }

    public IResult Page(string id)
    {
        if (!_orders.TryGetValue(id, out var o)) return Results.NotFound();
        var html = $@"<!doctype html><html><head><meta charset=""utf-8""><title>SignHub (test)</title>
<style>body{{font-family:sans-serif;max-width:640px;margin:40px auto}}button{{padding:10px 18px;font-size:16px}}</style></head><body>
<h2>SignHub test signing</h2><p>Document: <b>{o.ExternalReference}</b> ({o.Pdf.Length / 1024} kB)</p><p>Signers: {string.Join(", ", o.Signers.Select(s => s.Name))}</p>
<p>State: <b>{o.State}</b></p>
{(o.State == "pending" ? $@"<form method=""post"" action=""{BaseUrl}/esign/sign/{o.Id}""><button>Identify with test bank and sign</button></form>" : "")}
</body></html>";
        return Results.Content(html, "text/html");
    }

    public async Task<IResult> SignAsync(string id)
    {
        if (!_orders.TryGetValue(id, out var o)) return Results.NotFound();
        if (o.State == "pending")
        {
            o.State = "signed";
            await CallbackAsync(o);
        }
        return Results.Redirect($"{BaseUrl}/esign/sign/{id}");
    }

    private object View(Order o) => new
    {
        id = o.Id,
        state = o.State,
        signers = o.Signers.Select(s => new { email = s.Email, url = $"{BaseUrl}/esign/sign/{o.Id}", state = o.State })
    };

    private async Task CallbackAsync(Order o)
    {
        if (string.IsNullOrEmpty(o.CallbackUrl)) return;
        var json = JsonSerializer.Serialize(new { orderId = o.Id, externalReference = o.ExternalReference, state = o.State, timestamp = DateTime.UtcNow });
        var secret = _configuration["Stubs:ESignCallbackSecret"] ?? "dev-callback-secret";
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        using var message = new HttpRequestMessage(HttpMethod.Post, o.CallbackUrl) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        message.Headers.Add("X-SignHub-Signature", signature);
        try
        {
            var response = await _http.CreateClient().SendAsync(message);
            _log.LogInformation("Callback {State} for {Id}: HTTP {Status}", o.State, o.Id, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "Callback for {Id} failed", o.Id);
        }
    }
}
