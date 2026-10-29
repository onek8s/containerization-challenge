using System;
using System.Text;

namespace VanArsdel.Integration.Mapping.Functoids
{
    /// <summary>Scripting functoid class: text clean-up for EDI element limits and IDoc fields.</summary>
    public class TextFunctoids
    {
        public string Truncate(string value, double maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var max = (int)maxLength;
            var v = value.Trim();
            return v.Length <= max ? v : v.Substring(0, max).TrimEnd();
        }

        /// <summary>Removes characters that are separators in our outbound X12 and EDIFACT (* ~ &gt; + : ' ?).</summary>
        public string CleanForEdi(string value, double maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (ch == '*' || ch == '~' || ch == '>' || ch == '+' || ch == ':' || ch == '\'' || ch == '?' || char.IsControl(ch))
                {
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return Truncate(CollapseSpaces(sb.ToString()), maxLength);
        }

        /// <summary>Line n (1-based) of a text split into lines of at most maxLength at word boundaries.</summary>
        public string SplitLine(string value, double maxLength, double lineNumber)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var max = (int)maxLength;
            var wanted = (int)lineNumber;
            var words = CollapseSpaces(value).Split(' ');
            var line = new StringBuilder();
            var current = 1;
            foreach (var w in words)
            {
                if (line.Length > 0 && line.Length + 1 + w.Length > max)
                {
                    if (current == wanted)
                    {
                        return line.ToString();
                    }

                    current++;
                    line.Clear();
                }

                if (line.Length > 0)
                {
                    line.Append(' ');
                }

                line.Append(w.Length > max ? w.Substring(0, max) : w);
            }

            return current == wanted ? line.ToString() : string.Empty;
        }

        public string Upper(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        public string PadLeft(string value, double width, string padChar)
        {
            var c = string.IsNullOrEmpty(padChar) ? '0' : padChar[0];
            return (value ?? string.Empty).Trim().PadLeft((int)width, c);
        }

        /// <summary>SAP numbers come zero padded (0000104711); partners want them without.</summary>
        public string TrimLeadingZeros(string value)
        {
            var v = (value ?? string.Empty).Trim().TrimStart('0');
            return v.Length == 0 && !string.IsNullOrEmpty(value) ? "0" : v;
        }

        public string Coalesce(string first, string second)
        {
            return string.IsNullOrWhiteSpace(first) ? (second ?? string.Empty) : first;
        }

        private static string CollapseSpaces(string value)
        {
            var sb = new StringBuilder(value.Length);
            var space = false;
            foreach (var ch in value.Trim())
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!space)
                    {
                        sb.Append(' ');
                    }

                    space = true;
                }
                else
                {
                    sb.Append(ch);
                    space = false;
                }
            }

            return sb.ToString();
        }
    }
}
