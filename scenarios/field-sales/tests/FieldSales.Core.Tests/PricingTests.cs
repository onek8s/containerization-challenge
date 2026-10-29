using System;
using System.Collections.Generic;
using Xunit;

namespace Wingtip.FieldSales.Core.Tests
{
    public class PricingTests
    {
        private static readonly Product Coffee = new Product
        {
            ItemNo = "10021", Name = "Coffee 500 g", Category = "DRY", Unit = "CS", CaseSize = 12, ListPrice = 48.90m, VatRate = 14m,
            MinOrderQty = 1, Active = true
        };

        private static readonly Product Milk = new Product
        {
            ItemNo = "20410", Name = "Milk 1 l", Category = "DAIRY", Unit = "CS", CaseSize = 10, ListPrice = 9.80m, VatRate = 14m,
            MinOrderQty = 2, Active = true
        };

        private static PriceCalculator Calculator()
        {
            return new PriceCalculator(
                new[]
                {
                    new PriceRule { PriceGroup = "A", Category = "", DiscountPercent = 8m },
                    new PriceRule { PriceGroup = "A", Category = "DAIRY", DiscountPercent = 3m },
                    new PriceRule { PriceGroup = "B", Category = "", DiscountPercent = 4m }
                },
                new[] { new Promotion { ItemNo = "10021", PromoPrice = 42.00m, ValidFrom = new DateTime(2017, 11, 6), ValidTo = new DateTime(2017, 11, 19) } });
        }

        [Fact]
        public void Category_rule_wins_over_group_wide_rule()
        {
            Assert.Equal(3m, Calculator().GroupDiscountPercent("A", "DAIRY"));
            Assert.Equal(8m, Calculator().GroupDiscountPercent("a", "DRY"));
            Assert.Equal(0m, Calculator().GroupDiscountPercent("C", "DRY"));
        }

        [Fact]
        public void Group_discount_is_rounded_to_cents()
        {
            Assert.Equal(9.51m, Calculator().UnitPrice(Milk, "A", new DateTime(2017, 11, 2)));
            Assert.Equal(46.94m, Calculator().UnitPrice(Coffee, "B", new DateTime(2017, 11, 2)));
        }

        [Fact]
        public void Promotion_is_used_when_lower_and_valid_on_delivery_date()
        {
            Assert.Equal(42.00m, Calculator().UnitPrice(Coffee, "B", new DateTime(2017, 11, 19)));
            Assert.Equal(46.94m, Calculator().UnitPrice(Coffee, "B", new DateTime(2017, 11, 20)));
        }

        [Fact]
        public void Order_totals_and_vat()
        {
            var order = new SalesOrder();
            order.Lines.Add(new OrderLine { ItemNo = "10021", Quantity = 3, UnitPrice = 44.99m, LineDiscountPercent = 5m, VatRate = 14m });
            order.Lines.Add(new OrderLine { ItemNo = "30001", Quantity = 2, UnitPrice = 12.50m, VatRate = 24m });

            Assert.Equal(128.22m + 25.00m, order.NetTotal);
            Assert.Equal(17.95m + 6.00m, order.VatTotal);
            Assert.Equal(177.17m, order.GrossTotal);
            Assert.Equal(4.22m, order.ManualDiscountPercent);
        }

        [Fact]
        public void Price_check_reports_changed_lines()
        {
            var order = new SalesOrder { RequestedDelivery = new DateTime(2017, 11, 2) };
            order.Lines.Add(new OrderLine { ItemNo = "10021", Quantity = 1, UnitPrice = 46.94m });
            order.Lines.Add(new OrderLine { ItemNo = "20410", Quantity = 2, UnitPrice = 9.20m });
            var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase) { { "10021", Coffee }, { "20410", Milk } };

            var differences = PriceCheck.Compare(order, new Customer { PriceGroup = "B" }, products, Calculator());

            var difference = Assert.Single(differences);
            Assert.Equal("20410", difference.ItemNo);
            Assert.Equal(9.41m, difference.CurrentPrice);
        }

        [Theory]
        [InlineData(5, 5, DiscountDecision.WithinLimit)]
        [InlineData(6, 5, DiscountDecision.NeedsApproval)]
        [InlineData(25, 15, DiscountDecision.NeedsApproval)]
        [InlineData(26, 100, DiscountDecision.NotAllowed)]
        public void Discount_policy(decimal lineDiscount, decimal limit, DiscountDecision expected)
        {
            var order = new SalesOrder();
            order.Lines.Add(new OrderLine { ItemNo = "1", Quantity = 1, UnitPrice = 10m });
            order.Lines.Add(new OrderLine { ItemNo = "2", Quantity = 1, UnitPrice = 10m, LineDiscountPercent = lineDiscount });

            Assert.Equal(expected, new DiscountPolicy().Evaluate(order, limit));
        }
    }
}
