using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;

namespace VanArsdel.Integration.Deployment
{
    public class EnvironmentSettings
    {
        public EnvironmentSettings(string name, string fileName, IDictionary<string, string> values)
        {
            Name = name;
            FileName = fileName;
            Values = new Dictionary<string, string>(values, StringComparer.Ordinal);
        }

        public string Name { get; private set; }
        public string FileName { get; private set; }
        public IDictionary<string, string> Values { get; private set; }

        public string this[string key]
        {
            get
            {
                string v;
                return Values.TryGetValue(key, out v) ? v : null;
            }
        }
    }

    /// <summary>
    /// Reads the environment settings workbook (SettingsFileGenerator.xml, Excel 2003 XML Spreadsheet, the Deployment
    /// Framework for BizTalk convention). Row "Environment Name:" names the columns, column 2 holds the defaults,
    /// "Settings File Name:" the exported file name; a blank cell takes the default.
    /// </summary>
    public static class SettingsSpreadsheet
    {
        private const string Ss = "urn:schemas-microsoft-com:office:spreadsheet";

        public static IList<EnvironmentSettings> Load(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);
            return Parse(doc);
        }

        public static IList<EnvironmentSettings> Parse(XmlDocument doc)
        {
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("ss", Ss);
            var table = doc.SelectSingleNode("//ss:Worksheet[1]/ss:Table", ns);
            if (table == null)
            {
                throw new FormatException("The settings workbook has no worksheet table.");
            }

            var rows = table.SelectNodes("ss:Row", ns).OfType<XmlElement>().Select(r => ReadRow(r, ns)).ToList();
            var header = rows.FirstOrDefault(r => r.Count > 0 && Label(r) == "Environment Name:");
            if (header == null)
            {
                throw new FormatException("Row 'Environment Name:' is missing.");
            }

            var fileRow = rows.FirstOrDefault(r => Label(r) == "Settings File Name:");
            var generateRow = rows.FirstOrDefault(r => Label(r) == "Generate File?");
            var environments = new List<EnvironmentSettings>();
            var maxColumn = header.Keys.Max();
            for (var col = 3; col <= maxColumn; col++)
            {
                string envName;
                if (!header.TryGetValue(col, out envName) || string.IsNullOrWhiteSpace(envName))
                {
                    continue;
                }

                if (generateRow != null && Cell(generateRow, col).Equals("No", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var row in rows)
                {
                    var key = Label(row);
                    if (string.IsNullOrWhiteSpace(key) || key.EndsWith(":", StringComparison.Ordinal) || key.EndsWith("?", StringComparison.Ordinal) || key.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (row == header || row == fileRow || row == generateRow)
                    {
                        continue;
                    }

                    var value = Cell(row, col);
                    values[key] = string.IsNullOrEmpty(value) ? Cell(row, 2) : value;
                }

                var file = fileRow == null ? envName + "_settings.xml" : Cell(fileRow, col);
                environments.Add(new EnvironmentSettings(envName.Trim(), file, values));
            }

            return environments;
        }

        public static XmlDocument ToSettingsXml(EnvironmentSettings env)
        {
            var doc = new XmlDocument();
            var root = doc.CreateElement("settings");
            doc.AppendChild(root);
            foreach (var kv in env.Values.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var p = doc.CreateElement("property");
                p.SetAttribute("name", kv.Key);
                p.InnerText = kv.Value ?? string.Empty;
                root.AppendChild(p);
            }

            return doc;
        }

        private static Dictionary<int, string> ReadRow(XmlElement row, XmlNamespaceManager ns)
        {
            var cells = new Dictionary<int, string>();
            var col = 0;
            foreach (XmlElement cell in row.SelectNodes("ss:Cell", ns))
            {
                var index = cell.GetAttribute("Index", Ss);
                col = string.IsNullOrEmpty(index) ? col + 1 : int.Parse(index, CultureInfo.InvariantCulture);
                var data = cell.SelectSingleNode("ss:Data", ns);
                cells[col] = data == null ? string.Empty : data.InnerText;
            }

            return cells;
        }

        private static string Label(Dictionary<int, string> row)
        {
            return Cell(row, 1).Trim();
        }

        private static string Cell(Dictionary<int, string> row, int col)
        {
            string v;
            return row.TryGetValue(col, out v) ? v ?? string.Empty : string.Empty;
        }
    }
}
