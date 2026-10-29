using System;
using System.Linq;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.LabStubs;
using Tailspin.DealerOrders.Processing.Catalog;
using Xunit;

namespace Tailspin.DealerOrders.Tests
{
    public class PricingTests
    {
        private static readonly DateTime Today = new DateTime(2019, 10, 15);
        private readonly FileCatalog _catalog = new FileCatalog(Lab.CatalogFolder);
        private readonly InMemoryStores _stores = InMemoryStores.WithLabData();
        private PriceCalculator Calculator => new PriceCalculator(_stores);

        private Dealer Dealer(string account) => _stores.All().Single(d => d.AccountNumber == account);

        [Fact]
        public void Tier_discount_and_campaign_compound_for_finnish_dealer()
        {
            var model = _catalog.Get("TF14");
            var config = new ConfigurationEngine().Resolve(model, new[] { "CRANE-11M", "BOGIE-HD" }, "FI", Today).Configuration;
            var price = Calculator.Calculate(model, config, Dealer("D-10010"), 1, 0, "FI", Today);

            var machine = price.Lines.First();
            Assert.Equal(268000m, machine.ListPrice);
            // Tier A 18 % and the 3 % autumn campaign on top: 1 - 0.82 * 0.97 = 20.46 %.
            Assert.Equal(20.46m, machine.DiscountPercent);
            Assert.Equal(213167.20m, machine.NetPrice);
            var crane = price.Lines.Single(l => l.Code == "CRANE-11M");
            Assert.Equal(15m, crane.DiscountPercent);
            Assert.Equal(1200m, price.Freight);
            Assert.Empty(price.Problems);
        }

        [Fact]
        public void Swedish_dealer_gets_own_price_list_in_whole_crowns()
        {
            var model = _catalog.Get("TF10");
            var config = new MachineConfiguration("TF10", new[] { "CRANE-10M", "GRAPPLE-028" });
            var price = Calculator.Calculate(model, config, Dealer("D-10022"), 1, 0, "SE", Today);
            Assert.Equal("SEK", price.Currency);
            Assert.Equal(2074300m, price.Lines.First().ListPrice);
            Assert.All(price.Lines, l => Assert.Equal(Math.Round(l.NetPrice, 0), l.NetPrice));
            Assert.Equal(Math.Round(2400m * 10.45m, 0), price.Freight);
        }

        [Fact]
        public void Canadian_dealer_is_priced_from_eur_with_exchange_rate_and_no_freight_for_fca()
        {
            var model = _catalog.Get("TF10");
            var config = new ConfigurationEngine().Resolve(model, new[] { "ENG-T4F" }, "CA", Today).Configuration;
            var price = Calculator.Calculate(model, config, Dealer("D-20011"), 2, 0, "CA", Today);
            var machine = price.Lines.First();
            Assert.Equal(Math.Round(198500m * 1.49m, 2), machine.ListPrice);
            Assert.Contains("converted from EUR", machine.Note);
            Assert.Equal(0m, price.Freight);
            Assert.Equal(2, price.Quantity);
        }

        [Fact]
        public void Extra_discount_is_taken_from_the_net_total()
        {
            var model = _catalog.Get("TL450");
            var config = new MachineConfiguration("TL450", new[] { "BUCKET-GP" });
            var price = Calculator.Calculate(model, config, Dealer("D-10045"), 1, 5m, "DE", Today);
            Assert.Equal(Math.Round(price.NetBeforeExtra * 0.05m, 2), price.ExtraDiscountAmount);
            Assert.Equal(price.NetBeforeExtra - price.ExtraDiscountAmount, price.NetTotal);
            Assert.Equal(Math.Round(4800m * 0.3m, 0), price.Freight);
        }

        [Fact]
        public void Parts_use_volume_price_and_parts_discount()
        {
            var line = Calculator.PricePart("P-10045", "Hydraulic filter element", Dealer("D-10010"), 24, Today);
            Assert.Equal(55.00m, line.ListPrice);
            Assert.Equal(25m, line.DiscountPercent);
            Assert.Equal(41.25m, line.NetPrice);
        }

        [Fact]
        public void Missing_price_is_reported_not_thrown()
        {
            var line = Calculator.PricePart("P-NOPE", "x", Dealer("D-10010"), 1, Today);
            Assert.Equal("No price for P-NOPE in EUR.", line.Note);
            Assert.Equal(0m, line.ListPrice);
        }

        [Fact]
        public void Money_refuses_mixed_currencies_and_rounds_per_currency()
        {
            Assert.Throws<InvalidOperationException>(() => new Money(1, "EUR") + new Money(1, "SEK"));
            Assert.Equal(1235m, new Money(1234.5m, "SEK").Round().Amount);
            Assert.Equal(1234.57m, new Money(1234.567m, "EUR").Round().Amount);
        }
    }
}
