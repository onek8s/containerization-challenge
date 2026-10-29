namespace Contoso.Purchasing.Core
{
    public class Supplier
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string VatNumber { get; set; }
        public string Email { get; set; }
        public string ErpVendorNo { get; set; }
        public bool Active { get; set; }
    }
}
