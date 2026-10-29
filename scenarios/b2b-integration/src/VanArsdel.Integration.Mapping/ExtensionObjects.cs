using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace VanArsdel.Integration.Mapping
{
    public class ExtensionObjectDefinition
    {
        public string Namespace { get; set; }
        public string AssemblyName { get; set; }
        public string ClassName { get; set; }
    }

    /// <summary>
    /// Reads a BizTalk custom extension XML file (the ExtObjXmlPath of a map with custom XSLT): the namespaces the
    /// XSLT uses for scripting functoids and the classes behind them.
    /// </summary>
    public static class ExtensionObjects
    {
        public static IList<ExtensionObjectDefinition> Load(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);
            return Parse(doc);
        }

        public static IList<ExtensionObjectDefinition> Parse(XmlDocument doc)
        {
            return doc.SelectNodes("/ExtensionObjects/ExtensionObject").OfType<XmlElement>()
                .Select(e => new ExtensionObjectDefinition
                {
                    Namespace = e.GetAttribute("Namespace"),
                    AssemblyName = e.GetAttribute("AssemblyName"),
                    ClassName = e.GetAttribute("ClassName")
                })
                .ToList();
        }

        /// <summary>
        /// Resolves the class by its simple assembly name so the same file works with the GAC'd strong name on the
        /// BizTalk servers and with the build output elsewhere.
        /// </summary>
        public static object CreateInstance(ExtensionObjectDefinition definition)
        {
            var simpleName = (definition.AssemblyName ?? string.Empty).Split(',')[0].Trim();
            var type = Type.GetType(definition.ClassName + ", " + simpleName, false)
                       ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(definition.ClassName, false)).FirstOrDefault(t => t != null);
            if (type == null)
            {
                throw new InvalidOperationException("Extension class " + definition.ClassName + " (" + simpleName + ") was not found.");
            }

            return Activator.CreateInstance(type);
        }
    }
}
