using System.Collections.Generic;
using VanArsdel.Integration.Domain.Codes;

namespace VanArsdel.Integration.Mapping.Functoids
{
    /// <summary>
    /// Scripting functoid class: GS1 codes and counters. One instance per map execution, so the counters give the
    /// HL01/HL02 numbering of the 856 and the CPS sequence of the DESADV.
    /// </summary>
    public class CodeFunctoids
    {
        private readonly Dictionary<string, int> _counters = new Dictionary<string, int>();

        public string Gtin14(string code)
        {
            return CheckDigit.ToGtin14((code ?? string.Empty).Trim()) ?? string.Empty;
        }

        /// <summary>GTIN-14 back to GTIN-13 (EDIFACT EN) or GTIN-12 (UPC, X12 UP) when the leading digits are zero.</summary>
        public string GtinForStandard(string gtin14, string standard)
        {
            var g = (gtin14 ?? string.Empty).Trim();
            if (g.Length != 14)
            {
                return g;
            }

            if (standard == "X12" && g.StartsWith("00", System.StringComparison.Ordinal))
            {
                return g.Substring(2);
            }

            return g.StartsWith("0", System.StringComparison.Ordinal) ? g.Substring(1) : g;
        }

        public string IsValidGtin(string code)
        {
            return CheckDigit.IsValidGtin((code ?? string.Empty).Trim()) ? "true" : "false";
        }

        public string IsValidGln(string code)
        {
            return CheckDigit.IsValidGln((code ?? string.Empty).Trim()) ? "true" : "false";
        }

        public string Next(string counter)
        {
            int v;
            _counters.TryGetValue(counter, out v);
            v++;
            _counters[counter] = v;
            return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public string Current(string counter)
        {
            int v;
            _counters.TryGetValue(counter, out v);
            return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
