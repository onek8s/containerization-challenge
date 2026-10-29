using System;
using System.Globalization;

namespace VanArsdel.Integration.Domain.Codes
{
    /// <summary>
    /// Serial Shipping Container Codes printed on the pallet labels by the WMS and sent in the 856 MAN segment and the
    /// DESADV GIN segment: extension digit + GS1 company prefix + serial reference + check digit (18 digits).
    /// </summary>
    public static class Sscc
    {
        public const string CompanyPrefix = "0614141";

        public static string Create(int extensionDigit, long serial)
        {
            if (extensionDigit < 0 || extensionDigit > 9)
            {
                throw new ArgumentOutOfRangeException("extensionDigit");
            }

            var serialLength = 16 - CompanyPrefix.Length;
            var serialText = serial.ToString(CultureInfo.InvariantCulture);
            if (serialText.Length > serialLength)
            {
                throw new ArgumentOutOfRangeException("serial", "Serial reference does not fit the company prefix.");
            }

            var body = extensionDigit.ToString(CultureInfo.InvariantCulture) + CompanyPrefix + serialText.PadLeft(serialLength, '0');
            return CheckDigit.Append(body);
        }

        public static bool IsValid(string sscc)
        {
            return sscc != null && sscc.Length == 18 && CheckDigit.IsValid(sscc);
        }

        /// <summary>Label text with application identifier, as on the pallet label: (00) 1 0614141 000000123 4.</summary>
        public static string ToHumanReadable(string sscc)
        {
            if (!IsValid(sscc))
            {
                return sscc;
            }

            return "(00) " + sscc.Substring(0, 1) + " " + sscc.Substring(1, CompanyPrefix.Length) + " "
                   + sscc.Substring(1 + CompanyPrefix.Length, 16 - CompanyPrefix.Length) + " " + sscc.Substring(17);
        }
    }
}
