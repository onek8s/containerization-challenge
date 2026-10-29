using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Pricing;
using Proseware.ControlTower.Domain.Shipments;
using RestSharp;

namespace Proseware.ControlTower.Integration.Carriers;

/// <summary>Aerofreight Cargo booking and Cargo-IMP style status API (OAuth2 client credentials).</summary>
public sealed class AerofreightClient : RestCarrierClientBase
{
    // Cargo-IMP FSU status codes
    private static readonly Dictionary<string, string> FsuMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BKD"] = MilestoneCodes.Booked,
        ["RCS"] = MilestoneCodes.ArrivedAtTerminal,
        ["MAN"] = MilestoneCodes.Loaded,
        ["DEP"] = MilestoneCodes.Departed,
        ["ARR"] = MilestoneCodes.Arrived,
        ["RCF"] = MilestoneCodes.Discharged,
        ["NFD"] = MilestoneCodes.Arrived,
        ["CCD"] = MilestoneCodes.CustomsReleased,
        ["DLV"] = MilestoneCodes.Delivered,
        ["DIS"] = MilestoneCodes.Damaged,
    };

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _token;
    private DateTimeOffset _tokenExpires;

    public AerofreightClient(IOptions<CarrierIntegrationOptions> options, ILogger<AerofreightClient> logger) : base(options.Value, logger)
    {
    }

    public override IReadOnlyCollection<string> CarrierCodes { get; } = new[] { "AFX" };

    public override async Task<CarrierBookingResult> BookAsync(Carrier carrier, Shipment shipment, Leg leg, CancellationToken cancellationToken)
    {
        var client = CreateClient(carrier);
        var chargeable = ChargeableWeight.For(TransportMode.Air, shipment.Packages);
        var booking = new
        {
            agentAccount = Settings(carrier).AccountNumber,
            houseReference = shipment.Number,
            origin = Iata(leg.From!.Code),
            destination = Iata(leg.To!.Code),
            requestedFlightDate = leg.PlannedDeparture.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            pieces = shipment.PackageCount,
            grossWeightKg = shipment.GrossWeightKg,
            chargeableWeightKg = chargeable,
            volumeM3 = shipment.VolumeM3,
            natureOfGoods = shipment.GoodsDescription,
            specialHandling = SpecialHandlingCodes(shipment).ToArray(),
            dimensions = shipment.Packages.Select(p => new { pieces = p.Quantity, length = p.LengthCm, width = p.WidthCm, height = p.HeightCm, unit = "CMT" }),
            dangerousGoods = shipment.Packages.Where(p => p.DangerousGoods is not null)
                .Select(p => new { unNumber = "UN" + p.DangerousGoods!.UnNumber, @class = p.DangerousGoods.HazardClass, packingGroup = p.DangerousGoods.PackingGroup, netQuantity = p.DangerousGoods.NetQuantity })
        };
        var request = JsonRequest("v1/bookings", Method.POST, booking);
        request.AddHeader("Authorization", "Bearer " + await TokenAsync(carrier, cancellationToken));
        var response = await ExecuteAsync(client, request, cancellationToken);
        if (!response.IsSuccessful)
            return new CarrierBookingResult(false, null, null, $"HTTP {(int)response.StatusCode}: {response.Content}", RawResponse: response.Content);
        var created = Deserialize<AfxBookingResponse>(response);
        var accepted = created?.BookingStatus is "KK" or "CONFIRMED";
        var message = created?.Flights is { Count: > 0 } f ? "Flights " + string.Join(", ", f.Select(x => $"{x.FlightNumber} {x.Date}")) : created?.Remarks;
        return new CarrierBookingResult(accepted, created?.Awb, created?.LatestAcceptance, message, RawResponse: response.Content);
    }

    public override async Task<IReadOnlyList<StatusReported>> GetTrackingAsync(Carrier carrier, IReadOnlyList<TrackingQuery> queries, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var client = CreateClient(carrier);
        var token = await TokenAsync(carrier, cancellationToken);
        var result = new List<StatusReported>();
        foreach (var q in queries)
        {
            var request = JsonRequest($"v1/awb/{Uri.EscapeDataString(q.CarrierReference)}/events", Method.GET);
            request.AddHeader("Authorization", "Bearer " + token);
            var response = await ExecuteAsync(client, request, cancellationToken);
            if (!response.IsSuccessful)
            {
                Logger.LogWarning("AWB {Awb} events returned {Status}", q.CarrierReference, (int)response.StatusCode);
                continue;
            }
            var events = Deserialize<List<AfxEvent>>(response) ?? new List<AfxEvent>();
            foreach (var e in events.Where(e => e.EventTime > since))
            {
                if (!FsuMap.TryGetValue(e.StatusCode, out var milestone)) continue;
                var remarks = e.Flight is null ? $"{e.StatusCode} {e.Pieces} pcs" : $"{e.StatusCode} {e.Flight} {e.Pieces} pcs";
                result.Add(Status(q.ShipmentNumber, q.CarrierReference, null, milestone, e.EventTime, e.Airport + "-AP", e.Airport, "api:AFX", remarks));
            }
        }
        return result;
    }

    private async Task<string> TokenAsync(Carrier carrier, CancellationToken ct)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_token is not null && _tokenExpires > DateTimeOffset.UtcNow.AddMinutes(1)) return _token;
            var settings = Settings(carrier);
            var tokenClient = new RestClient(settings.TokenUrl ?? (settings.BaseUrl ?? carrier.ApiBaseUrl) + "oauth/token");
            var request = new RestRequest(Method.POST);
            request.AddParameter("grant_type", "client_credentials");
            request.AddParameter("client_id", settings.ClientId);
            request.AddParameter("client_secret", settings.ClientSecret);
            request.AddParameter("scope", "bookings tracking");
            var response = await ExecuteAsync(tokenClient, request, ct);
            if (!response.IsSuccessful) throw new InvalidOperationException($"Aerofreight token request failed: HTTP {(int)response.StatusCode}");
            var token = Deserialize<TokenResponse>(response)!;
            _token = token.Access_Token;
            _tokenExpires = DateTimeOffset.UtcNow.AddSeconds(token.Expires_In);
            return _token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static IEnumerable<string> SpecialHandlingCodes(Shipment s)
    {
        if (s.IsReefer) yield return "COL";
        if (s.Customer?.Code == "CPH") yield return "PIL";
        foreach (var dg in s.Packages.Select(p => p.DangerousGoods).Where(d => d is not null))
        {
            yield return dg!.UnNumber == "1845" ? "ICE" : dg.UnNumber is "3480" or "3481" ? "ELI" : "DGR";
        }
    }

    /// <summary>Our airport location codes are "&lt;LOCODE&gt;-AP"; the IATA code is the last three letters of the LOCODE.</summary>
    internal static string Iata(string locationCode) => locationCode.Length >= 5 ? locationCode.Substring(2, 3) : locationCode;

    private sealed class TokenResponse
    {
        public string Access_Token { get; set; } = "";
        public int Expires_In { get; set; }
    }

    private sealed class AfxBookingResponse
    {
        public string? Awb { get; set; }
        public string? BookingStatus { get; set; }
        public DateTimeOffset? LatestAcceptance { get; set; }
        public List<AfxFlight> Flights { get; set; } = new();
        public string? Remarks { get; set; }
    }

    private sealed class AfxFlight
    {
        public string FlightNumber { get; set; } = "";
        public string Date { get; set; } = "";
    }

    private sealed class AfxEvent
    {
        public string StatusCode { get; set; } = "";
        public DateTimeOffset EventTime { get; set; }
        public string Airport { get; set; } = "";
        public string? Flight { get; set; }
        public int Pieces { get; set; }
    }
}
