using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Litware.OrderHub.Contracts;
using Newtonsoft.Json;

namespace Litware.OrderHub.Core.Channels
{
    /// <summary>Maps the order webhook payload of the webshop (WooCommerce REST v3 order) to the hub format.</summary>
    public static class WebshopOrderMapper
    {
        public static WebshopOrder Parse(string json)
        {
            var order = JsonConvert.DeserializeObject<WebshopOrder>(json);
            if (order == null || order.Id == 0)
            {
                throw new FormatException("Webshop payload is not an order.");
            }
            return order;
        }

        /// <summary>Only paid orders are sent to the warehouse; the webshop also sends drafts and refunds.</summary>
        public static bool IsFulfillable(WebshopOrder order)
        {
            return order.Status == "processing";
        }

        public static OrderDocument Map(WebshopOrder order)
        {
            var shipping = order.Shipping ?? new WebshopAddress();
            var billing = order.Billing ?? new WebshopAddress();
            bool hasShippingAddress = !string.IsNullOrWhiteSpace(shipping.Address1);
            var ship = hasShippingAddress ? shipping : billing;

            return new OrderDocument
            {
                CustomerName = JoinName(billing.FirstName, billing.LastName),
                CustomerEmail = billing.Email,
                Phone = billing.Phone,
                Currency = order.Currency,
                ShippingAmount = ParseAmount(order.ShippingTotal),
                ShippingMethod = order.ShippingLines?.FirstOrDefault()?.MethodId,
                CustomerReference = order.Number,
                ShipTo = new Address
                {
                    Name = string.IsNullOrWhiteSpace(ship.Company)
                        ? JoinName(ship.FirstName, ship.LastName)
                        : ship.Company + ", " + JoinName(ship.FirstName, ship.LastName),
                    Street = ship.Address1,
                    Street2 = NullIfEmpty(ship.Address2),
                    PostalCode = ship.Postcode,
                    City = ship.City,
                    CountryCode = ship.Country
                },
                Lines = (order.LineItems ?? new List<WebshopLineItem>())
                    .Select(l => new OrderLineDocument
                    {
                        Sku = l.Sku,
                        Description = l.Name,
                        Quantity = l.Quantity,
                        UnitPrice = l.Quantity == 0 ? 0m : Math.Round(ParseAmount(l.Total) / l.Quantity, 2)
                    })
                    .ToList()
            };
        }

        private static decimal ParseAmount(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0m;
            return decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
        }

        private static string JoinName(string first, string last)
        {
            return string.Join(" ", new[] { first, last }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public class WebshopOrder
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("number")] public string Number { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("currency")] public string Currency { get; set; }
        [JsonProperty("date_created_gmt")] public DateTime? CreatedGmt { get; set; }
        [JsonProperty("shipping_total")] public string ShippingTotal { get; set; }
        [JsonProperty("total")] public string Total { get; set; }
        [JsonProperty("billing")] public WebshopAddress Billing { get; set; }
        [JsonProperty("shipping")] public WebshopAddress Shipping { get; set; }
        [JsonProperty("line_items")] public List<WebshopLineItem> LineItems { get; set; }
        [JsonProperty("shipping_lines")] public List<WebshopShippingLine> ShippingLines { get; set; }
    }

    public class WebshopAddress
    {
        [JsonProperty("first_name")] public string FirstName { get; set; }
        [JsonProperty("last_name")] public string LastName { get; set; }
        [JsonProperty("company")] public string Company { get; set; }
        [JsonProperty("address_1")] public string Address1 { get; set; }
        [JsonProperty("address_2")] public string Address2 { get; set; }
        [JsonProperty("city")] public string City { get; set; }
        [JsonProperty("postcode")] public string Postcode { get; set; }
        [JsonProperty("country")] public string Country { get; set; }
        [JsonProperty("email")] public string Email { get; set; }
        [JsonProperty("phone")] public string Phone { get; set; }
    }

    public class WebshopLineItem
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("sku")] public string Sku { get; set; }
        [JsonProperty("quantity")] public int Quantity { get; set; }
        [JsonProperty("total")] public string Total { get; set; }
    }

    public class WebshopShippingLine
    {
        [JsonProperty("method_id")] public string MethodId { get; set; }
    }
}
