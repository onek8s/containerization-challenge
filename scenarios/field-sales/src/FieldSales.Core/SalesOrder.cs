using System;
using System.Collections.Generic;
using System.Linq;

namespace Wingtip.FieldSales.Core
{
    public class SalesOrder
    {
        public SalesOrder()
        {
            Lines = new List<OrderLine>();
        }

        public Guid ClientOrderId { get; set; }

        /// <summary>Number the client gives the order while offline, e.g. RJ-171103-004. Printed on the store copy.</summary>
        public string RepReference { get; set; }

        public string CustomerNo { get; set; }
        public string RepCode { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime RequestedDelivery { get; set; }
        public string Comment { get; set; }
        public List<OrderLine> Lines { get; set; }

        public decimal NetTotal
        {
            get { return Lines.Sum(l => l.LineTotal); }
        }

        public decimal VatTotal
        {
            get
            {
                return Lines
                    .GroupBy(l => l.VatRate)
                    .Sum(g => Money.Round(g.Sum(l => l.LineTotal) * g.Key / 100m));
            }
        }

        public decimal GrossTotal
        {
            get { return NetTotal + VatTotal; }
        }

        /// <summary>Manual discount over the whole order, percent of the customer net price.</summary>
        public decimal ManualDiscountPercent
        {
            get
            {
                var before = Lines.Sum(l => l.GrossBeforeDiscount);
                if (before == 0m) return 0m;
                return Math.Round((before - NetTotal) * 100m / before, 2, MidpointRounding.AwayFromZero);
            }
        }

        public decimal MaxLineDiscountPercent
        {
            get { return Lines.Count == 0 ? 0m : Lines.Max(l => l.LineDiscountPercent); }
        }
    }
}
