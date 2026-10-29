using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Woodgrove.Statements.Core.Scheduling
{
    /// <summary>
    /// Report parameters of a subscription, stored as "Name=Value;Name=Value". Values may contain date tokens
    /// resolved against the run date: {Today}, {Yesterday}, {MonthStart}, {PrevMonthStart}, {PrevMonthEnd},
    /// {WeekStart}, {PrevWeekStart}, {YearStart}, {QuarterStart}, {PrevQuarterEnd} and {Today-N} / {Today+N}.
    /// A literal ';' or '\' in a value is escaped with '\'.
    /// </summary>
    public sealed class ParameterSet
    {
        static readonly Regex Token = new Regex(@"\{(?<name>[A-Za-z]+)(?<offset>[+-]\d{1,4})?\}", RegexOptions.Compiled);
        public const string DateFormat = "yyyy-MM-dd";

        readonly List<KeyValuePair<string, string>> _items = new List<KeyValuePair<string, string>>();

        public IReadOnlyList<KeyValuePair<string, string>> Items { get { return _items; } }

        public string this[string name]
        {
            get
            {
                var hit = _items.FirstOrDefault(i => string.Equals(i.Key, name, StringComparison.OrdinalIgnoreCase));
                return hit.Key == null ? null : hit.Value;
            }
            set
            {
                var index = _items.FindIndex(i => string.Equals(i.Key, name, StringComparison.OrdinalIgnoreCase));
                if (index >= 0) _items[index] = new KeyValuePair<string, string>(_items[index].Key, value);
                else _items.Add(new KeyValuePair<string, string>(name, value));
            }
        }

        public static ParameterSet Parse(string text)
        {
            var set = new ParameterSet();
            if (string.IsNullOrWhiteSpace(text)) return set;
            var current = new StringBuilder();
            var parts = new List<string>();
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch == '\\' && i + 1 < text.Length) { current.Append(text[++i]); continue; }
                if (ch == ';') { parts.Add(current.ToString()); current.Clear(); continue; }
                current.Append(ch);
            }
            parts.Add(current.ToString());
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                var eq = part.IndexOf('=');
                if (eq <= 0) throw new FormatException("Parameter '" + part.Trim() + "' has no name or no '='.");
                set[part.Substring(0, eq).Trim()] = part.Substring(eq + 1).Trim();
            }
            return set;
        }

        public override string ToString()
        {
            return string.Join(";", _items.Select(i => i.Key + "=" + Escape(i.Value)));
        }

        static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace(";", "\\;");
        }

        public ParameterSet Resolve(DateTime runDate)
        {
            var resolved = new ParameterSet();
            foreach (var item in _items)
                resolved[item.Key] = ResolveValue(item.Value, runDate);
            return resolved;
        }

        public static string ResolveValue(string value, DateTime runDate)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return Token.Replace(value, m =>
            {
                var date = TokenDate(m.Groups["name"].Value, runDate.Date);
                if (date == null) throw new FormatException("Unknown parameter token '" + m.Value + "'.");
                var d = date.Value;
                if (m.Groups["offset"].Success) d = d.AddDays(int.Parse(m.Groups["offset"].Value, CultureInfo.InvariantCulture));
                return d.ToString(DateFormat, CultureInfo.InvariantCulture);
            });
        }

        static DateTime? TokenDate(string name, DateTime today)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            var quarterStart = new DateTime(today.Year, (today.Month - 1) / 3 * 3 + 1, 1);
            switch (name.ToUpperInvariant())
            {
                case "TODAY": return today;
                case "YESTERDAY": return today.AddDays(-1);
                case "MONTHSTART": return monthStart;
                case "PREVMONTHSTART": return monthStart.AddMonths(-1);
                case "PREVMONTHEND": return monthStart.AddDays(-1);
                case "WEEKSTART": return weekStart;
                case "PREVWEEKSTART": return weekStart.AddDays(-7);
                case "YEARSTART": return new DateTime(today.Year, 1, 1);
                case "QUARTERSTART": return quarterStart;
                case "PREVQUARTEREND": return quarterStart.AddDays(-1);
                default: return null;
            }
        }

        public DateTime GetDate(string name, DateTime fallback)
        {
            var v = this[name];
            if (string.IsNullOrWhiteSpace(v)) return fallback;
            DateTime d;
            if (DateTime.TryParseExact(v, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
            throw new FormatException("Parameter " + name + " is not a date (" + DateFormat + "): " + v);
        }
    }
}
