using System;
using System.Globalization;

namespace Southridge.Clinic.Domain
{
    /// <summary>
    /// Southridge medical record numbers: 8 digits with a trailing mod-10 check digit, shown as 0012345-6.
    /// Numbers below 1000000 were converted from the old practice management system in 2008.
    /// </summary>
    public static class MedicalRecordNumber
    {
        public static string Format(long sequence)
        {
            if (sequence <= 0 || sequence > 99999999)
            {
                throw new ArgumentOutOfRangeException("sequence");
            }

            string body = sequence.ToString("00000000", CultureInfo.InvariantCulture);
            return body.Substring(1) + "-" + CheckDigit(body).ToString(CultureInfo.InvariantCulture);
        }

        public static bool IsValid(string mrn)
        {
            string body;
            int check;
            return TryParse(mrn, out body, out check) && CheckDigit("0" + body) == check;
        }

        public static string Normalize(string mrn)
        {
            if (string.IsNullOrEmpty(mrn))
            {
                return mrn;
            }

            string digits = mrn.Trim().Replace("-", string.Empty).Replace(" ", string.Empty);
            if (digits.Length == 8)
            {
                return digits.Substring(0, 7) + "-" + digits.Substring(7);
            }

            return mrn.Trim();
        }

        private static bool TryParse(string mrn, out string body, out int check)
        {
            body = null;
            check = -1;
            string normalized = Normalize(mrn);
            if (normalized == null || normalized.Length != 9 || normalized[7] != '-')
            {
                return false;
            }

            body = normalized.Substring(0, 7);
            foreach (char c in body)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }

            if (!char.IsDigit(normalized[8]))
            {
                return false;
            }

            check = normalized[8] - '0';
            return true;
        }

        internal static int CheckDigit(string digits)
        {
            int sum = 0;
            bool doubleIt = true;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int d = digits[i] - '0';
                if (doubleIt)
                {
                    d *= 2;
                    if (d > 9)
                    {
                        d -= 9;
                    }
                }

                sum += d;
                doubleIt = !doubleIt;
            }

            return (10 - (sum % 10)) % 10;
        }
    }
}
