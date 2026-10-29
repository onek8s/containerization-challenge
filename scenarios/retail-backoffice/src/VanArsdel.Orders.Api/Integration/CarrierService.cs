using Microsoft.Extensions.Options;
using Refit;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.Integration.Carrier;
using VanArsdel.Orders.Api.Domain;

namespace VanArsdel.Orders.Api.Integration;

public class CarrierOptions
{
    public string BaseUrl { get; set; } = "https://api.nordpost.example";
    public string ApiKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string Name { get; set; } = "NordPost";
}

public class LocationAddress
{
    public string Name { get; set; } = "";
    public string Street { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
}

public class CarrierService
{
    private readonly ICarrierApi _api;
    private readonly CarrierOptions _options;
    private readonly IConfiguration _configuration;

    public CarrierService(ICarrierApi api, IOptions<CarrierOptions> options, IConfiguration configuration)
    {
        _api = api;
        _options = options.Value;
        _configuration = configuration;
    }

    public string Name => _options.Name;

    public async Task<CarrierShipmentResponse> BookAsync(Order order, Shipment shipment, int parcels, decimal weightKg)
    {
        var sender = SenderFor(shipment.FromLocation);
        var receiver = order.DeliveryMethod == Contracts.Orders.DeliveryMethods.ClickAndCollect
            ? Party(SenderFor(order.CollectStoreCode ?? "DC01"), order)
            : new CarrierParty
            {
                Name = order.ShipToName,
                Street = order.ShipToStreet,
                PostalCode = order.ShipToPostalCode,
                City = order.ShipToCity,
                Country = order.ShipToCountry,
                Email = order.CustomerEmail,
                Phone = order.CustomerPhone,
            };

        var request = new CarrierShipmentRequest
        {
            Reference = $"{order.OrderNo}-{shipment.Id.ToString("N")[..6]}",
            Service = order.DeliveryMethod == Contracts.Orders.DeliveryMethods.ParcelLocker ? "LOCKER" : "HOME",
            Sender = new CarrierParty { Name = sender.Name, Street = sender.Street, PostalCode = sender.PostalCode, City = sender.City, Country = sender.Country },
            Receiver = receiver,
            LabelFormat = shipment.FromLocationType == "DC" ? "ZPL" : "PDF",
        };
        for (var i = 0; i < Math.Max(parcels, 1); i++)
        {
            request.Parcels.Add(new CarrierParcel { WeightKg = Math.Round(Math.Max(weightKg, 0.2m) / Math.Max(parcels, 1), 2), Reference = $"{i + 1}/{parcels}" });
        }

        try
        {
            return await _api.CreateShipmentAsync(request, _options.ApiKey);
        }
        catch (ApiException ex)
        {
            throw new UpstreamException("Carrier", $"Booking {request.Reference} failed with {(int)ex.StatusCode}: {ex.Content}", ex);
        }
    }

    private static CarrierParty Party(LocationAddress a, Order order) => new()
    {
        Name = a.Name + " / " + order.ShipToName,
        Street = a.Street,
        PostalCode = a.PostalCode,
        City = a.City,
        Country = a.Country,
        Email = order.CustomerEmail,
        Phone = order.CustomerPhone,
    };

    /// <summary>Sender addresses come from configuration ("Locations:{code}"); unknown stores use the DC address.</summary>
    private LocationAddress SenderFor(string locationCode)
    {
        var section = _configuration.GetSection("Locations:" + locationCode);
        var address = section.Exists() ? section.Get<LocationAddress>() : _configuration.GetSection("Locations:DC01").Get<LocationAddress>();
        return address ?? new LocationAddress { Name = "VanArsdel DC", Street = "Logistiikkatie 4", PostalCode = "01510", City = "Vantaa", Country = "FI" };
    }
}
