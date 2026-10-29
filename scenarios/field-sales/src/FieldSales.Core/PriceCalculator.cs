using System;
using System.Collections.Generic;
using System.Linq;

namespace Wingtip.FieldSales.Core
{
    /// <summary>
    /// Net unit price for a customer. The group discount for the product category is applied to the list price,
    /// and a valid promotion price is used instead when it is lower. The same calculation runs in the client
    /// (offline, with the cached price list) and on the server when the order is submitted.
    /// </summary>
    public class PriceCalculator
    {
        private readonly IList<PriceRule> _rules;
        private readonly IList<Promotion> _promotions;

        public PriceCalculator(IEnumerable<PriceRule> rules, IEnumerable<Promotion> promotions)
        {
            _rules = (rules ?? Enumerable.Empty<PriceRule>()).ToList();
            _promotions = (promotions ?? Enumerable.Empty<Promotion>()).ToList();
        }

        public decimal GroupDiscountPercent(string priceGroup, string category)
        {
            var exact = _rules.FirstOrDefault(r => Same(r.PriceGroup, priceGroup) && Same(r.Category, category));
            if (exact != null) return exact.DiscountPercent;

            var groupWide = _rules.FirstOrDefault(r => Same(r.PriceGroup, priceGroup) && string.IsNullOrEmpty(r.Category));
            return groupWide == null ? 0m : groupWide.DiscountPercent;
        }

        public decimal UnitPrice(Product product, string priceGroup, DateTime deliveryDate)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));

            var discount = GroupDiscountPercent(priceGroup, product.Category);
            var price = Money.Round(product.ListPrice * (100m - discount) / 100m);

            var promo = _promotions
                .Where(p => Same(p.ItemNo, product.ItemNo) && p.IsValidOn(deliveryDate))
                .OrderBy(p => p.PromoPrice)
                .FirstOrDefault();

            if (promo != null && promo.PromoPrice < price) price = promo.PromoPrice;
            return price;
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }
}
