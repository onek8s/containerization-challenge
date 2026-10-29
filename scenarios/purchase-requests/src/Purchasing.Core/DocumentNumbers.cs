using System;
using System.Globalization;

namespace Contoso.Purchasing.Core
{
    public static class DocumentNumbers
    {
        public static string RequestNumber(int year, long sequence)
        {
            return string.Format(CultureInfo.InvariantCulture, "PR-{0}-{1:00000}", year, sequence);
        }

        /// <summary>
        /// ERP accepts 10 character order numbers: 45 + 8 digits.
        /// </summary>
        public static string OrderNumber(long sequence)
        {
            if (sequence < 0 || sequence > 99999999) throw new ArgumentOutOfRangeException("sequence");
            return "45" + sequence.ToString("00000000", CultureInfo.InvariantCulture);
        }
    }
}
