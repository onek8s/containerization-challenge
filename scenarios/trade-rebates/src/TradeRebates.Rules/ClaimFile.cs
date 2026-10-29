using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ContosoFoods.TradeRebates.Rules
{
    public sealed class ClaimFileLine
    {
        public int LineNumber { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string CustomerNo { get; set; } = string.Empty;
        public string Gtin { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal NetAmount { get; set; }
    }

    public sealed class ClaimFileError
    {
        public ClaimFileError(int lineNumber, string message)
        {
            LineNumber = lineNumber;
            Message = message;
        }

        public int LineNumber { get; }
        public string Message { get; }
    }

    public sealed class ClaimFile
    {
        public string Format { get; set; } = string.Empty;
        public string DistributorCode { get; set; } = string.Empty;
        public int Period { get; set; }
        public List<ClaimFileLine> Lines { get; } = new List<ClaimFileLine>();
        public List<ClaimFileError> Errors { get; } = new List<ClaimFileError>();

        public decimal TotalQuantity
        {
            get
            {
                decimal total = 0m;
                foreach (ClaimFileLine line in Lines)
                    total += line.Quantity;
                return total;
            }
        }

        public decimal TotalAmount
        {
            get
            {
                decimal total = 0m;
                foreach (ClaimFileLine line in Lines)
                    total += line.NetAmount;
                return total;
            }
        }
    }

    /// <summary>
    /// Reads distributor sell-out files from the drop folder. Two layouts are accepted:
    /// <list type="bullet">
    /// <item>CSV with a header row, semicolon separated (DistributorCode;Period;InvoiceNo;InvoiceDate;CustomerNo;Gtin;Quantity;NetAmount).</item>
    /// <item>SO fixed-width records H/D/T written by older distributor ERPs (code page 850, amounts in cents).</item>
    /// </list>
    /// </summary>
    public static class ClaimFileParser
    {
        public const string CsvHeader = "DistributorCode;Period;InvoiceNo;InvoiceDate;CustomerNo;Gtin;Quantity;NetAmount";

        public static ClaimFile Parse(TextReader reader)
        {
            var lines = new List<string>();
            string? text;
            while ((text = reader.ReadLine()) != null)
                lines.Add(text);

            int first = 0;
            while (first < lines.Count && lines[first].Trim().Length == 0)
                first++;

            if (first == lines.Count)
            {
                var empty = new ClaimFile { Format = "EMPTY" };
                empty.Errors.Add(new ClaimFileError(0, "File is empty."));
                return empty;
            }

            string head = lines[first].TrimStart('﻿');
            if (head.StartsWith("DistributorCode;", StringComparison.OrdinalIgnoreCase))
                return ParseCsv(lines, first);
            if (head.StartsWith("H", StringComparison.Ordinal) && head.Length >= 15)
                return ParseFixed(lines, first);

            var unknown = new ClaimFile { Format = "UNKNOWN" };
            unknown.Errors.Add(new ClaimFileError(first + 1, "Unrecognised file layout."));
            return unknown;
        }

        private static ClaimFile ParseCsv(List<string> lines, int headerIndex)
        {
            var file = new ClaimFile { Format = "CSV" };
            string[] header = lines[headerIndex].TrimStart('﻿').Split(';');
            if (header.Length < 8)
            {
                file.Errors.Add(new ClaimFileError(headerIndex + 1, "Header must have 8 columns: " + CsvHeader));
                return file;
            }

            for (int i = headerIndex + 1; i < lines.Count; i++)
            {
                int lineNo = i + 1;
                string row = lines[i];
                if (row.Trim().Length == 0)
                    continue;

                string[] f = row.Split(';');
                if (f.Length < 8)
                {
                    file.Errors.Add(new ClaimFileError(lineNo, "Expected 8 columns, found " + f.Length + "."));
                    continue;
                }

                string distributor = f[0].Trim();
                if (file.DistributorCode.Length == 0)
                    file.DistributorCode = distributor;
                else if (!string.Equals(file.DistributorCode, distributor, StringComparison.OrdinalIgnoreCase))
                {
                    file.Errors.Add(new ClaimFileError(lineNo, "Distributor code changes within the file."));
                    continue;
                }

                if (!int.TryParse(f[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int period) || !IsValidPeriod(period))
                {
                    file.Errors.Add(new ClaimFileError(lineNo, "Period must be yyyyMM."));
                    continue;
                }
                if (file.Period == 0)
                    file.Period = period;
                else if (file.Period != period)
                {
                    file.Errors.Add(new ClaimFileError(lineNo, "Period changes within the file."));
                    continue;
                }

                if (!TryParseDate(f[3], out DateTime invoiceDate))
                {
                    file.Errors.Add(new ClaimFileError(lineNo, "Invoice date '" + f[3].Trim() + "' is not valid."));
                    continue;
                }
                if (!TryParseAmount(f[6], out decimal qty))
                {
                    file.Errors.Add(new ClaimFileError(lineNo, "Quantity '" + f[6].Trim() + "' is not a number."));
                    continue;
                }
                if (!TryParseAmount(f[7], out decimal amount))
                {
                    file.Errors.Add(new ClaimFileError(lineNo, "Net amount '" + f[7].Trim() + "' is not a number."));
                    continue;
                }

                AddLine(file, lineNo, f[2], invoiceDate, f[4], f[5], qty, amount);
            }

            return file;
        }

        private static ClaimFile ParseFixed(List<string> lines, int headerIndex)
        {
            var file = new ClaimFile { Format = "SO" };
            string h = lines[headerIndex];
            file.DistributorCode = Slice(h, 1, 8).Trim();
            if (!int.TryParse(Slice(h, 9, 6), NumberStyles.None, CultureInfo.InvariantCulture, out int period) || !IsValidPeriod(period))
            {
                file.Errors.Add(new ClaimFileError(headerIndex + 1, "Header period must be yyyyMM."));
                return file;
            }
            file.Period = period;

            bool trailerSeen = false;
            for (int i = headerIndex + 1; i < lines.Count; i++)
            {
                int lineNo = i + 1;
                string r = lines[i];
                if (r.Trim().Length == 0)
                    continue;
                if (trailerSeen)
                {
                    file.Errors.Add(new ClaimFileError(lineNo, "Data after trailer record."));
                    break;
                }

                char type = r[0];
                if (type == 'D')
                {
                    if (r.Length < 66)
                    {
                        file.Errors.Add(new ClaimFileError(lineNo, "Detail record is shorter than 66 characters."));
                        continue;
                    }
                    if (!DateTime.TryParseExact(Slice(r, 13, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                    {
                        file.Errors.Add(new ClaimFileError(lineNo, "Invoice date is not yyyyMMdd."));
                        continue;
                    }
                    if (!long.TryParse(Slice(r, 45, 9).Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long qty))
                    {
                        file.Errors.Add(new ClaimFileError(lineNo, "Quantity is not numeric."));
                        continue;
                    }
                    if (!long.TryParse(Slice(r, 54, 12).Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long cents))
                    {
                        file.Errors.Add(new ClaimFileError(lineNo, "Net amount is not numeric."));
                        continue;
                    }
                    AddLine(file, lineNo, Slice(r, 1, 12), date, Slice(r, 21, 10), Slice(r, 31, 14), qty, cents / 100m);
                }
                else if (type == 'T')
                {
                    trailerSeen = true;
                    if (!int.TryParse(Slice(r, 1, 7), NumberStyles.None, CultureInfo.InvariantCulture, out int count))
                    {
                        file.Errors.Add(new ClaimFileError(lineNo, "Trailer record count is not numeric."));
                        continue;
                    }
                    long.TryParse(Slice(r, 8, 14).Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long totalCents);
                    int expected = file.Lines.Count + CountDetailErrors(file);
                    if (count != expected)
                        file.Errors.Add(new ClaimFileError(lineNo, "Trailer says " + count + " detail records, file has " + expected + "."));
                    else if (CountDetailErrors(file) == 0 && totalCents / 100m != file.TotalAmount)
                        file.Errors.Add(new ClaimFileError(lineNo, "Trailer total does not match detail records."));
                }
                else
                {
                    file.Errors.Add(new ClaimFileError(lineNo, "Unknown record type '" + type + "'."));
                }
            }

            if (!trailerSeen)
                file.Errors.Add(new ClaimFileError(lines.Count, "Trailer record missing."));
            return file;
        }

        private static int CountDetailErrors(ClaimFile file)
        {
            // Every error before the trailer belongs to a detail record.
            return file.Errors.Count;
        }

        private static void AddLine(ClaimFile file, int lineNo, string invoiceNo, DateTime invoiceDate, string customerNo, string gtin, decimal qty, decimal amount)
        {
            string g = gtin.Trim();
            if (!Gtin.IsValid(g))
            {
                file.Errors.Add(new ClaimFileError(lineNo, "GTIN '" + g + "' has an invalid check digit."));
                return;
            }
            if (invoiceNo.Trim().Length == 0)
            {
                file.Errors.Add(new ClaimFileError(lineNo, "Invoice number missing."));
                return;
            }

            file.Lines.Add(new ClaimFileLine
            {
                LineNumber = lineNo,
                InvoiceNo = invoiceNo.Trim(),
                InvoiceDate = invoiceDate,
                CustomerNo = customerNo.Trim(),
                Gtin = Gtin.ToGtin14(g),
                Quantity = qty,
                NetAmount = amount
            });
        }

        public static bool IsValidPeriod(int period)
        {
            int year = period / 100;
            int month = period % 100;
            return year >= 2000 && year <= 2099 && month >= 1 && month <= 12;
        }

        private static bool TryParseDate(string text, out DateTime date)
        {
            string[] formats = { "yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "yyyyMMdd" };
            return DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        private static bool TryParseAmount(string text, out decimal value)
        {
            string t = text.Trim();
            if (t.IndexOf(',') >= 0 && t.IndexOf('.') < 0)
                t = t.Replace(',', '.');
            return decimal.TryParse(t, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
        }

        private static string Slice(string s, int start, int length)
        {
            if (start >= s.Length)
                return string.Empty;
            return s.Substring(start, Math.Min(length, s.Length - start));
        }
    }
}
