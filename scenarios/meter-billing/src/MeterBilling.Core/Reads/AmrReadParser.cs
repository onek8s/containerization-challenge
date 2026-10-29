using System;
using System.Collections.Generic;
using System.Globalization;

namespace Coho.Billing.Core.Reads
{
    /// <summary>
    /// Drive-by AMR collection file (fixed width, one record per line).
    /// H COHOAMR yyyyMMddHHmmss batch(10)
    /// D serial(12) yyyyMMdd reading(10, one implied decimal) status(2) rssi(3)
    /// T count(8)
    /// </summary>
    public class AmrReadParser : IReadFileParser
    {
        public bool CanParse(string fileName, string firstLine)
        {
            return firstLine != null && firstLine.StartsWith("HCOHOAMR", StringComparison.Ordinal);
        }

        public ReadFile Parse(string fileName, IEnumerable<string> lines)
        {
            var file = new ReadFile { FileName = fileName, Source = ReadSource.Amr };
            var lineNumber = 0;
            var detailCount = 0;
            var trailerSeen = false;

            foreach (var line in lines)
            {
                lineNumber++;
                if (line.Length == 0)
                {
                    continue;
                }

                if (trailerSeen)
                {
                    throw new ReadFileException("Data after trailer record", lineNumber);
                }

                switch (line[0])
                {
                    case 'H':
                        if (lineNumber != 1 || line.Length < 32)
                        {
                            throw new ReadFileException("Invalid header record", lineNumber);
                        }

                        file.BatchId = line.Substring(22, 10).Trim();
                        break;

                    case 'D':
                        detailCount++;
                        ParseDetail(file, line, lineNumber);
                        break;

                    case 'T':
                        int expected;
                        if (line.Length < 9 || !int.TryParse(line.Substring(1, 8), NumberStyles.None, CultureInfo.InvariantCulture, out expected))
                        {
                            throw new ReadFileException("Invalid trailer record", lineNumber);
                        }

                        if (expected != detailCount)
                        {
                            throw new ReadFileException(
                                string.Format("Trailer count {0} does not match {1} detail records", expected, detailCount), lineNumber);
                        }

                        trailerSeen = true;
                        break;

                    default:
                        throw new ReadFileException("Unknown record type '" + line[0] + "'", lineNumber);
                }
            }

            if (file.BatchId == null)
            {
                throw new ReadFileException("Header record missing", 0);
            }

            if (!trailerSeen)
            {
                throw new ReadFileException("Trailer record missing, file may be incomplete", lineNumber);
            }

            return file;
        }

        private static void ParseDetail(ReadFile file, string line, int lineNumber)
        {
            if (line.Length < 36)
            {
                file.RejectedLines.Add(string.Format("{0}: record too short", lineNumber));
                return;
            }

            var serial = line.Substring(1, 12).Trim().ToUpperInvariant();
            var status = line.Substring(31, 2);

            DateTime date;
            if (!DateTime.TryParseExact(line.Substring(13, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                file.RejectedLines.Add(string.Format("{0}: meter {1} invalid date", lineNumber, serial));
                return;
            }

            long raw;
            if (!long.TryParse(line.Substring(21, 10), NumberStyles.None, CultureInfo.InvariantCulture, out raw))
            {
                file.RejectedLines.Add(string.Format("{0}: meter {1} invalid reading", lineNumber, serial));
                return;
            }

            if (status == "99")
            {
                file.RejectedLines.Add(string.Format("{0}: meter {1} no response", lineNumber, serial));
                return;
            }

            file.Reads.Add(new MeterRead
            {
                MeterSerial = serial,
                ReadDate = date,
                Reading = raw / 10m,
                ReadType = ReadType.Actual,
                Source = ReadSource.Amr,
                // 01 = tamper flag, 02 = reverse flow, 03 = continuous flow (leak)
                ReaderCode = status == "03" ? "LK" : status == "00" ? "00" : "A" + status
            });
        }
    }
}
