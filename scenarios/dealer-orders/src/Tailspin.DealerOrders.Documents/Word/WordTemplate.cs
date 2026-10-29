using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Tailspin.DealerOrders.Documents.Word
{
    /// <summary>
    /// Fills a .docx template whose fields are content controls (Developer tab, plain text content controls) identified
    /// by their Tag. Table rows that contain controls tagged <c>&lt;prefix&gt;.*</c> are repeated once per item of the
    /// matching collection. Optional blocks tagged <c>if:&lt;name&gt;</c> are removed when the flag is false.
    /// </summary>
    public class WordTemplate
    {
        private readonly byte[] _template;

        public WordTemplate(byte[] template)
        {
            _template = template ?? throw new ArgumentNullException(nameof(template));
        }

        public byte[] Fill(IDictionary<string, string> fields, IDictionary<string, IList<IDictionary<string, string>>> tables = null,
            IDictionary<string, bool> flags = null)
        {
            using (var stream = new MemoryStream())
            {
                stream.Write(_template, 0, _template.Length);
                using (var document = WordprocessingDocument.Open(stream, true))
                {
                    var parts = new List<OpenXmlPartRootElement> { document.MainDocumentPart.Document };
                    parts.AddRange(document.MainDocumentPart.HeaderParts.Select(h => (OpenXmlPartRootElement)h.Header));
                    parts.AddRange(document.MainDocumentPart.FooterParts.Select(f => (OpenXmlPartRootElement)f.Footer));

                    foreach (var root in parts)
                    {
                        if (flags != null) ApplyFlags(root, flags);
                        if (tables != null) ExpandTables(root, tables);
                        FillControls(root, fields);
                        root.Save();
                    }

                    var settings = document.MainDocumentPart.DocumentSettingsPart?.Settings;
                    if (settings != null)
                    {
                        // Ask Word to refresh fields such as page numbers when the document is opened.
                        settings.RemoveAllChildren<UpdateFieldsOnOpen>();
                        settings.Append(new UpdateFieldsOnOpen { Val = true });
                        settings.Save();
                    }

                    document.PackageProperties.Modified = DateTime.UtcNow;
                }
                return stream.ToArray();
            }
        }

        /// <summary>Tags of all content controls in the template; used by the template check in the sales desk.</summary>
        public IList<string> Tags()
        {
            using (var stream = new MemoryStream(_template, false))
            using (var document = WordprocessingDocument.Open(stream, false))
            {
                return document.MainDocumentPart.Document.Descendants<SdtElement>()
                    .Select(TagOf).Where(t => t != null).Distinct().ToList();
            }
        }

        private static void ApplyFlags(OpenXmlElement root, IDictionary<string, bool> flags)
        {
            foreach (var sdt in root.Descendants<SdtElement>().Where(s => (TagOf(s) ?? "").StartsWith("if:", StringComparison.Ordinal)).ToList())
            {
                var name = TagOf(sdt).Substring(3);
                flags.TryGetValue(name, out var keep);
                if (keep) Unwrap(sdt);
                else sdt.Remove();
            }
        }

        private static void ExpandTables(OpenXmlElement root, IDictionary<string, IList<IDictionary<string, string>>> tables)
        {
            foreach (var table in tables)
            {
                var prefix = table.Key + ".";
                var templateRows = root.Descendants<TableRow>()
                    .Where(r => r.Descendants<SdtElement>().Any(s => (TagOf(s) ?? "").StartsWith(prefix, StringComparison.Ordinal)))
                    .ToList();
                foreach (var templateRow in templateRows)
                {
                    foreach (var item in table.Value)
                    {
                        var row = (TableRow)templateRow.CloneNode(true);
                        FillControls(row, item.ToDictionary(kv => prefix + kv.Key, kv => kv.Value));
                        templateRow.InsertBeforeSelf(row);
                    }
                    templateRow.Remove();
                }
            }
        }

        private static void FillControls(OpenXmlElement root, IDictionary<string, string> fields)
        {
            foreach (var sdt in root.Descendants<SdtElement>().ToList())
            {
                var tag = TagOf(sdt);
                if (tag == null || !fields.TryGetValue(tag, out var value)) continue;
                SetText(sdt, value ?? "");
                sdt.SdtProperties?.RemoveAllChildren<ShowingPlaceholder>();
            }
        }

        private static void SetText(SdtElement sdt, string value)
        {
            var runs = sdt.Descendants<Run>().ToList();
            var properties = runs.FirstOrDefault()?.RunProperties?.CloneNode(true) as RunProperties;
            if (properties != null) properties.RemoveAllChildren<RunStyle>();

            var lines = value.Replace("\r\n", "\n").Split('\n');
            var run = new Run();
            if (properties != null) run.Append(properties);
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0) run.Append(new Break());
                run.Append(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
            }

            var paragraphs = sdt.Descendants<Paragraph>().ToList();
            if (paragraphs.Count > 0)
            {
                // Block or cell level control: keep the first paragraph and its formatting, drop the rest.
                var first = paragraphs[0];
                foreach (var extra in paragraphs.Skip(1)) extra.Remove();
                foreach (var r in first.Elements<Run>().ToList()) r.Remove();
                first.Append(run);
                return;
            }

            var content = sdt.Elements().FirstOrDefault(e => e is SdtContentRun) ?? sdt.Elements().FirstOrDefault(e => e.LocalName == "sdtContent");
            if (content == null) return;
            content.RemoveAllChildren();
            content.Append(run);
        }

        private static void Unwrap(SdtElement sdt)
        {
            var content = sdt.Elements().FirstOrDefault(e => e.LocalName == "sdtContent");
            if (content == null) { sdt.Remove(); return; }
            foreach (var child in content.ChildElements.ToList())
            {
                child.Remove();
                sdt.InsertBeforeSelf(child);
            }
            sdt.Remove();
        }

        private static string TagOf(SdtElement sdt) => sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
    }
}
