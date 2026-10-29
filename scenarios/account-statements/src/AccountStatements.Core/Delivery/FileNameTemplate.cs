using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Woodgrove.Statements.Core.Delivery
{
    /// <summary>
    /// File name templates for share deliveries, e.g. "{CustomerNo}_{StatementDate:yyyyMMdd}.pdf" or
    /// "AgedDebt_{RunDate:yyyy-MM-dd}.xlsx". Unknown fields stay as written so mistakes are visible.
    /// </summary>
    public static class FileNameTemplate
    {
        static readonly Regex Field = new Regex(@"\{(?<name>[A-Za-z]+)(:(?<format>[^}]+))?\}", RegexOptions.Compiled);
        static readonly char[] Invalid = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "LPT1", "LPT2", "LPT3"
        };

        public static string Expand(string template, IDictionary<string, object> values)
        {
            if (string.IsNullOrWhiteSpace(template)) throw new ArgumentException("Template is empty.", nameof(template));
            return Sanitize(Replace(template, values, Sanitize));
        }

        /// <summary>Same fields for e-mail subjects and texts, without file name rules.</summary>
        public static string ExpandText(string template, IDictionary<string, object> values)
        {
            return string.IsNullOrEmpty(template) ? template : Replace(template, values, t => t);
        }

        static string Replace(string template, IDictionary<string, object> values, Func<string, string> clean)
        {
            return Field.Replace(template, m =>
            {
                object value;
                if (!values.TryGetValue(m.Groups["name"].Value, out value)) return m.Value;
                var format = m.Groups["format"].Success ? m.Groups["format"].Value : null;
                var formattable = value as IFormattable;
                var text = formattable != null ? formattable.ToString(format, CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture);
                return clean(text);
            });
        }

        /// <summary>Template values from report parameters: ISO dates become dates so "{StatementDate:yyyyMMdd}" works.</summary>
        public static IDictionary<string, object> Values(IEnumerable<KeyValuePair<string, string>> parameters, DateTime runDate)
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { { "RunDate", runDate } };
            foreach (var p in parameters)
            {
                DateTime date;
                if (DateTime.TryParseExact(p.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) values[p.Key] = date;
                else values[p.Key] = p.Value;
            }
            return values;
        }

        public static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_";
            var sb = new StringBuilder(name.Length);
            foreach (var ch in name)
                sb.Append(ch < 32 || Array.IndexOf(Invalid, ch) >= 0 ? '_' : ch);
            var result = sb.ToString().Trim().TrimEnd('.');
            var stem = result.Split('.')[0];
            if (Reserved.Contains(stem)) result = "_" + result;
            if (result.Length > 120)
            {
                var dot = result.LastIndexOf('.');
                var ext = dot > 0 && result.Length - dot <= 6 ? result.Substring(dot) : "";
                result = result.Substring(0, 120 - ext.Length) + ext;
            }
            return result.Length == 0 ? "_" : result;
        }
    }
}
