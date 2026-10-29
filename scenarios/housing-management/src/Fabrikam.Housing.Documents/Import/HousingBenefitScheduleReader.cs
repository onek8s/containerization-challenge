using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Fabrikam.Housing.Common;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Fabrikam.Housing.Documents.Import
{
    /// <summary>
    /// Reads Bristol City Council's landlord payment schedule. The sheet starts with a few title rows
    /// (council name, payment run, BACS date); the column header row contains "Claim Ref".
    /// Older schedules are .xls, newer ones .xlsx.
    /// </summary>
    public class HousingBenefitScheduleReader : IHousingBenefitScheduleReader
    {
        private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("en-GB");

        public IList<HousingBenefitLine> Read(Stream content, string fileName)
        {
            IWorkbook workbook = fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                ? (IWorkbook)new XSSFWorkbook(content)
                : new HSSFWorkbook(content);
            var sheet = workbook.GetSheetAt(0);
            var formatter = new DataFormatter(Uk);

            int headerRow = -1;
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var r = sheet.FirstRowNum; r <= Math.Min(sheet.LastRowNum, 20); r++)
            {
                var row = sheet.GetRow(r);
                if (row == null) continue;
                var cells = row.Cells.Select(c => (c.ColumnIndex, Text: formatter.FormatCellValue(c).Trim())).ToList();
                if (!cells.Any(c => c.Text.Equals("Claim Ref", StringComparison.OrdinalIgnoreCase))) continue;
                headerRow = r;
                foreach (var c in cells.Where(c => c.Text.Length > 0)) columns[Normalise(c.Text)] = c.ColumnIndex;
                break;
            }
            if (headerRow < 0) throw new InvalidDataException("No 'Claim Ref' header row found in " + fileName);

            int Col(params string[] names)
            {
                foreach (var n in names)
                    if (columns.TryGetValue(Normalise(n), out var i)) return i;
                throw new InvalidDataException($"Column '{names[0]}' not found in {fileName}");
            }

            var claim = Col("Claim Ref");
            var name = Col("Claimant", "Claimant Name", "Name");
            var address = Col("Address", "Address Line 1", "Property Address");
            var postcode = Col("Postcode", "Post Code");
            var from = Col("Period From", "From");
            var to = Col("Period To", "To");
            var amount = Col("Amount", "Amount Paid", "Payment");

            var lines = new List<HousingBenefitLine>();
            for (var r = headerRow + 1; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row == null) continue;
                var claimRef = Text(row, claim, formatter);
                if (string.IsNullOrWhiteSpace(claimRef) || claimRef.StartsWith("Total", StringComparison.OrdinalIgnoreCase)) continue;
                lines.Add(new HousingBenefitLine
                {
                    RowNumber = r + 1,
                    ClaimReference = claimRef,
                    ClaimantName = Text(row, name, formatter),
                    AddressLine1 = Text(row, address, formatter),
                    Postcode = Postcode.TryNormalise(Text(row, postcode, formatter), out var pc) ? pc : Text(row, postcode, formatter),
                    PeriodFrom = Date(row.GetCell(from), formatter, r),
                    PeriodTo = Date(row.GetCell(to), formatter, r),
                    Amount = Amount(row.GetCell(amount), formatter, r)
                });
            }
            return lines;
        }

        private static string Normalise(string header) => new string(header.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        private static string Text(IRow row, int col, DataFormatter formatter)
        {
            var cell = row.GetCell(col);
            return cell == null ? "" : formatter.FormatCellValue(cell).Trim();
        }

        private static DateTime Date(ICell cell, DataFormatter formatter, int r)
        {
            if (cell == null) throw new InvalidDataException($"Row {r + 1}: date missing");
            if (cell.CellType == CellType.Numeric) return DateUtil.GetJavaDate(cell.NumericCellValue).Date;
            var text = formatter.FormatCellValue(cell).Trim();
            if (DateTime.TryParseExact(text, new[] { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "dd-MMM-yyyy" }, Uk, DateTimeStyles.None, out var d)) return d;
            throw new InvalidDataException($"Row {r + 1}: '{text}' is not a date");
        }

        private static decimal Amount(ICell cell, DataFormatter formatter, int r)
        {
            if (cell == null) throw new InvalidDataException($"Row {r + 1}: amount missing");
            if (cell.CellType == CellType.Numeric || cell.CellType == CellType.Formula && cell.CachedFormulaResultType == CellType.Numeric)
                return Math.Round((decimal)cell.NumericCellValue, 2);
            var text = formatter.FormatCellValue(cell).Replace("£", "").Replace(",", "").Trim();
            var negative = text.StartsWith("(") && text.EndsWith(")");
            text = text.Trim('(', ')');
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                throw new InvalidDataException($"Row {r + 1}: '{text}' is not an amount");
            return negative ? -value : value;
        }
    }
}
