using System.Text.RegularExpressions;

namespace Fabrikam.Housing.Common
{
    public static class Postcode
    {
        private static readonly Regex Pattern = new Regex(@"^([A-Z]{1,2}[0-9][A-Z0-9]?)\s*([0-9][A-Z]{2})$", RegexOptions.Compiled);

        public static bool TryNormalise(string value, out string normalised)
        {
            normalised = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var m = Pattern.Match(value.Trim().ToUpperInvariant().Replace("  ", " "));
            if (!m.Success) return false;
            normalised = m.Groups[1].Value + " " + m.Groups[2].Value;
            return true;
        }

        public static string Outward(string postcode)
        {
            return TryNormalise(postcode, out var n) ? n.Split(' ')[0] : null;
        }
    }
}
