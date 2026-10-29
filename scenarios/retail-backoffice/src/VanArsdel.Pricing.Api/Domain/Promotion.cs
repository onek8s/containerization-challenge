using System;
using System.Collections.Generic;
using System.Linq;

namespace VanArsdel.Pricing.Api.Domain
{
    public static class PromotionTypes
    {
        /// <summary>Value = percent off the items in scope.</summary>
        public const string PercentOff = "PercentOff";

        /// <summary>Value = amount off the basket when MinBasket is reached, spread over the items in scope.</summary>
        public const string AmountOff = "AmountOff";

        /// <summary>"3 for 2": BuyQuantity items for the price of PayQuantity, cheapest free.</summary>
        public const string BuyXPayY = "BuyXPayY";

        /// <summary>BuyQuantity items in scope for Value in total.</summary>
        public const string Bundle = "Bundle";

        /// <summary>End-of-season markdown: Value = percent off, shown as a new price with the old one struck through.</summary>
        public const string Markdown = "Markdown";

        public static readonly string[] All = { PercentOff, AmountOff, BuyXPayY, Bundle, Markdown };
    }

    public class Promotion
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Channel { get; set; } = "All";
        public string Markets { get; set; }
        public DateTime StartsUtc { get; set; }
        public DateTime EndsUtc { get; set; }
        public int Priority { get; set; }
        public bool Exclusive { get; set; }
        public bool RequiresCode { get; set; }
        public decimal Value { get; set; }
        public int? BuyQuantity { get; set; }
        public int? PayQuantity { get; set; }
        public decimal? MinBasket { get; set; }
        public string Status { get; set; } = "Draft";
        public DateTime CreatedUtc { get; set; }
        public List<PromotionScope> Scopes { get; set; } = new List<PromotionScope>();

        public bool IsActive(DateTime nowUtc, string market, string channel, string enteredCode)
        {
            if (Status != "Active") return false;
            if (nowUtc < StartsUtc || nowUtc >= EndsUtc) return false;
            if (!MarketList().Contains(market, StringComparer.OrdinalIgnoreCase)) return false;
            if (Channel != "All" && !string.Equals(Channel, channel, StringComparison.OrdinalIgnoreCase)) return false;
            if (RequiresCode && !string.Equals(Code, enteredCode, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        public IEnumerable<string> MarketList() =>
            (Markets ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(m => m.Trim());

        public bool Covers(BasketLine line)
        {
            if (Scopes.Count == 0) return true;
            return Scopes.Any(s =>
                (s.ScopeType == "Sku" && Eq(s.Value, line.Sku)) ||
                (s.ScopeType == "Style" && Eq(s.Value, line.StyleCode)) ||
                (s.ScopeType == "Category" && Eq(s.Value, line.CategoryCode)) ||
                (s.ScopeType == "Brand" && Eq(s.Value, line.Brand)) ||
                (s.ScopeType == "Season" && Eq(s.Value, line.Season)));
        }

        private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public class PromotionScope
    {
        public int PromotionId { get; set; }
        public string ScopeType { get; set; }
        public string Value { get; set; }
    }
}
