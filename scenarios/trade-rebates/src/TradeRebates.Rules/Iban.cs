using System.Text;

namespace ContosoFoods.TradeRebates.Rules
{
    public static class Iban
    {
        public static string Normalize(string? iban)
        {
            if (iban == null)
                return string.Empty;
            var sb = new StringBuilder(iban.Length);
            foreach (char c in iban)
            {
                if (c != ' ' && c != '-')
                    sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        public static bool IsValid(string? iban)
        {
            string value = Normalize(iban);
            if (value.Length < 15 || value.Length > 34)
                return false;
            if (!char.IsLetter(value[0]) || !char.IsLetter(value[1]) || !char.IsDigit(value[2]) || !char.IsDigit(value[3]))
                return false;

            string rearranged = value.Substring(4) + value.Substring(0, 4);
            int remainder = 0;
            foreach (char c in rearranged)
            {
                if (c >= '0' && c <= '9')
                {
                    remainder = (remainder * 10 + (c - '0')) % 97;
                }
                else if (c >= 'A' && c <= 'Z')
                {
                    int n = c - 'A' + 10;
                    remainder = (remainder * 100 + n) % 97;
                }
                else
                {
                    return false;
                }
            }
            return remainder == 1;
        }

        /// <summary>Shows country code and last four characters only.</summary>
        public static string Mask(string? iban)
        {
            string value = Normalize(iban);
            if (value.Length <= 6)
                return new string('*', value.Length);
            return value.Substring(0, 2) + new string('*', value.Length - 6) + value.Substring(value.Length - 4);
        }
    }
}
