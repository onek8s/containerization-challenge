using System;
using System.IO;
using System.Text;

namespace Wingtip.Invoices.Core.Parsing
{
    public static class InvoiceParsers
    {
        public static IInvoiceParser For(SourceFormat format)
        {
            switch (format)
            {
                case SourceFormat.Edifact:
                    return new EdifactInvoiceParser();
                case SourceFormat.Ubl:
                    return new UblInvoiceParser();
                case SourceFormat.Csv:
                    return new CsvInvoiceParser();
                default:
                    throw new ArgumentOutOfRangeException("format", format, "No parser for the format.");
            }
        }

        /// <summary>Detects the format from the first characters of the file and its extension.</summary>
        public static SourceFormat Detect(string fileName, string content)
        {
            var head = (content ?? string.Empty).TrimStart('﻿', ' ', '\r', '\n', '\t');
            if (head.StartsWith("UNA") || head.StartsWith("UNB")) return SourceFormat.Edifact;
            if (head.StartsWith("<")) return SourceFormat.Ubl;
            if (head.StartsWith("H;", StringComparison.OrdinalIgnoreCase) || head.StartsWith("#")) return SourceFormat.Csv;

            switch ((Path.GetExtension(fileName) ?? string.Empty).ToLowerInvariant())
            {
                case ".edi":
                case ".inv":
                    return SourceFormat.Edifact;
                case ".xml":
                    return SourceFormat.Ubl;
                case ".csv":
                case ".txt":
                    return SourceFormat.Csv;
                default:
                    return SourceFormat.Unknown;
            }
        }

        /// <summary>
        /// Decodes a supplier file. A byte order mark wins; otherwise XML and EDIFACT UNOC/UNOW files
        /// use their declared character set and CSV files use the supplier's code page.
        /// </summary>
        public static string Decode(byte[] data, Encoding supplierEncoding)
        {
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return Encoding.UTF8.GetString(data, 3, data.Length - 3);
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
                return Encoding.Unicode.GetString(data, 2, data.Length - 2);

            var probe = Encoding.ASCII.GetString(data, 0, Math.Min(data.Length, 200));
            if (probe.StartsWith("<?xml") && probe.IndexOf("encoding=\"UTF-8\"", StringComparison.OrdinalIgnoreCase) >= 0)
                return Encoding.UTF8.GetString(data);
            if (probe.IndexOf("UNOW", StringComparison.Ordinal) >= 0 || probe.IndexOf("UNOY", StringComparison.Ordinal) >= 0)
                return Encoding.UTF8.GetString(data);
            if (probe.IndexOf("UNOC", StringComparison.Ordinal) >= 0)
                return Encoding.GetEncoding(28591).GetString(data);

            return (supplierEncoding ?? Encoding.UTF8).GetString(data);
        }
    }
}
