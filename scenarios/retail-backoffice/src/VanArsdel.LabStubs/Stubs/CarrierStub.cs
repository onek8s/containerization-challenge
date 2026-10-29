using System.Collections.Concurrent;
using System.Text;
using Newtonsoft.Json;
using VanArsdel.Integration.Carrier;

namespace VanArsdel.LabStubs.Stubs;

/// <summary>NordPost parcel API. Shipments move PICKED_UP -> DELIVERED (or READY_FOR_PICKUP) on a timer and push webhooks.</summary>
public class CarrierStub
{
    private readonly ConcurrentDictionary<string, CarrierShipmentResponse> _shipments = new();
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CarrierStub> _logger;
    // Seeded from the clock so that a restarted stub does not hand out shipment ids the services already know.
    private long _sequence = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 10_000_000 * 10;

    public CarrierStub(IHttpClientFactory http, IConfiguration configuration, ILogger<CarrierStub> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public void Map(WebApplication app)
    {
        var apiKey = _configuration["Lab:CarrierApiKey"] ?? "dev";
        bool Authorized(HttpRequest r) => r.Headers["X-Api-Key"] == apiKey;

        app.MapPost("/carrier/v2/shipments", (HttpRequest r, CarrierShipmentRequest request) =>
        {
            if (!Authorized(r)) return Results.Unauthorized();
            var id = "NP" + Interlocked.Increment(ref _sequence);
            var tracking = "JJFI" + Random.Shared.NextInt64(100000000000, 999999999999);
            var shipment = new CarrierShipmentResponse
            {
                Id = id, TrackingNumber = tracking, Status = "CREATED",
                TrackingUrl = $"{Base(r)}/carrier/track/{tracking}", LabelUrl = $"{Base(r)}/carrier/labels/{id}.pdf",
            };
            _shipments[id] = shipment;
            _logger.LogInformation("Carrier shipment {Id} {Service} for {Reference}", id, request.Service, request.Reference);
            var final = string.IsNullOrEmpty(request.PickupPointId) ? CarrierEvents.Delivered : CarrierEvents.ReadyForPickup;
            _ = Progress(shipment, request.Reference, final, _configuration["Lab:OrdersCarrierWebhook"] ?? "http://localhost:5000/webhooks/carrier");
            return Results.Json(shipment);
        });
        app.MapGet("/carrier/v2/shipments/{id}", (HttpRequest r, string id) =>
            !Authorized(r) ? Results.Unauthorized() : _shipments.TryGetValue(id, out var s) ? Results.Json(s) : Results.NotFound());
        app.MapDelete("/carrier/v2/shipments/{id}", (HttpRequest r, string id) =>
        {
            if (!Authorized(r)) return Results.Unauthorized();
            if (_shipments.TryGetValue(id, out var s)) s.Status = "CANCELLED";
            return Results.NoContent();
        });
        app.MapPost("/carrier/v2/returns", (HttpRequest r, CarrierReturnRequest request) =>
        {
            if (!Authorized(r)) return Results.Unauthorized();
            var id = "NR" + Interlocked.Increment(ref _sequence);
            var tracking = "JJRF" + Random.Shared.NextInt64(100000000000, 999999999999);
            var shipment = new CarrierShipmentResponse { Id = id, TrackingNumber = tracking, Status = "CREATED" };
            _shipments[id] = shipment;
            _ = Progress(shipment, request.Reference, CarrierEvents.Delivered, _configuration["Lab:ReturnsCarrierWebhook"] ?? "http://localhost:5000/webhooks/carrier-returns");
            return Results.Json(new CarrierReturnResponse { Id = id, TrackingNumber = tracking, LabelUrl = $"{Base(r)}/carrier/labels/{id}.pdf", QrCodeUrl = $"{Base(r)}/carrier/qr/{id}.png" });
        });
        app.MapGet("/carrier/v2/pickup-points", (string postalCode, string country) => Results.Json(new[]
        {
            new CarrierPickupPoint { Id = "PP-" + postalCode + "-1", Name = "Parcel locker K-Market", Street = "Kauppakatu 1", City = "Lab" },
            new CarrierPickupPoint { Id = "PP-" + postalCode + "-2", Name = "Parcel locker Station", Street = "Asemakatu 4", City = "Lab" },
        }));
        app.MapGet("/carrier/labels/{file}", (string file) => Results.Bytes(MinimalPdf(file), "application/pdf"));
        app.MapGet("/carrier/track/{tracking}", (string tracking) =>
            Results.Content($"<h3>Tracking {tracking}</h3><p>{_shipments.Values.FirstOrDefault(s => s.TrackingNumber == tracking)?.Status ?? "unknown"}</p>", "text/html"));
    }

    private async Task Progress(CarrierShipmentResponse shipment, string reference, string final, string webhookUrl)
    {
        var delay = TimeSpan.FromSeconds(_configuration.GetValue("Lab:CarrierStepSeconds", 5));
        foreach (var e in new[] { CarrierEvents.PickedUp, CarrierEvents.InTransit, final })
        {
            await Task.Delay(delay);
            if (shipment.Status == "CANCELLED") return;
            shipment.Status = e;
            var body = JsonConvert.SerializeObject(new CarrierTrackingEvent
            {
                ShipmentId = shipment.Id, Reference = reference, TrackingNumber = shipment.TrackingNumber, Event = e, Timestamp = DateTime.UtcNow, Location = "Lab terminal",
            });
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                request.Headers.Add("X-Carrier-Token", _configuration["Lab:CarrierWebhookToken"] ?? "carrier-dev-token");
                using var response = await _http.CreateClient("webhooks").SendAsync(request);
                _logger.LogInformation("Carrier webhook {Event} for {Id} -> {Status}", e, shipment.Id, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Carrier webhook {Event} for {Id} failed: {Message}", e, shipment.Id, ex.Message);
            }
        }
    }

    private static string Base(HttpRequest r) => $"{r.Scheme}://{r.Host}";

    private static byte[] MinimalPdf(string title) => Encoding.ASCII.GetBytes(
        "%PDF-1.1\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n" +
        "3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 288 432]>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n% label " + title);
}
