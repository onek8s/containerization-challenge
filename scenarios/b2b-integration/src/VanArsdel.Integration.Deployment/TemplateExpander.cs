using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VanArsdel.Integration.Deployment
{
    public class ExpansionResult
    {
        public string Text { get; set; }
        public IList<string> MissingTokens { get; set; }
    }

    /// <summary>
    /// Environment specific bindings from the master bindings file, XmlPreprocess style: ${Name} tokens and
    /// conditional blocks between &lt;!-- ifdef ${Name} --&gt; ... &lt;!-- endif --&gt; (kept when the setting is
    /// "True") and &lt;!-- ifndef ... --&gt;. Values are XML escaped because they land in attribute and element text.
    /// </summary>
    public static class TemplateExpander
    {
        private static readonly Regex Token = new Regex(@"\$\{(?<name>[A-Za-z0-9_\.]+)\}", RegexOptions.Compiled);
        private static readonly Regex Block = new Regex(@"<!--\s*(?<kind>ifdef|ifndef)\s+\$\{(?<name>[A-Za-z0-9_\.]+)\}\s*-->(?<body>.*?)<!--\s*endif\s*-->",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public static ExpansionResult Expand(string template, IDictionary<string, string> settings, bool xmlEscape = true)
        {
            var missing = new SortedSet<string>(StringComparer.Ordinal);

            var withBlocks = Block.Replace(template, m =>
            {
                string v;
                var on = settings.TryGetValue(m.Groups["name"].Value, out v) && string.Equals(v, "True", StringComparison.OrdinalIgnoreCase);
                var keep = m.Groups["kind"].Value == "ifdef" ? on : !on;
                return keep ? m.Groups["body"].Value : string.Empty;
            });

            var text = Token.Replace(withBlocks, m =>
            {
                string v;
                if (settings.TryGetValue(m.Groups["name"].Value, out v))
                {
                    return xmlEscape ? Escape(v) : v;
                }

                missing.Add(m.Groups["name"].Value);
                return m.Value;
            });

            return new ExpansionResult { Text = text, MissingTokens = missing.ToList() };
        }

        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '&':
                        sb.Append("&amp;");
                        break;
                    case '<':
                        sb.Append("&lt;");
                        break;
                    case '>':
                        sb.Append("&gt;");
                        break;
                    case '"':
                        sb.Append("&quot;");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
