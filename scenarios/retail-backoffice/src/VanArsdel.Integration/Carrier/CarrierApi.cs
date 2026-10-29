using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Refit;

namespace VanArsdel.Integration.Carrier
{
    /// <summary>NordPost Parcel API v2 (home delivery and parcel lockers in FI, SE and the Baltics).</summary>
    [Headers("Accept: application/json")]
    public interface ICarrierApi
    {
        [Post("/v2/shipments")]
        Task<CarrierShipmentResponse> CreateShipmentAsync([Body] CarrierShipmentRequest request, [Header("X-Api-Key")] string apiKey);

        [Get("/v2/shipments/{shipmentId}")]
        Task<CarrierShipmentResponse> GetShipmentAsync(string shipmentId, [Header("X-Api-Key")] string apiKey);

        [Delete("/v2/shipments/{shipmentId}")]
        Task CancelShipmentAsync(string shipmentId, [Header("X-Api-Key")] string apiKey);

        [Post("/v2/returns")]
        Task<CarrierReturnResponse> CreateReturnAsync([Body] CarrierReturnRequest request, [Header("X-Api-Key")] string apiKey);

        [Get("/v2/pickup-points")]
        Task<List<CarrierPickupPoint>> FindPickupPointsAsync([AliasAs("postalCode")] string postalCode, [AliasAs("country")] string country, [Header("X-Api-Key")] string apiKey);
    }

    public class CarrierShipmentRequest
    {
        [JsonProperty("reference")] public string Reference { get; set; }
        [JsonProperty("service")] public string Service { get; set; }
        [JsonProperty("sender")] public CarrierParty Sender { get; set; }
        [JsonProperty("receiver")] public CarrierParty Receiver { get; set; }
        [JsonProperty("parcels")] public List<CarrierParcel> Parcels { get; set; } = new List<CarrierParcel>();
        [JsonProperty("pickupPointId")] public string PickupPointId { get; set; }
        [JsonProperty("labelFormat")] public string LabelFormat { get; set; } = "PDF";
    }

    public class CarrierParty
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("street")] public string Street { get; set; }
        [JsonProperty("postalCode")] public string PostalCode { get; set; }
        [JsonProperty("city")] public string City { get; set; }
        [JsonProperty("country")] public string Country { get; set; }
        [JsonProperty("email")] public string Email { get; set; }
        [JsonProperty("phone")] public string Phone { get; set; }
    }

    public class CarrierParcel
    {
        [JsonProperty("weightKg")] public decimal WeightKg { get; set; }
        [JsonProperty("reference")] public string Reference { get; set; }
    }

    public class CarrierShipmentResponse
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("trackingNumber")] public string TrackingNumber { get; set; }
        [JsonProperty("trackingUrl")] public string TrackingUrl { get; set; }
        [JsonProperty("labelUrl")] public string LabelUrl { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
    }

    public class CarrierReturnRequest
    {
        [JsonProperty("reference")] public string Reference { get; set; }
        [JsonProperty("sender")] public CarrierParty Sender { get; set; }
        [JsonProperty("receiver")] public CarrierParty Receiver { get; set; }
    }

    public class CarrierReturnResponse
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("trackingNumber")] public string TrackingNumber { get; set; }
        [JsonProperty("qrCodeUrl")] public string QrCodeUrl { get; set; }
        [JsonProperty("labelUrl")] public string LabelUrl { get; set; }
    }

    public class CarrierPickupPoint
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("street")] public string Street { get; set; }
        [JsonProperty("city")] public string City { get; set; }
    }

    /// <summary>Tracking event pushed by the carrier to /webhooks/carrier.</summary>
    public class CarrierTrackingEvent
    {
        [JsonProperty("shipmentId")] public string ShipmentId { get; set; }
        [JsonProperty("reference")] public string Reference { get; set; }
        [JsonProperty("trackingNumber")] public string TrackingNumber { get; set; }
        [JsonProperty("event")] public string Event { get; set; }
        [JsonProperty("timestamp")] public DateTime Timestamp { get; set; }
        [JsonProperty("location")] public string Location { get; set; }
    }

    public static class CarrierEvents
    {
        public const string PickedUp = "PICKED_UP";
        public const string InTransit = "IN_TRANSIT";
        public const string Delivered = "DELIVERED";
        public const string ReadyForPickup = "READY_FOR_PICKUP";
        public const string Returned = "RETURNED_TO_SENDER";
    }
}
