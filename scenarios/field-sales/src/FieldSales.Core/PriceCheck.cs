using System;
using System.Collections.Generic;

namespace Wingtip.FieldSales.Core
{
    public class PriceDifference
    {
        public string ItemNo { get; set; }
        public decimal OrderedPrice { get; set; }
        public decimal CurrentPrice { get; set; }
    }

    /// <summary>
    /// Offline orders carry the prices the rep showed to the customer. When the price list has changed since the
    /// client last synchronized, the server recalculates and reports the lines that differ, and the rep has to
    /// confirm the new prices with the customer before submitting again.
    /// </summary>
    public static class PriceCheck
    {
        public const decimal Tolerance = 0.01m;

        public static IList<PriceDifference> Compare(SalesOrder order, Customer customer, IDictionary<string, Product> products,
            PriceCalculator calculator)
        {
            var differences = new List<PriceDifference>();
            foreach (var line in order.Lines)
            {
                Product product;
                if (!products.TryGetValue(line.ItemNo, out product)) continue;

                var current = calculator.UnitPrice(product, customer.PriceGroup, order.RequestedDelivery);
                if (Math.Abs(current - line.UnitPrice) >= Tolerance)
                {
                    differences.Add(new PriceDifference { ItemNo = line.ItemNo, OrderedPrice = line.UnitPrice, CurrentPrice = current });
                }
            }
            return differences;
        }
    }
}
