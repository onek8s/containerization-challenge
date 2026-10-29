using System.Net;
using System.Text.RegularExpressions;

namespace Fabrikam.Housing.Documents
{
    internal static class HtmlText
    {
        private static readonly Regex Body = new Regex(@"<body[^>]*>(.*)</body>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex StyleOrScript = new Regex(@"<(style|script|head)[^>]*>.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Tags = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex Space = new Regex(@"\s+", RegexOptions.Compiled);

        public static string BodyOf(string html)
        {
            var m = Body.Match(html ?? "");
            return m.Success ? m.Groups[1].Value : html ?? "";
        }

        public static string ToText(string html)
        {
            var text = StyleOrScript.Replace(html ?? "", " ");
            text = Tags.Replace(text, " ");
            return Space.Replace(WebUtility.HtmlDecode(text), " ").Trim();
        }
    }
}
