using System;
using System.Collections.Generic;
using System.Xml;

namespace VanArsdel.Integration.BindingTool.Commands
{
    /// <summary>Reads the &lt;settings&gt;&lt;property name="..."&gt;value&lt;/property&gt;&lt;/settings&gt; files export-settings writes.</summary>
    internal static class SettingsFile
    {
        public static IDictionary<string, string> Load(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XmlElement property in doc.SelectNodes("/settings/property"))
            {
                values[property.GetAttribute("name")] = property.InnerText;
            }

            return values;
        }
    }
}
