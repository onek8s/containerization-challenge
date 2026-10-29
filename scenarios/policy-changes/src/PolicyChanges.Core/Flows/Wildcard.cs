using System.Text.RegularExpressions;

namespace PolicyChanges.Core.Flows;

public static class Wildcard
{
    public static bool IsMatch(string text, string pattern) =>
        Regex.IsMatch(text, "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
