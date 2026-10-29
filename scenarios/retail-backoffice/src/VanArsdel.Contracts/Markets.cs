using System.Collections.Generic;

namespace VanArsdel.Contracts
{
    public static class Markets
    {
        public static readonly IReadOnlyDictionary<string, string> Currencies = new Dictionary<string, string>
        {
            ["FI"] = "EUR",
            ["SE"] = "SEK",
            ["EE"] = "EUR",
            ["LV"] = "EUR",
            ["LT"] = "EUR",
        };

        public static readonly IReadOnlyDictionary<string, decimal> StandardVat = new Dictionary<string, decimal>
        {
            ["FI"] = 0.24m,
            ["SE"] = 0.25m,
            ["EE"] = 0.20m,
            ["LV"] = 0.21m,
            ["LT"] = 0.21m,
        };

        public static string CurrencyOf(string market) =>
            market != null && Currencies.TryGetValue(market.ToUpperInvariant(), out var c) ? c : "EUR";
    }

    public static class Scopes
    {
        public const string Catalog = "retail.catalog";
        public const string Pricing = "retail.pricing";
        public const string Inventory = "retail.inventory";
        public const string Orders = "retail.orders";
        public const string Customers = "retail.customers";
        public const string Returns = "retail.returns";
        public const string StoreOps = "retail.storeops";
        public const string Reporting = "retail.reporting";
    }

    public static class Roles
    {
        public const string Admin = "Retail.Admin";
        public const string Merchandiser = "Retail.Merchandiser";
        public const string CustomerService = "Retail.CustomerService";
        public const string StoreManager = "Retail.StoreManager";
        public const string StoreStaff = "Retail.StoreStaff";
        public const string Finance = "Retail.Finance";
        public const string Service = "Retail.Service";
    }
}
