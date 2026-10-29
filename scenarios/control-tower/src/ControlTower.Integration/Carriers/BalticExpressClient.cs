using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Shipments;
using RestSharp;

namespace Proseware.ControlTower.Integration.Carriers;

/// <summary>Baltic Express Trucking transport order API v2 (API key header).</summary>
public sealed class BalticExpressClient : RestCarrierClientBase
{
    private static readonly Dictionary<string, string> EventMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["COLLECTED"] = MilestoneCodes.PickedUp,
        ["AT_HUB"] = MilestoneCodes.ArrivedAtTerminal,
        ["LEFT_HUB"] = MilestoneCodes.DepartedTerminal,
        ["ON_FERRY"] = MilestoneCodes.Departed,
        ["FERRY_ARRIVED"] = MilestoneCodes.Arrived,
        ["IN_DELIVERY"] = MilestoneCodes.OutForDelivery,
        ["DELIVERED"] = MilestoneCodes.Delivered,
        ["NOT_DELIVERED"] = MilestoneCodes.DeliveryAttempted,
        ["DAMAGED"] = MilestoneCodes.Damaged,
        ["ETA"] = MilestoneCodes.EtaChanged,
    };

    public BalticExpressClient(IOptions<CarrierIntegrationOptions> options, ILogger<BalticExpressClient> logger) : base(options.Value, logger)
    {
    }

    public override IReadOnlyCollection<string> CarrierCodes { get; } = new[] { "BALX" };

    public override async Task<CarrierBookingResult> BookAsync(Carrier carrier, Shipment shipment, Leg leg, CancellationToken cancellationToken)
    {
        var settings = Settings(carrier);
        var client = CreateClient(carrier);
        var order = new BalxTransportOrder
        {
            CustomerNumber = settings.AccountNumber ?? "",
            OrderReference = $"{shipment.Number}-{leg.Sequence}",
            PickupDate = leg.PlannedDeparture.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            PickupFrom = leg.PlannedDeparture.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            DeliveryDate = leg.PlannedArrival.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Sender = Address(shipment.ShipperName, leg.From!),
            Receiver = Address(shipment.ConsigneeName, leg.To!),
            Temperature = shipment.IsReefer ? new BalxTemperature { Min = shipment.TemperatureMin, Max = shipment.TemperatureMax } : null,
            Goods = shipment.Packages.Select(p => new BalxGoodsLine
            {
                Sscc = p.Sscc,
                PackageType = p.Kind.ToString().ToUpperInvariant(),
                Quantity = p.Quantity,
                WeightKg = p.WeightKg * p.Quantity,
                LengthCm = p.LengthCm,
                WidthCm = p.WidthCm,
                HeightCm = p.HeightCm,
                Stackable = p.Stackable,
                Adr = p.DangerousGoods is { } dg ? new BalxAdr { UnNumber = dg.UnNumber, Class = dg.HazardClass, PackingGroup = dg.PackingGroup, Quantity = dg.NetQuantity, LimitedQuantity = dg.LimitedQuantity } : null
            }).ToList()
        };
        var request = JsonRequest("transport-orders", Method.POST, order);
        request.AddHeader("X-Api-Key", settings.ApiKey ?? "");
        var response = await ExecuteAsync(client, request, cancellationToken);
        if (!response.IsSuccessful)
            return new CarrierBookingResult(false, null, null, $"HTTP {(int)response.StatusCode}: {response.Content}", RawResponse: response.Content);
        var created = Deserialize<BalxOrderCreated>(response);
        return new CarrierBookingResult(created?.Status == "ACCEPTED", created?.ShipmentId, created?.PlannedPickup, created?.Message, RawResponse: response.Content);
    }

    public override async Task<IReadOnlyList<StatusReported>> GetTrackingAsync(Carrier carrier, IReadOnlyList<TrackingQuery> queries, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var settings = Settings(carrier);
        var client = CreateClient(carrier);
        var result = new List<StatusReported>();
        // The API returns events of all our orders changed since a timestamp, 200 per page.
        string? cursor = null;
        do
        {
            var request = JsonRequest("events", Method.GET);
            request.AddHeader("X-Api-Key", settings.ApiKey ?? "");
            request.AddQueryParameter("since", since.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            if (cursor is not null) request.AddQueryParameter("cursor", cursor);
            var response = await ExecuteAsync(client, request, cancellationToken);
            if (!response.IsSuccessful)
            {
                Logger.LogWarning("BALX events returned {Status}", (int)response.StatusCode);
                break;
            }
            var page = Deserialize<BalxEventPage>(response);
            if (page is null) break;
            var known = queries.ToDictionary(q => q.CarrierReference, StringComparer.OrdinalIgnoreCase);
            foreach (var e in page.Events)
            {
                if (!EventMap.TryGetValue(e.Code, out var milestone)) continue;
                known.TryGetValue(e.ShipmentId, out var q);
                var number = q?.ShipmentNumber ?? e.OrderReference?.Split('-').Take(2).Aggregate((a, b) => a + "-" + b);
                var remarks = e.Code == "ETA" && e.Eta is { } eta ? $"New ETA {eta:yyyy-MM-dd HH:mm}" : e.Description;
                result.Add(Status(number, e.ShipmentId, e.Sscc, milestone, e.Timestamp, e.Terminal, e.City, "api:BALX", remarks));
            }
            cursor = page.NextCursor;
        } while (!string.IsNullOrEmpty(cursor));
        return result;
    }

    private static BalxAddress Address(string name, Domain.Locations.Location l) => new()
    {
        Name = name,
        Street = l.Street,
        PostalCode = l.PostalCode,
        City = l.City,
        Country = l.CountryCode,
        Latitude = l.Position.Y,
        Longitude = l.Position.X
    };

    private sealed class BalxTransportOrder
    {
        public string CustomerNumber { get; set; } = "";
        public string OrderReference { get; set; } = "";
        public string PickupDate { get; set; } = "";
        public string PickupFrom { get; set; } = "";
        public string DeliveryDate { get; set; } = "";
        public BalxAddress Sender { get; set; } = new();
        public BalxAddress Receiver { get; set; } = new();
        public BalxTemperature? Temperature { get; set; }
        public List<BalxGoodsLine> Goods { get; set; } = new();
    }

    private sealed class BalxAddress
    {
        public string Name { get; set; } = "";
        public string Street { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string City { get; set; } = "";
        public string Country { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private sealed class BalxTemperature
    {
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
    }

    private sealed class BalxGoodsLine
    {
        public string Sscc { get; set; } = "";
        public string PackageType { get; set; } = "";
        public int Quantity { get; set; }
        public decimal WeightKg { get; set; }
        public int LengthCm { get; set; }
        public int WidthCm { get; set; }
        public int HeightCm { get; set; }
        public bool Stackable { get; set; }
        public BalxAdr? Adr { get; set; }
    }

    private sealed class BalxAdr
    {
        public string UnNumber { get; set; } = "";
        public string Class { get; set; } = "";
        public string? PackingGroup { get; set; }
        public decimal Quantity { get; set; }
        public bool LimitedQuantity { get; set; }
    }

    private sealed class BalxOrderCreated
    {
        public string ShipmentId { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTimeOffset? PlannedPickup { get; set; }
        public string? Message { get; set; }
    }

    private sealed class BalxEventPage
    {
        public List<BalxEvent> Events { get; set; } = new();
        public string? NextCursor { get; set; }
    }

    private sealed class BalxEvent
    {
        public string ShipmentId { get; set; } = "";
        public string? OrderReference { get; set; }
        public string Code { get; set; } = "";
        public string? Description { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string? Terminal { get; set; }
        public string? City { get; set; }
        public string? Sscc { get; set; }
        public DateTimeOffset? Eta { get; set; }
    }
}
