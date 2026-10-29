using System;
using System.Collections.Generic;
using System.Linq;

namespace VanArsdel.Integration.Domain.Orders
{
    public class AmountLine
    {
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal PriceBasis { get; set; }
        public decimal TaxRatePercent { get; set; }
    }

    public class DocumentTotals
    {
        public decimal LineTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public IDictionary<decimal, decimal> TaxByRate { get; set; }
    }

    /// <summary>
    /// Totals as the ERP calculates them: line amounts rounded to 2 decimals, tax per rate on the summed base, so the
    /// TDS/MOA totals we send match the ERP invoice to the cent.
    /// </summary>
    public static class Totals
    {
        public static decimal LineAmount(decimal quantity, decimal unitPrice, decimal priceBasis)
        {
            if (priceBasis <= 0)
            {
                priceBasis = 1;
            }

            return Math.Round(quantity * unitPrice / priceBasis, 2, MidpointRounding.AwayFromZero);
        }

        public static DocumentTotals Calculate(IEnumerable<AmountLine> lines)
        {
            var list = (lines ?? Enumerable.Empty<AmountLine>()).ToList();
            var byRate = list
                .GroupBy(l => l.TaxRatePercent)
                .ToDictionary(g => g.Key, g => g.Sum(l => LineAmount(l.Quantity, l.UnitPrice, l.PriceBasis)));

            var tax = byRate.ToDictionary(r => r.Key, r => Math.Round(r.Value * r.Key / 100m, 2, MidpointRounding.AwayFromZero));
            var lineTotal = byRate.Values.Sum();
            var taxTotal = tax.Values.Sum();

            return new DocumentTotals
            {
                LineTotal = lineTotal,
                TaxTotal = taxTotal,
                GrandTotal = lineTotal + taxTotal,
                TaxByRate = tax
            };
        }

        /// <summary>X12 TDS01 is the total in cents without a decimal point (N2).</summary>
        public static string ToX12ImpliedDecimal(decimal amount)
        {
            return Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static decimal FromX12ImpliedDecimal(string value, int decimals)
        {
            var raw = decimal.Parse(value, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture);
            for (var i = 0; i < decimals; i++)
            {
                raw /= 10m;
            }

            return raw;
        }
    }
}
