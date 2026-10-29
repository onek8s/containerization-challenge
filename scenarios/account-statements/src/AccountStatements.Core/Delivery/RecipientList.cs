using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Woodgrove.Statements.Core.Delivery
{
    public static class RecipientList
    {
        static readonly Regex Address = new Regex(@"^[^@\s;,]+@[^@\s;,]+\.[A-Za-z]{2,}$", RegexOptions.Compiled);

        public static IList<string> Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();
            return text.Split(new[] { ';', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(a => a.Trim())
                       .Where(a => a.Length > 0)
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .ToList();
        }

        public static IList<string> Invalid(IEnumerable<string> addresses)
        {
            return addresses.Where(a => !IsValid(a)).ToList();
        }

        public static bool IsValid(string address)
        {
            return !string.IsNullOrWhiteSpace(address) && Address.IsMatch(address.Trim());
        }

        public static string Join(IEnumerable<string> addresses)
        {
            return string.Join("; ", addresses);
        }
    }
}
