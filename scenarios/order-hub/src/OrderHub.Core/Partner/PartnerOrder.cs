using System;
using System.Collections.Generic;
using System.Linq;
using Litware.OrderHub.Contracts;
using Newtonsoft.Json;

namespace Litware.OrderHub.Core.Partner
{
    /// <summary>Drop-ship purchase order sent by Contoso Retail to the inbound Service Bus queue.</summary>
    public class PartnerOrder
    {
        [JsonProperty("poNumber")] public string PoNumber { get; set; }
        [JsonProperty("storeId")] public string StoreId { get; set; }
        [JsonProperty("currency")] public string Currency { get; set; }
        [JsonProperty("consumer")] public PartnerConsumer Consumer { get; set; }
        [JsonProperty("lines")] public List<PartnerOrderLine> Lines { get; set; }

        public static PartnerOrder Parse(string json)
        {
            var order = JsonConvert.DeserializeObject<PartnerOrder>(json);
            if (order == null || string.IsNullOrWhiteSpace(order.PoNumber))
            {
                throw new FormatException("Partner message has no poNumber.");
            }
            return order;
        }

        public OrderDocument ToDocument()
        {
            var consumer = Consumer ?? new PartnerConsumer();
            return new OrderDocument
            {
                CustomerName = consumer.Name,
                CustomerEmail = consumer.Email,
                Phone = consumer.Phone,
                Currency = Currency ?? "EUR",
                ShippingMethod = "dropship",
                CustomerReference = PoNumber + (string.IsNullOrEmpty(StoreId) ? "" : "/" + StoreId),
                ShipTo = new Address
                {
                    Name = consumer.Name,
                    Street = consumer.Street,
                    PostalCode = consumer.Zip,
                    City = consumer.City,
                    CountryCode = consumer.Country
                },
                Lines = (Lines ?? new List<PartnerOrderLine>()).Select(l => new OrderLineDocument
                {
                    Sku = l.ArticleNo,
                    Description = l.Description,
                    Quantity = l.Qty,
                    UnitPrice = l.NetPrice
                }).ToList()
            };
        }
    }

    public class PartnerConsumer
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("email")] public string Email { get; set; }
        [JsonProperty("phone")] public string Phone { get; set; }
        [JsonProperty("street")] public string Street { get; set; }
        [JsonProperty("zip")] public string Zip { get; set; }
        [JsonProperty("city")] public string City { get; set; }
        [JsonProperty("country")] public string Country { get; set; }
    }

    public class PartnerOrderLine
    {
        [JsonProperty("articleNo")] public string ArticleNo { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("qty")] public int Qty { get; set; }
        [JsonProperty("netPrice")] public decimal NetPrice { get; set; }
    }
}
