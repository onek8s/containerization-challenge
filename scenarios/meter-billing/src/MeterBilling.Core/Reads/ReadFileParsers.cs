using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Coho.Billing.Core.Reads
{
    public static class ReadFileParsers
    {
        private static readonly IReadFileParser[] Parsers = { new HandheldReadParser(), new AmrReadParser() };

        public static ReadFile Parse(string fileName, Stream content)
        {
            // Handheld docking stations write ANSI; AMR files are plain ASCII.
            string[] lines;
            using (var reader = new StreamReader(content, Encoding.GetEncoding(1252), true))
            {
                lines = reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            }

            var first = lines.FirstOrDefault(l => l.Length > 0);
            var parser = Parsers.FirstOrDefault(p => p.CanParse(fileName, first));
            if (parser == null)
            {
                throw new ReadFileException("Unknown read file format", 1);
            }

            return parser.Parse(fileName, lines);
        }

        public static IEnumerable<IReadFileParser> All
        {
            get { return Parsers; }
        }
    }
}
