using System;

namespace Wingtip.FieldSales.Core
{
    /// <summary>Campaign price for one item. Valid dates are inclusive and refer to the delivery date.</summary>
    public class Promotion
    {
        public string ItemNo { get; set; }
        public decimal PromoPrice { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }

        public bool IsValidOn(DateTime day)
        {
            return day.Date >= ValidFrom.Date && day.Date <= ValidTo.Date;
        }
    }
}
