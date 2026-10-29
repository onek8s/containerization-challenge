namespace Wingtip.FieldSales.Core
{
    public class OrderLine
    {
        public int LineNo { get; set; }
        public string ItemNo { get; set; }
        public int Quantity { get; set; }

        /// <summary>Net unit price for the customer before the manual line discount.</summary>
        public decimal UnitPrice { get; set; }

        /// <summary>Manual discount given by the sales rep, percent.</summary>
        public decimal LineDiscountPercent { get; set; }

        public decimal VatRate { get; set; }

        public decimal GrossBeforeDiscount
        {
            get { return Money.Round(Quantity * UnitPrice); }
        }

        public decimal LineTotal
        {
            get { return Money.Round(Quantity * UnitPrice * (100m - LineDiscountPercent) / 100m); }
        }
    }
}
