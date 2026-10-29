using System;
using System.Collections.Generic;
using ProductReturns.Core;

namespace ProductReturns.Data
{
    /// <summary>Sample ERP data for the local development database.</summary>
    public static class SampleData
    {
        public const string DevelopmentScannerKey = "dev-scanner-0001";

        public static void Load(ReturnsDbContext db)
        {
            db.Catalog.AddRange(new List<CatalogItem>
            {
                new CatalogItem { Sku = "WWI-10021", Description = "Espresso machine Barista Pro", Category = "Appliances", WeightKg = 9.2m },
                new CatalogItem { Sku = "WWI-10544", Description = "Noise cancelling headphones NC700", Category = "Electronics", WeightKg = 0.4m },
                new CatalogItem { Sku = "WWI-20310", Description = "Oak side table", Category = "Furniture", WeightKg = 12.5m },
                new CatalogItem { Sku = "WWI-30007", Description = "Linen bed sheet set, king", Category = "Home", WeightKg = 1.6m },
                new CatalogItem { Sku = "WWI-30114", Description = "Ceramic mug set (4)", Category = "Home", WeightKg = 1.9m },
                new CatalogItem { Sku = "WWI-40002", Description = "Gift card 50 EUR", Category = "GiftCards", WeightKg = 0m },
                new CatalogItem { Sku = "WWI-50090", Description = "Coffee beans, dark roast 1 kg", Category = "Food", WeightKg = 1m },
                new CatalogItem { Sku = "WWI-60033", Description = "Desk lamp, clearance", Category = "Home", FinalSale = true, WeightKg = 1.1m }
            });

            var today = DateTime.Today;
            db.Orders.AddRange(new List<SalesOrder>
            {
                Order("SO-1048213", "C-20017", "Anna de Vries", "anna.devries@example.com", "Keizersgracht 120", "1015 CX", "Amsterdam", "NL",
                    today.AddDays(-12), today.AddDays(-9),
                    Line(1, "WWI-10544", "Noise cancelling headphones NC700", 1, 249.00m),
                    Line(2, "WWI-30114", "Ceramic mug set (4)", 2, 24.95m),
                    Line(3, "WWI-50090", "Coffee beans, dark roast 1 kg", 3, 17.50m)),
                Order("SO-1048377", "C-31002", "Jonas Peeters", "jonas.peeters@example.com", "Meir 45", "2000", "Antwerpen", "BE",
                    today.AddDays(-25), today.AddDays(-20),
                    Line(1, "WWI-20310", "Oak side table", 1, 189.00m),
                    Line(2, "WWI-30007", "Linen bed sheet set, king", 1, 89.90m),
                    Line(3, "WWI-60033", "Desk lamp, clearance", 1, 19.99m)),
                Order("SO-1047002", "C-20017", "Anna de Vries", "anna.devries@example.com", "Keizersgracht 120", "1015 CX", "Amsterdam", "NL",
                    today.AddDays(-75), today.AddDays(-70),
                    Line(1, "WWI-10021", "Espresso machine Barista Pro", 1, 649.00m)),
                Order("SO-1049120", "C-40555", "Lukas Schmidt", "lukas.schmidt@example.com", "Hohe Str. 9", "50667", "Koeln", "DE",
                    today.AddDays(-2), null,
                    Line(1, "WWI-10021", "Espresso machine Barista Pro", 1, 649.00m),
                    Line(2, "WWI-40002", "Gift card 50 EUR", 1, 50.00m))
            });

            db.Scanners.Add(new ScannerDevice
            {
                Name = "DEV-SCANNER-01",
                Location = "Development",
                ApiKeyHash = ApiKeys.Hash(DevelopmentScannerKey),
                Enabled = true
            });

            db.SaveChanges();
        }

        private static SalesOrder Order(string number, string customer, string name, string email, string street, string postalCode,
            string city, string country, DateTime orderDate, DateTime? delivered, params SalesOrderLine[] lines)
        {
            var order = new SalesOrder
            {
                OrderNumber = number,
                CustomerNumber = customer,
                CustomerName = name,
                CustomerEmail = email,
                ShipToName = name,
                ShipToStreet = street,
                ShipToPostalCode = postalCode,
                ShipToCity = city,
                ShipToCountry = country,
                OrderDate = orderDate,
                DeliveredDate = delivered,
                Currency = "EUR"
            };

            foreach (var line in lines)
            {
                line.OrderNumber = number;
                order.Lines.Add(line);
            }

            return order;
        }

        private static SalesOrderLine Line(int number, string sku, string description, int quantity, decimal price) =>
            new SalesOrderLine { LineNumber = number, Sku = sku, Description = description, Quantity = quantity, UnitPrice = price };
    }
}
