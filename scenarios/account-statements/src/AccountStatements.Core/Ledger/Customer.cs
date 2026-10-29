namespace Woodgrove.Statements.Core.Ledger
{
    public enum StatementDelivery
    {
        Email = 1,
        Share = 2,
        Post = 3,
        None = 9
    }

    public class Customer
    {
        public int CustomerId { get; set; }
        public string CustomerNo { get; set; }
        public string Name { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string CountryCode { get; set; }
        public string Email { get; set; }
        public string Language { get; set; }
        public string CurrencyCode { get; set; }
        public StatementDelivery Delivery { get; set; }
        public string DropFolder { get; set; }
        public string CreditController { get; set; }
        public decimal CreditLimit { get; set; }
        public bool OnHold { get; set; }

        public string[] AddressLines()
        {
            var lines = new System.Collections.Generic.List<string>();
            lines.Add(Name);
            if (!string.IsNullOrWhiteSpace(AddressLine1)) lines.Add(AddressLine1);
            if (!string.IsNullOrWhiteSpace(AddressLine2)) lines.Add(AddressLine2);
            lines.Add(((PostalCode ?? "") + " " + (City ?? "")).Trim());
            if (!string.IsNullOrWhiteSpace(CountryCode) && CountryCode != "FI") lines.Add(CountryCode);
            return lines.ToArray();
        }
    }
}
