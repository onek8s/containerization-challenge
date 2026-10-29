using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Lucerne.ProjectSites.Documents
{
    /// <summary>Thin helper over the Open XML SDK for the simple report layouts used here.</summary>
    internal sealed class WordBuilder : IDisposable
    {
        private readonly MemoryStream _stream = new MemoryStream();
        private readonly WordprocessingDocument _doc;
        private readonly Body _body;

        public WordBuilder(string title, string author)
        {
            _doc = WordprocessingDocument.Create(_stream, WordprocessingDocumentType.Document, true);
            var main = _doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            _body = main.Document.Body;
            AddStyles(main);
            _doc.PackageProperties.Title = title;
            _doc.PackageProperties.Creator = author;
            _doc.PackageProperties.Created = DateTime.UtcNow;
        }

        public void Heading(string text, int level = 1)
        {
            _body.AppendChild(new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "Heading" + level }),
                new Run(new Text(text))));
        }

        public void Paragraph(string text, bool bold = false)
        {
            var run = new Run(new Text(text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve });
            if (bold)
            {
                run.RunProperties = new RunProperties(new Bold());
            }

            _body.AppendChild(new Paragraph(run));
        }

        public void Bullets(IEnumerable<string> items)
        {
            foreach (var item in items)
            {
                Paragraph("• " + item);
            }
        }

        /// <summary>Table with a header row. fills: optional cell fill colour (hex) per row/column.</summary>
        public void Table(string[] header, IEnumerable<string[]> rows, Func<int, int, string> fill = null)
        {
            var table = new Table(new TableProperties(
                new TableStyle { Val = "TableGrid" },
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4 },
                    new BottomBorder { Val = BorderValues.Single, Size = 4 },
                    new LeftBorder { Val = BorderValues.Single, Size = 4 },
                    new RightBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

            table.AppendChild(Row(header, true, null, -1));
            var r = 0;
            foreach (var row in rows)
            {
                var rowIndex = r++;
                table.AppendChild(Row(row, false, fill, rowIndex));
            }

            _body.AppendChild(table);
            _body.AppendChild(new Paragraph());
        }

        public byte[] ToArray()
        {
            _doc.MainDocumentPart.Document.Save();
            _doc.Dispose();
            return _stream.ToArray();
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        private static TableRow Row(string[] cells, bool header, Func<int, int, string> fill, int rowIndex)
        {
            var tr = new TableRow();
            for (var c = 0; c < cells.Length; c++)
            {
                var run = new Run(new Text(cells[c] ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve });
                if (header)
                {
                    run.RunProperties = new RunProperties(new Bold(), new Color { Val = "FFFFFF" });
                }

                var props = new TableCellProperties();
                var colour = header ? "1F4E79" : fill?.Invoke(rowIndex, c);
                if (!string.IsNullOrEmpty(colour))
                {
                    props.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = colour });
                }

                tr.AppendChild(new TableCell(props, new Paragraph(run)));
            }

            return tr;
        }

        private static void AddStyles(MainDocumentPart main)
        {
            var styles = main.AddNewPart<StyleDefinitionsPart>();
            styles.Styles = new Styles(
                new DocDefaults(new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = "Arial", HighAnsi = "Arial" },
                    new FontSize { Val = "20" }))),
                HeadingStyle("Heading1", "heading 1", "32", "1F4E79"),
                HeadingStyle("Heading2", "heading 2", "26", "2E75B6"));
        }

        private static Style HeadingStyle(string id, string name, string size, string colour)
        {
            return new Style(
                new StyleName { Val = name },
                new BasedOn { Val = "Normal" },
                new StyleRunProperties(new Bold(), new FontSize { Val = size }, new Color { Val = colour }),
                new StyleParagraphProperties(new SpacingBetweenLines { Before = "240", After = "120" }))
            {
                Type = StyleValues.Paragraph,
                StyleId = id
            };
        }
    }

    internal static class RagColours
    {
        public static string Hex(Domain.RagStatus status)
        {
            switch (status)
            {
                case Domain.RagStatus.Green: return "C6EFCE";
                case Domain.RagStatus.Amber: return "FFEB9C";
                case Domain.RagStatus.Red: return "FFC7CE";
                default: return "EDEDED";
            }
        }
    }
}
