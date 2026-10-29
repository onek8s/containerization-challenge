using System.Text;

namespace Southridge.Clinic.Domain
{
    public static class PhoneNumber
    {
        /// <summary>Returns the 10 digit US number or null when the input is not a usable phone number.</summary>
        public static string Digits(string phone)
        {
            if (string.IsNullOrEmpty(phone))
            {
                return null;
            }

            var sb = new StringBuilder();
            foreach (char c in phone)
            {
                if (char.IsDigit(c))
                {
                    sb.Append(c);
                }
            }

            string digits = sb.ToString();
            if (digits.Length == 11 && digits[0] == '1')
            {
                digits = digits.Substring(1);
            }

            return digits.Length == 10 ? digits : null;
        }

        public static string Format(string phone)
        {
            string digits = Digits(phone);
            if (digits == null)
            {
                return phone;
            }

            return "(" + digits.Substring(0, 3) + ") " + digits.Substring(3, 3) + "-" + digits.Substring(6);
        }

        /// <summary>E.164 form used by the SMS gateway.</summary>
        public static string ToE164(string phone)
        {
            string digits = Digits(phone);
            return digits == null ? null : "+1" + digits;
        }
    }
}
