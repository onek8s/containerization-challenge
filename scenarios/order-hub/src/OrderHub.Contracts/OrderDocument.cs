using System.Collections.Generic;

namespace Litware.OrderHub.Contracts
{
    /// <summary>Hub order format shared by all channels.</summary>
    public class OrderDocument
    {
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string Phone { get; set; }
        public Address ShipTo { get; set; }
        public string Currency { get; set; }
        public decimal ShippingAmount { get; set; }
        public string ShippingMethod { get; set; }
        public string CustomerReference { get; set; }
        public List<OrderLineDocument> Lines { get; set; } = new List<OrderLineDocument>();
    }

    public class Address
    {
        public string Name { get; set; }
        public string Street { get; set; }
        public string Street2 { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string CountryCode { get; set; }
    }

    public class OrderLineDocument
    {
        public string Sku { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
