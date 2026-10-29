using System.Collections.Generic;
using System.Text;

namespace Litware.OrderHub.Core
{
    /// <summary>Splits one delimited line, honouring double quotes ("" inside quotes is a quote).</summary>
    public static class CsvLine
    {
        public static IReadOnlyList<string> Split(string line, char delimiter)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"')
                {
                    quoted = true;
                }
                else if (c == delimiter)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields;
        }

        public static string Quote(string value, char delimiter)
        {
            if (value == null) return string.Empty;
            if (value.IndexOf(delimiter) < 0 && value.IndexOf('"') < 0 && value.IndexOf('\n') < 0)
            {
                return value;
            }
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
