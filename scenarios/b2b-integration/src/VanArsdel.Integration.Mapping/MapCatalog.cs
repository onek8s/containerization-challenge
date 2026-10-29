using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace VanArsdel.Integration.Mapping
{
    public class MapEntry
    {
        public string Name { get; set; }
        public string BtmPath { get; set; }
        public string XsltPath { get; set; }
        public string ExtensionXmlPath { get; set; }
        public string SourceSchema { get; set; }
        public string TargetSchema { get; set; }
    }

    /// <summary>
    /// Index of the maps in the BizTalk maps project, read from the .btm files (custom XSLT path, extension XML,
    /// source and target schema references). Runners are compiled once and cached.
    /// </summary>
    public class MapCatalog
    {
        private readonly Dictionary<string, MapEntry> _entries;
        private readonly Dictionary<string, MapRunner> _runners = new Dictionary<string, MapRunner>(StringComparer.OrdinalIgnoreCase);
        private readonly object _gate = new object();

        private MapCatalog(IEnumerable<MapEntry> entries)
        {
            _entries = entries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<MapEntry> Entries
        {
            get { return _entries.Values.OrderBy(e => e.Name); }
        }

        public static MapCatalog Load(string mapsProjectFolder)
        {
            var entries = new List<MapEntry>();
            foreach (var btm in Directory.GetFiles(mapsProjectFolder, "*.btm", SearchOption.AllDirectories))
            {
                var doc = new XmlDocument();
                doc.Load(btm);
                var custom = (XmlElement)doc.SelectSingleNode("/mapsource/CustomXSLT");
                if (custom == null)
                {
                    continue;
                }

                var folder = Path.GetDirectoryName(btm);
                entries.Add(new MapEntry
                {
                    Name = Path.GetFileNameWithoutExtension(btm),
                    BtmPath = btm,
                    XsltPath = Resolve(folder, custom.GetAttribute("XsltPath")),
                    ExtensionXmlPath = Resolve(folder, custom.GetAttribute("ExtObjXmlPath")),
                    SourceSchema = Attr(doc, "/mapsource/SrcTree/Reference", "Location"),
                    TargetSchema = Attr(doc, "/mapsource/TrgTree/Reference", "Location")
                });
            }

            return new MapCatalog(entries);
        }

        public MapRunner Get(string name)
        {
            lock (_gate)
            {
                MapRunner runner;
                if (_runners.TryGetValue(name, out runner))
                {
                    return runner;
                }

                MapEntry e;
                if (!_entries.TryGetValue(name, out e))
                {
                    throw new KeyNotFoundException("Map " + name + " is not in the maps project.");
                }

                runner = new MapRunner(e.Name, e.XsltPath, e.ExtensionXmlPath);
                _runners[name] = runner;
                return runner;
            }
        }

        private static string Resolve(string folder, string relative)
        {
            if (string.IsNullOrEmpty(relative))
            {
                return null;
            }

            return Path.GetFullPath(Path.Combine(folder, relative.Replace('\\', Path.DirectorySeparatorChar)));
        }

        private static string Attr(XmlDocument doc, string xpath, string name)
        {
            var e = doc.SelectSingleNode(xpath) as XmlElement;
            return e == null ? null : e.GetAttribute(name);
        }
    }
}
