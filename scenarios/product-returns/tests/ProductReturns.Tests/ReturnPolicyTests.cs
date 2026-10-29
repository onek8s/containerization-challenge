using System;
using System.Collections.Generic;
using System.Linq;
using ProductReturns.Core;
using Xunit;

namespace ProductReturns.Tests
{
    public class ReturnPolicyTests
    {
        private static readonly Dictionary<string, CatalogItem> Catalog = new[]
        {
            new CatalogItem { Sku = "E1", Category = "Electronics", Description = "Headphones" },
            new CatalogItem { Sku = "H1", Category = "Home", Description = "Mugs" },
            new CatalogItem { Sku = "F1", Category = "Food", Description = "Coffee" },
            new CatalogItem { Sku = "C1", Category = "Home", Description = "Lamp", FinalSale = true },
            new CatalogItem { Sku = "FU", Category = "Furniture", Description = "Table" }
        }.ToDictionary(c => c.Sku);

        private static SalesOrder Order(DateTime? delivered, params (int Line, string Sku, int Qty)[] lines) => new SalesOrder
        {
            OrderNumber = "SO-1",
            DeliveredDate = delivered,
            Lines = lines.Select(l => new SalesOrderLine { LineNumber = l.Line, Sku = l.Sku, Quantity = l.Qty, UnitPrice = 10m }).ToList()
        };

        private static IList<ReturnableLine> Evaluate(SalesOrder order, DateTime today, IDictionary<int, int> returned = null) =>
            new ReturnPolicy(new ReturnPolicyOptions()).Evaluate(order, s => Catalog.TryGetValue(s, out var c) ? c : null, returned, today);

        [Fact]
        public void Default_window_is_30_days_from_delivery()
        {
            var lines = Evaluate(Order(new DateTime(2024, 3, 1), (1, "H1", 1)), new DateTime(2024, 3, 31));
            Assert.Equal(1, lines[0].ReturnableQuantity);
            Assert.Equal(new DateTime(2024, 3, 31), lines[0].ReturnByDate);

            lines = Evaluate(Order(new DateTime(2024, 3, 1), (1, "H1", 1)), new DateTime(2024, 4, 1));
            Assert.Equal(0, lines[0].ReturnableQuantity);
            Assert.StartsWith("Return period ended", lines[0].NotReturnableReason);
        }

        [Fact]
        public void Category_windows_override_the_default()
        {
            var lines = Evaluate(Order(new DateTime(2024, 3, 1), (1, "E1", 1), (2, "FU", 1)), new DateTime(2024, 3, 20));
            Assert.False(lines[0].IsReturnable);
            Assert.True(lines[1].IsReturnable);
            Assert.Equal(new DateTime(2024, 4, 30), lines[1].ReturnByDate);
        }

        [Fact]
        public void Holiday_deliveries_can_be_returned_until_end_of_january()
        {
            var lines = Evaluate(Order(new DateTime(2023, 12, 10), (1, "E1", 1)), new DateTime(2024, 1, 20));
            Assert.Equal(new DateTime(2024, 1, 31), lines[0].ReturnByDate);
            Assert.True(lines[0].IsReturnable);
        }

        [Fact]
        public void Non_returnable_lines_have_a_reason()
        {
            var lines = Evaluate(Order(new DateTime(2024, 3, 1), (1, "F1", 1), (2, "C1", 1), (3, "XX", 1)), new DateTime(2024, 3, 2));
            Assert.Equal("This product cannot be returned", lines[0].NotReturnableReason);
            Assert.Equal("Final sale", lines[1].NotReturnableReason);
            Assert.Equal("Unknown product", lines[2].NotReturnableReason);
        }

        [Fact]
        public void Undelivered_orders_cannot_be_returned()
        {
            var lines = Evaluate(Order(null, (1, "H1", 1)), new DateTime(2024, 3, 2));
            Assert.Equal("Not delivered yet", lines[0].NotReturnableReason);
        }

        [Fact]
        public void Already_returned_quantity_is_subtracted()
        {
            var order = Order(new DateTime(2024, 3, 1), (1, "H1", 3));
            var lines = Evaluate(order, new DateTime(2024, 3, 2), new Dictionary<int, int> { [1] = 2 });
            Assert.Equal(1, lines[0].ReturnableQuantity);

            lines = Evaluate(order, new DateTime(2024, 3, 2), new Dictionary<int, int> { [1] = 3 });
            Assert.Equal(0, lines[0].ReturnableQuantity);
            Assert.Equal("Already returned", lines[0].NotReturnableReason);
        }
    }
}
