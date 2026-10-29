using System.Collections.Generic;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core;
using Litware.OrderHub.Tests.Fakes;
using Xunit;

namespace Litware.OrderHub.Tests
{
    public class ValidatorTests
    {
        private readonly OrderValidator _validator = new OrderValidator(new[] { "FI", "SE", "DE" }, new[] { "EUR", "SEK" });
        private readonly InMemoryOrderStore _store = InMemoryOrderStore.WithProducts();

        private Product Find(string sku) => _store.Products.TryGetValue(sku, out var p) ? p : null;

        private static OrderDocument Order(string country = "FI", string currency = "EUR", params OrderLineDocument[] lines)
        {
            return new OrderDocument
            {
                Currency = currency,
                ShipTo = new Address { Name = "Test", Street = "Street 1", PostalCode = "00100", City = "Helsinki", CountryCode = country },
                Lines = new List<OrderLineDocument>(lines.Length == 0 ? new[] { new OrderLineDocument { Sku = "LW-HDL-300", Quantity = 1, UnitPrice = 29.9m } } : lines)
            };
        }

        [Fact]
        public void Valid_order_has_no_problems()
        {
            Assert.Empty(_validator.Validate(Order(), Find));
        }

        [Fact]
        public void Unserved_country_and_currency_are_reported()
        {
            var problems = _validator.Validate(Order("NO", "NOK"), Find);
            Assert.Equal(2, problems.Count);
        }

        [Fact]
        public void Unknown_and_discontinued_skus_are_reported()
        {
            var problems = _validator.Validate(Order(lines: new[]
            {
                new OrderLineDocument { Sku = "NOPE", Quantity = 1 },
                new OrderLineDocument { Sku = "LW-PK-40-RED", Quantity = 1 },
                new OrderLineDocument { Sku = "LW-HDL-300", Quantity = 0 }
            }), Find);
            Assert.Contains("Unknown SKU NOPE.", problems);
            Assert.Contains("SKU LW-PK-40-RED is discontinued.", problems);
            Assert.Contains("SKU LW-HDL-300: quantity 0 is out of range.", problems);
        }
    }
}
