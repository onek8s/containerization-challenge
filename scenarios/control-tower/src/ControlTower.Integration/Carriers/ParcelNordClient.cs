using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Shipments;
using RestSharp;

namespace Proseware.ControlTower.Integration.Carriers;

/// <summary>ParcelNord shipment API: creates parcels, returns ZPL labels and tracking numbers; tracking is pushed by webhook.</summary>
public sealed class ParcelNordClient : RestCarrierClientBase
{
    internal static readonly Dictionary<string, string> EventMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PICKED_UP"] = MilestoneCodes.PickedUp,
        ["IN_TERMINAL"] = MilestoneCodes.ArrivedAtTerminal,
        ["IN_TRANSIT"] = MilestoneCodes.DepartedTerminal,
        ["OUT_FOR_DELIVERY"] = MilestoneCodes.OutForDelivery,
        ["DELIVERED"] = MilestoneCodes.Delivered,
        ["DELIVERED_TO_PICKUP_POINT"] = MilestoneCodes.Delivered,
        ["DELIVERY_FAILED"] = MilestoneCodes.DeliveryAttempted,
        ["DAMAGED"] = MilestoneCodes.Damaged,
    };

    public ParcelNordClient(IOptions<CarrierIntegrationOptions> options, ILogger<ParcelNordClient> logger) : base(options.Value, logger)
    {
    }

    public override IReadOnlyCollection<string> CarrierCodes { get; } = new[] { "PRCL" };

    public override async Task<CarrierBookingResult> BookAsync(Carrier carrier, Shipment shipment, Leg leg, CancellationToken cancellationToken)
    {
        var settings = Settings(carrier);
        var client = CreateClient(carrier);
        var body = new
        {
            account = settings.AccountNumber,
            reference = shipment.Number,
            service = "BUSINESS_PARCEL",
            labelFormat = "ZPL203",
            sender = new { name = shipment.ShipperName, street = leg.From!.Street, postalCode = leg.From.PostalCode, city = leg.From.City, country = leg.From.CountryCode },
            receiver = new { name = shipment.ConsigneeName, street = leg.To!.Street, postalCode = leg.To.PostalCode, city = leg.To.City, country = leg.To.CountryCode, email = shipment.ConsigneeEmail, phone = shipment.NotifyPhone },
            parcels = shipment.Packages.Select(p => new { reference = p.Sscc, weight = p.WeightKg, length = p.LengthCm, width = p.WidthCm, height = p.HeightCm })
        };
        var request = JsonRequest("shipments", Method.POST, body);
        request.AddHeader("Authorization", "ApiKey " + settings.ApiKey);
        var response = await ExecuteAsync(client, request, cancellationToken);
        if (!response.IsSuccessful)
            return new CarrierBookingResult(false, null, null, $"HTTP {(int)response.StatusCode}: {response.Content}", RawResponse: response.Content);
        var created = Deserialize<PnShipment>(response);
        var zpl = created?.Labels is { Count: > 0 } labels
            ? string.Concat(labels.Select(l => Encoding.ASCII.GetString(Convert.FromBase64String(l))))
            : null;
        return new CarrierBookingResult(created is not null, created?.TrackingNumber, null, null, zpl);
    }

    public override async Task<IReadOnlyList<StatusReported>> GetTrackingAsync(Carrier carrier, IReadOnlyList<TrackingQuery> queries, DateTimeOffset since, CancellationToken cancellationToken)
    {
        // Normally pushed by webhook; this pull is the catch-up for missed deliveries.
        var settings = Settings(carrier);
        var client = CreateClient(carrier);
        var result = new List<StatusReported>();
        foreach (var q in queries)
        {
            var request = JsonRequest($"tracking/{Uri.EscapeDataString(q.CarrierReference)}", Method.GET);
            request.AddHeader("Authorization", "ApiKey " + settings.ApiKey);
            var response = await ExecuteAsync(client, request, cancellationToken);
            if (!response.IsSuccessful) continue;
            var tracking = Deserialize<PnTracking>(response);
            if (tracking is null) continue;
            result.AddRange(tracking.Events.Where(e => e.Time > since).Select(e => ToStatus(e, tracking.TrackingNumber, q.ShipmentNumber)).OfType<StatusReported>());
        }
        return result;
    }

    /// <summary>Webhook body: {"trackingNumber": "...", "reference": "PF24-...", "event": {...}}.</summary>
    public static StatusReported? ParseWebhook(string json)
    {
        var hook = JsonConvert.DeserializeObject<PnWebhook>(json, Json);
        return hook?.Event is null ? null : ToStatus(hook.Event, hook.TrackingNumber, hook.Reference);
    }

    /// <summary>Signature header "X-ParcelNord-Signature: t=&lt;unix&gt;,v1=&lt;hex hmac-sha256 of "t.body"&gt;".</summary>
    public static bool VerifySignature(string header, string body, string secret, DateTimeOffset now, TimeSpan tolerance)
    {
        if (string.IsNullOrEmpty(header) || string.IsNullOrEmpty(secret)) return false;
        string? t = null, v1 = null;
        foreach (var part in header.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0].Trim() == "t") t = kv[1].Trim();
            if (kv[0].Trim() == "v1") v1 = kv[1].Trim();
        }
        if (t is null || v1 is null || !long.TryParse(t, out var unix)) return false;
        if ((now - DateTimeOffset.FromUnixTimeSeconds(unix)).Duration() > tolerance) return false;
        var expected = Sign(t, body, secret);
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(v1.ToLowerInvariant()));
    }

    public static string Sign(string timestamp, string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp + "." + body))).ToLowerInvariant();
    }

    public static string SignatureHeader(string body, string secret, DateTimeOffset now)
    {
        var t = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return $"t={t},v1={Sign(t, body, secret)}";
    }

    private static StatusReported? ToStatus(PnEvent e, string trackingNumber, string? reference)
    {
        if (!EventMap.TryGetValue(e.Type, out var milestone)) return null;
        var remarks = e.Type == "DELIVERED_TO_PICKUP_POINT" ? $"Delivered to pickup point {e.Location?.Name}" : e.Description;
        if (!string.IsNullOrEmpty(e.SignedBy)) remarks = $"{remarks} Signed by {e.SignedBy}".Trim();
        return new StatusReported(IntegrationEvent.NewId(), e.Time, reference, trackingNumber, e.ParcelReference, milestone, null, e.Location?.City, "api:PRCL", remarks);
    }

    private sealed class PnShipment
    {
        public string TrackingNumber { get; set; } = "";
        public List<string> Labels { get; set; } = new();
    }

    private sealed class PnTracking
    {
        public string TrackingNumber { get; set; } = "";
        public List<PnEvent> Events { get; set; } = new();
    }

    private sealed class PnWebhook
    {
        public string TrackingNumber { get; set; } = "";
        public string? Reference { get; set; }
        public PnEvent? Event { get; set; }
    }

    private sealed class PnEvent
    {
        public string Type { get; set; } = "";
        public DateTimeOffset Time { get; set; }
        public string? Description { get; set; }
        public string? ParcelReference { get; set; }
        public string? SignedBy { get; set; }
        public PnLocation? Location { get; set; }
    }

    private sealed class PnLocation
    {
        public string? Name { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
    }
}
