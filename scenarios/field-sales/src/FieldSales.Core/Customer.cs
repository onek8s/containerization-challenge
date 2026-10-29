namespace Wingtip.FieldSales.Core
{
    public class Customer
    {
        public string CustomerNo { get; set; }
        public string Name { get; set; }
        public string Street { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string Email { get; set; }
        public string PriceGroup { get; set; }
        public string RepCode { get; set; }
        public DeliveryDays DeliveryDays { get; set; }
        public bool CreditBlocked { get; set; }
    }
}
