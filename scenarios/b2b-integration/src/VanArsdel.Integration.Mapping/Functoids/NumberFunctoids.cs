using System;
using System.Globalization;
using VanArsdel.Integration.Domain.Orders;

namespace VanArsdel.Integration.Mapping.Functoids
{
    /// <summary>Scripting functoid class: numbers as the EDI and IDoc formats want them.</summary>
    public class NumberFunctoids
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public string Format(string value, double decimals)
        {
            decimal d;
            if (!TryParse(value, out d))
            {
                return string.Empty;
            }

            var places = (int)decimals;
            var rounded = Math.Round(d, places, MidpointRounding.AwayFromZero);
            return rounded.ToString(places == 0 ? "0" : "0." + new string('0', places), Inv);
        }

        /// <summary>Real (R) EDI number: no trailing zeros, no trailing decimal point.</summary>
        public string Real(string value)
        {
            decimal d;
            if (!TryParse(value, out d))
            {
                return string.Empty;
            }

            var s = d.ToString("0.######", Inv);
            return s;
        }

        /// <summary>X12 N2 (TDS01): amount in cents.</summary>
        public string ImpliedDecimal(string value)
        {
            decimal d;
            return TryParse(value, out d) ? Totals.ToX12ImpliedDecimal(d) : string.Empty;
        }

        public string FromImpliedDecimal(string value, double decimals)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Totals.FromX12ImpliedDecimal(value.Trim(), (int)decimals).ToString("0.00", Inv);
        }

        public string LineAmount(string quantity, string unitPrice, string priceBasis)
        {
            decimal q, p, b;
            if (!TryParse(quantity, out q) || !TryParse(unitPrice, out p))
            {
                return string.Empty;
            }

            if (!TryParse(priceBasis, out b) || b <= 0)
            {
                b = 1;
            }

            return Totals.LineAmount(q, p, b).ToString("0.00", Inv);
        }

        /// <summary>SAP style number with the sign at the end (IDoc amounts like "125.00-" for credits).</summary>
        public string FromSapNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var v = value.Trim();
            if (v.EndsWith("-", StringComparison.Ordinal))
            {
                v = "-" + v.Substring(0, v.Length - 1);
            }

            decimal d;
            return TryParse(v, out d) ? d.ToString("0.00####", Inv).TrimEnd('0').TrimEnd('.') : string.Empty;
        }

        public string Percent(string rate)
        {
            decimal d;
            return TryParse(rate, out d) ? d.ToString("0.##", Inv) : string.Empty;
        }

        private static bool TryParse(string value, out decimal d)
        {
            return decimal.TryParse((value ?? string.Empty).Trim(), NumberStyles.Number, Inv, out d);
        }
    }
}
