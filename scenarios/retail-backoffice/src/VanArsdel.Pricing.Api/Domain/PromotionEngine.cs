using System;
using System.Collections.Generic;
using System.Linq;

namespace VanArsdel.Pricing.Api.Domain
{
    public class BasketLine
    {
        public string Sku { get; set; }
        public string StyleCode { get; set; }
        public string CategoryCode { get; set; }
        public string Brand { get; set; }
        public string Season { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? CompareAtPrice { get; set; }

        public decimal Discount { get; set; }
        public List<string> Promotions { get; } = new List<string>();
        public bool Locked { get; set; }

        public decimal Gross => UnitPrice * Quantity;
        public decimal Net => Gross - Discount;
    }

    public class BasketResult
    {
        public string Market { get; set; }
        public string Currency { get; set; }
        public List<BasketLine> Lines { get; set; } = new List<BasketLine>();
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal Total { get; set; }
        public decimal VatAmount { get; set; }
        public List<string> AppliedPromotions { get; set; } = new List<string>();
        public List<string> Messages { get; set; } = new List<string>();
    }

    /// <summary>
    /// Applies promotions to a basket. Order: markdowns (price level) first, then line promotions by
    /// priority, then basket amount-off. An exclusive promotion locks the lines it touched.
    /// The same engine prices web shop baskets and is mirrored by the POS promotion export.
    /// </summary>
    public class PromotionEngine
    {
        public BasketResult Apply(string market, string currency, decimal vatRate, IList<BasketLine> lines,
            IEnumerable<Promotion> candidates, DateTime nowUtc, string channel, string enteredCode)
        {
            var active = candidates.Where(p => p.IsActive(nowUtc, market, channel, enteredCode))
                .OrderBy(p => p.Type == PromotionTypes.Markdown ? 0 : p.Type == PromotionTypes.AmountOff ? 2 : 1)
                .ThenByDescending(p => p.Priority)
                .ThenBy(p => p.Id)
                .ToList();

            var result = new BasketResult { Market = market, Currency = currency, Lines = lines.ToList() };

            foreach (var promotion in active)
            {
                var inScope = lines.Where(l => !l.Locked && l.Quantity > 0 && promotion.Covers(l)).ToList();
                if (inScope.Count == 0) continue;

                decimal discount;
                switch (promotion.Type)
                {
                    case PromotionTypes.Markdown:
                    case PromotionTypes.PercentOff:
                        discount = ApplyPercent(promotion, inScope);
                        break;
                    case PromotionTypes.BuyXPayY:
                        discount = ApplyBuyXPayY(promotion, inScope);
                        break;
                    case PromotionTypes.Bundle:
                        discount = ApplyBundle(promotion, inScope);
                        break;
                    case PromotionTypes.AmountOff:
                        discount = ApplyAmountOff(promotion, inScope, result.Messages);
                        break;
                    default:
                        discount = 0;
                        break;
                }

                if (discount > 0)
                {
                    result.AppliedPromotions.Add(promotion.Code);
                    if (promotion.Exclusive)
                    {
                        foreach (var l in inScope.Where(l => l.Promotions.Contains(promotion.Code))) l.Locked = true;
                    }
                }
            }

            foreach (var line in lines)
            {
                line.Discount = Math.Min(Round(line.Discount), line.Gross);
            }

            result.Subtotal = lines.Sum(l => l.Gross);
            result.DiscountTotal = lines.Sum(l => l.Discount);
            result.Total = result.Subtotal - result.DiscountTotal;
            // Prices are VAT inclusive.
            result.VatAmount = Round(result.Total - result.Total / (1 + vatRate));
            return result;
        }

        private static decimal ApplyPercent(Promotion p, List<BasketLine> lines)
        {
            var pct = Math.Clamp(p.Value, 0, 100) / 100m;
            decimal total = 0;
            foreach (var l in lines)
            {
                var d = Round((l.Gross - l.Discount) * pct);
                if (d <= 0) continue;
                if (p.Type == PromotionTypes.Markdown && l.CompareAtPrice == null) l.CompareAtPrice = l.UnitPrice;
                l.Discount += d;
                l.Promotions.Add(p.Code);
                total += d;
            }
            return total;
        }

        private static decimal ApplyBuyXPayY(Promotion p, List<BasketLine> lines)
        {
            var buy = p.BuyQuantity ?? 0;
            var pay = p.PayQuantity ?? 0;
            if (buy <= 0 || pay >= buy) return 0;

            // Expand into units, most expensive first; in each group of "buy" the cheapest (buy-pay) are free.
            var units = lines.SelectMany(l => Enumerable.Repeat(l, l.Quantity))
                .OrderByDescending(l => l.UnitPrice - l.Discount / Math.Max(l.Quantity, 1))
                .ToList();
            var groups = units.Count / buy;
            decimal total = 0;
            for (var g = 0; g < groups; g++)
            {
                var group = units.Skip(g * buy).Take(buy).ToList();
                foreach (var free in group.Skip(pay))
                {
                    var unitNet = free.UnitPrice - free.Discount / Math.Max(free.Quantity, 1);
                    free.Discount += unitNet;
                    if (!free.Promotions.Contains(p.Code)) free.Promotions.Add(p.Code);
                    total += unitNet;
                }
            }
            return total;
        }

        private static decimal ApplyBundle(Promotion p, List<BasketLine> lines)
        {
            var size = p.BuyQuantity ?? 0;
            if (size <= 0) return 0;
            var units = lines.SelectMany(l => Enumerable.Repeat(l, l.Quantity)).OrderByDescending(l => l.UnitPrice).ToList();
            var bundles = units.Count / size;
            decimal total = 0;
            for (var b = 0; b < bundles; b++)
            {
                var group = units.Skip(b * size).Take(size).ToList();
                var normal = group.Sum(u => u.UnitPrice);
                var saving = normal - p.Value;
                if (saving <= 0) continue;
                foreach (var u in group)
                {
                    var share = Round(saving * u.UnitPrice / normal);
                    u.Discount += share;
                    if (!u.Promotions.Contains(p.Code)) u.Promotions.Add(p.Code);
                    total += share;
                }
            }
            return total;
        }

        private static decimal ApplyAmountOff(Promotion p, List<BasketLine> lines, List<string> messages)
        {
            var basket = lines.Sum(l => l.Net);
            if (p.MinBasket.HasValue && basket < p.MinBasket.Value)
            {
                messages.Add($"Add {p.MinBasket.Value - basket:0.00} more to get {p.Name}.");
                return 0;
            }
            var amount = Math.Min(p.Value, basket);
            if (amount <= 0) return 0;

            decimal given = 0;
            for (var i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                var share = i == lines.Count - 1 ? amount - given : Round(amount * l.Net / basket);
                l.Discount += share;
                l.Promotions.Add(p.Code);
                given += share;
            }
            return amount;
        }

        private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
