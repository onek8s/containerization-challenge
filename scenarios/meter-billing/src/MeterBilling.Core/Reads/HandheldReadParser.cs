using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Coho.Billing.Core.Reads
{
    /// <summary>
    /// Route export from the meter reader handhelds (docking station export, semicolon separated):
    /// ROUTE;SEQ;METER;READ_DATE;READ_TIME;READING;CODE;READER
    /// Dates are dd.MM.yyyy and readings use a decimal comma.
    /// </summary>
    public class HandheldReadParser : IReadFileParser
    {
        public const string Header = "ROUTE;SEQ;METER;READ_DATE;READ_TIME;READING;CODE;READER";

        private static readonly string[] SkippedCodes = { "NA", "DM" };

        public bool CanParse(string fileName, string firstLine)
        {
            return firstLine != null && firstLine.Trim().StartsWith("ROUTE;SEQ;METER", StringComparison.OrdinalIgnoreCase);
        }

        public ReadFile Parse(string fileName, IEnumerable<string> lines)
        {
            var file = new ReadFile
            {
                FileName = fileName,
                Source = ReadSource.Handheld,
                BatchId = Path.GetFileNameWithoutExtension(fileName)
            };

            var lineNumber = 0;
            var headerSeen = false;
            foreach (var raw in lines)
            {
                lineNumber++;
                var line = raw.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (!headerSeen)
                {
                    if (!string.Equals(line, Header, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ReadFileException("Unexpected header, expected " + Header, lineNumber);
                    }

                    headerSeen = true;
                    continue;
                }

                var fields = line.Split(';');
                if (fields.Length != 8)
                {
                    file.RejectedLines.Add(string.Format("{0}: expected 8 fields, found {1}", lineNumber, fields.Length));
                    continue;
                }

                var code = fields[6].Trim().ToUpperInvariant();
                if (Array.IndexOf(SkippedCodes, code) >= 0)
                {
                    // No reading taken; the account is estimated in the bill run.
                    file.RejectedLines.Add(string.Format("{0}: meter {1} not read ({2})", lineNumber, fields[2].Trim(), code));
                    continue;
                }

                DateTime date;
                if (!DateTime.TryParseExact(fields[3].Trim() + " " + fields[4].Trim(), "dd.MM.yyyy HH:mm",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    file.RejectedLines.Add(string.Format("{0}: invalid date '{1} {2}'", lineNumber, fields[3], fields[4]));
                    continue;
                }

                decimal reading;
                if (!decimal.TryParse(fields[5].Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out reading))
                {
                    file.RejectedLines.Add(string.Format("{0}: invalid reading '{1}'", lineNumber, fields[5]));
                    continue;
                }

                file.Reads.Add(new MeterRead
                {
                    Route = fields[0].Trim(),
                    MeterSerial = fields[2].Trim().ToUpperInvariant(),
                    ReadDate = date.Date,
                    Reading = reading,
                    ReadType = ReadType.Actual,
                    Source = ReadSource.Handheld,
                    ReaderCode = code.Length == 0 ? "00" : code,
                    ReaderId = fields[7].Trim()
                });
            }

            if (!headerSeen)
            {
                throw new ReadFileException("File is empty", 0);
            }

            return file;
        }
    }
}
