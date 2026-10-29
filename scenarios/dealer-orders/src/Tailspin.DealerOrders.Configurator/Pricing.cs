using System;
using System.Collections.Generic;
using System.Linq;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.Configurator
{
    /// <summary>Source of trade agreement prices, discounts, campaigns, freight and exchange rates (SQL cache or test data).</summary>
    public interface IPriceSource
    {
        IEnumerable<PriceEntry> PricesFor(string itemId);
        IEnumerable<DiscountEntry> Discounts();
        IEnumerable<Campaign> Campaigns(DateTime date);
        IEnumerable<FreightRate> FreightRates(string countryCode);
        IEnumerable<ExchangeRate> ExchangeRates();
    }

    public class PriceLine
    {
        public string Code { get; set; }
        public string ItemId { get; set; }
        public string Description { get; set; }
        public decimal ListPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal NetPrice { get; set; }
        public string Note { get; set; }
    }

    public class PriceBreakdown
    {
        public string Currency { get; set; }
        public decimal Quantity { get; set; }
        public List<PriceLine> Lines { get; } = new List<PriceLine>();
        public decimal ListTotal => Lines.Sum(l => l.ListPrice) * Quantity;
        public decimal NetBeforeExtra => Lines.Sum(l => l.NetPrice) * Quantity;
        public decimal ExtraDiscountPercent { get; set; }
        public decimal ExtraDiscountAmount { get; set; }
        public decimal NetTotal => NetBeforeExtra - ExtraDiscountAmount;
        public decimal DiscountTotal => ListTotal - NetTotal;
        public decimal Freight { get; set; }

        /// <summary>Net unit price of the configured machine after all discounts, as sent to AX.</summary>
        public decimal UnitNet => Quantity == 0 ? 0 : NetTotal / Quantity;

        /// <summary>Overall discount in percent of list, as sent to AX (SalesLine.LinePercent).</summary>
        public decimal EffectiveDiscountPercent => ListTotal == 0 ? 0 : Math.Round((ListTotal - NetTotal) / ListTotal * 100m, 4);

        public List<string> Problems { get; } = new List<string>();
    }

    /// <summary>
    /// Builds the dealer price of a configuration: list price per dealer price group and currency (falling back to the
    /// EUR list converted with the AX exchange rate), tier discount per discount group, campaigns, the extra discount
    /// requested on the quote and freight for CPT/DAP delivery terms.
    /// </summary>
    public class PriceCalculator
    {
        public const string BaseDiscountGroup = "MACHINE";
        public const string DefaultPriceGroup = "LIST";

        private readonly IPriceSource _source;

        public PriceCalculator(IPriceSource source)
        {
            _source = source;
        }

        public PriceBreakdown Calculate(ProductModel model, MachineConfiguration configuration, Dealer dealer, decimal quantity,
            decimal extraDiscountPercent, string deliveryCountry, DateTime date)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            var currency = dealer.Currency;
            var result = new PriceBreakdown { Currency = currency, Quantity = quantity, ExtraDiscountPercent = extraDiscountPercent };
            var discounts = _source.Discounts().Where(d => d.Tier == dealer.DiscountTier).ToList();
            var campaigns = _source.Campaigns(date).ToList();
            var country = deliveryCountry ?? dealer.CountryCode;

            result.Lines.Add(BuildLine(model.ModelCode, model.ItemId, model.Name, BaseDiscountGroup, null, model, dealer, quantity, discounts, campaigns, country, date, result));

            foreach (var code in configuration.Options)
            {
                var option = model.FindOption(code);
                if (option == null) continue;
                if (string.IsNullOrEmpty(option.ItemId))
                {
                    result.Lines.Add(new PriceLine { Code = option.Code, Description = option.Name, Note = "included" });
                    continue;
                }
                result.Lines.Add(BuildLine(option.Code, option.ItemId, option.Name, option.DiscountGroup ?? "OPTION", option.Code, model, dealer, quantity, discounts, campaigns, country, date, result));
            }

            if (extraDiscountPercent < 0 || extraDiscountPercent > 30)
                result.Problems.Add("Extra discount must be between 0 and 30 percent.");
            result.ExtraDiscountAmount = Round(result.NetBeforeExtra * extraDiscountPercent / 100m, currency);
            result.Freight = Round(FreightFor(model.Family, country, dealer.DeliveryTerms, currency, date, result) * quantity, currency);
            return result;
        }

        /// <summary>Price of a spare part or attachment line (no configuration).</summary>
        public PriceLine PricePart(string itemId, string description, Dealer dealer, decimal quantity, DateTime date)
        {
            var discounts = _source.Discounts().Where(d => d.Tier == dealer.DiscountTier).ToList();
            var dummy = new PriceBreakdown { Currency = dealer.Currency, Quantity = quantity };
            var line = BuildLine(itemId, itemId, description, "PARTS", null, null, dealer, quantity, discounts, Enumerable.Empty<Campaign>().ToList(), dealer.CountryCode, date, dummy);
            if (dummy.Problems.Count > 0) line.Note = string.Join(" ", dummy.Problems);
            return line;
        }

        private PriceLine BuildLine(string code, string itemId, string description, string discountGroup, string optionCode, ProductModel model,
            Dealer dealer, decimal quantity, List<DiscountEntry> discounts, List<Campaign> campaigns, string country, DateTime date, PriceBreakdown result)
        {
            var list = ListPrice(itemId, dealer.PriceGroup, dealer.Currency, quantity, date, out var note);
            if (list == null)
            {
                result.Problems.Add($"No price for {itemId} in {dealer.Currency}.");
                return new PriceLine { Code = code, ItemId = itemId, Description = description, Note = "no price" };
            }

            var tier = discounts.FirstOrDefault(d => string.Equals(d.DiscountGroup, discountGroup, StringComparison.OrdinalIgnoreCase))?.Percent ?? 0m;
            var campaign = model == null ? null : campaigns.Where(c => c.Applies(model.ModelCode, optionCode, country, date)).OrderByDescending(c => c.Percent).FirstOrDefault();
            var percent = tier;
            if (campaign != null)
            {
                // Campaign discount is applied on top of the tier discount, not added to it.
                percent = 100m - (100m - tier) * (100m - campaign.Percent) / 100m;
                note = (note == null ? "" : note + "; ") + campaign.Name;
            }

            return new PriceLine
            {
                Code = code,
                ItemId = itemId,
                Description = description,
                ListPrice = list.Value,
                DiscountPercent = Math.Round(percent, 4),
                NetPrice = Round(list.Value * (100m - percent) / 100m, dealer.Currency),
                Note = note
            };
        }

        private decimal? ListPrice(string itemId, string priceGroup, string currency, decimal quantity, DateTime date, out string note)
        {
            note = null;
            var prices = _source.PricesFor(itemId).Where(p => p.IsValidOn(date) && p.QuantityFrom <= quantity).ToList();

            var exact = Best(prices, priceGroup, currency) ?? Best(prices, DefaultPriceGroup, currency);
            if (exact != null) return exact.Amount;

            var euro = Best(prices, priceGroup, Currencies.Euro) ?? Best(prices, DefaultPriceGroup, Currencies.Euro);
            if (euro == null) return null;
            var rate = Rate(Currencies.Euro, currency, date);
            if (rate == null) return null;
            note = $"converted from EUR at {rate.Value:0.####}";
            return Round(euro.Amount * rate.Value, currency);
        }

        private static PriceEntry Best(IEnumerable<PriceEntry> prices, string group, string currency) =>
            prices.Where(p => string.Equals(p.PriceGroup, group, StringComparison.OrdinalIgnoreCase) && string.Equals(p.Currency, currency, StringComparison.OrdinalIgnoreCase))
                  .OrderByDescending(p => p.QuantityFrom).ThenByDescending(p => p.FromDate).FirstOrDefault();

        public decimal? Rate(string from, string to, DateTime date)
        {
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return 1m;
            var rates = _source.ExchangeRates().Where(r => r.ValidFrom.Date <= date.Date).ToList();
            var direct = rates.Where(r => r.FromCurrency == from && r.ToCurrency == to).OrderByDescending(r => r.ValidFrom).FirstOrDefault();
            if (direct != null) return direct.Rate;
            var inverse = rates.Where(r => r.FromCurrency == to && r.ToCurrency == from).OrderByDescending(r => r.ValidFrom).FirstOrDefault();
            if (inverse != null && inverse.Rate != 0) return Math.Round(1m / inverse.Rate, 6);
            return null;
        }

        private decimal FreightFor(ProductFamily family, string country, DeliveryTerms terms, string currency, DateTime date, PriceBreakdown result)
        {
            if (terms == DeliveryTerms.Exw || terms == DeliveryTerms.Fca) return 0m;
            var rate = _source.FreightRates(country).Where(f => f.Family == family).OrderByDescending(f => f.AmountEur).FirstOrDefault();
            if (rate == null)
            {
                result.Problems.Add($"No freight rate to {country}; the sales desk will add freight.");
                return 0m;
            }
            var fx = Rate(Currencies.Euro, currency, date);
            if (fx == null)
            {
                result.Problems.Add($"No exchange rate EUR/{currency}.");
                return 0m;
            }
            return rate.AmountEur * fx.Value;
        }

        private static decimal Round(decimal amount, string currency) =>
            Math.Round(amount, Currencies.Decimals(currency), MidpointRounding.AwayFromZero);
    }
}
