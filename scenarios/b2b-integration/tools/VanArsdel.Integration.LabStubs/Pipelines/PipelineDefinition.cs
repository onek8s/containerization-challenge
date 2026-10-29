using System.Xml.Linq;

namespace VanArsdel.Integration.LabStubs.Pipelines
{
    public class PipelineComponentDefinition
    {
        public string TypeName { get; set; }
        public string DisplayName { get; set; }
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Class name without namespace and assembly (EncodingFixerComponent, EdiDisassembler, ...).</summary>
        public string ShortName => TypeName.Split(',')[0].Trim().Split('.').Last();

        public string Get(string name, string fallback = null) => Properties.TryGetValue(name, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;

        public bool GetBool(string name, bool fallback) => Properties.TryGetValue(name, out var v) && bool.TryParse(v, out var b) ? b : fallback;

        public int GetInt(string name, int fallback) => Properties.TryGetValue(name, out var v) && int.TryParse(v, out var i) ? i : fallback;
    }

    /// <summary>A pipeline designer file (.btp): stages in order, components with their design-time properties.</summary>
    public class PipelineDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsReceive { get; set; }
        public List<PipelineComponentDefinition> Components { get; } = new List<PipelineComponentDefinition>();

        public static PipelineDefinition Load(string path)
        {
            // the designer saves UTF-16; XDocument.Load reads the BOM
            var doc = XDocument.Load(path);
            var root = doc.Root;
            var definition = new PipelineDefinition
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Description = (string)root.Element("Description"),
                IsReceive = ((string)root.Attribute("PolicyFilePath") ?? string.Empty).Contains("Receive", StringComparison.OrdinalIgnoreCase)
            };

            foreach (var component in root.Descendants("Component"))
            {
                var c = new PipelineComponentDefinition
                {
                    TypeName = (string)component.Element("Name"),
                    DisplayName = (string)component.Element("ComponentName")
                };

                foreach (var property in component.Element("Properties")?.Elements("Property") ?? Enumerable.Empty<XElement>())
                {
                    c.Properties[(string)property.Attribute("Name")] = ((string)property.Element("Value") ?? string.Empty).Trim();
                }

                definition.Components.Add(c);
            }

            return definition;
        }
    }
}
