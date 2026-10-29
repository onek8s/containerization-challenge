using System;

namespace Contoso.Purchasing.Core
{
    public class RequestLine
    {
        public int LineNo { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; }

        public decimal NetAmount
        {
            get { return Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero); }
        }

        public decimal VatAmount
        {
            get { return Math.Round(NetAmount * VatRate / 100m, 2, MidpointRounding.AwayFromZero); }
        }

        public decimal GrossAmount
        {
            get { return NetAmount + VatAmount; }
        }
    }
}
