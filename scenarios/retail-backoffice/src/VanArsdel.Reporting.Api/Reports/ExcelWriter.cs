using ClosedXML.Excel;

namespace VanArsdel.Reporting.Api.Reports;

public static class ExcelWriter
{
    public static void Write(Stream output, string title, DateTime businessDate, IEnumerable<ReportTable> tables)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Author = "VanArsdel Reporting";
        workbook.Properties.Title = title;

        foreach (var table in tables)
        {
            // Sheet names: max 31 characters, no slashes.
            var sheet = workbook.Worksheets.Add(new string(table.Sheet.Replace('/', '-').Take(31).ToArray()));
            sheet.Cell(1, 1).Value = title;
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 14;
            sheet.Cell(2, 1).Value = "Business date " + businessDate.ToString("yyyy-MM-dd") + ", generated " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            sheet.Cell(2, 1).Style.Font.Italic = true;

            const int headerRow = 4;
            for (var c = 0; c < table.Columns.Count; c++)
            {
                var cell = sheet.Cell(headerRow, c + 1);
                cell.Value = table.Columns[c].Title;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F3864");
                cell.Style.Font.FontColor = XLColor.White;
                // Fixed widths; AdjustToContents needs GDI+ which the servers do not have for services.
                sheet.Column(c + 1).Width = table.Columns[c].Width;
            }

            var row = headerRow + 1;
            foreach (var values in table.Rows)
            {
                WriteRow(sheet, row++, table, values);
            }
            if (table.Totals != null)
            {
                WriteRow(sheet, row, table, table.Totals);
                sheet.Row(row).Style.Font.Bold = true;
                sheet.Row(row).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }
            if (table.Rows.Count > 0)
            {
                sheet.Range(headerRow, 1, headerRow + table.Rows.Count, table.Columns.Count).SetAutoFilter();
            }
            sheet.SheetView.FreezeRows(headerRow);
        }
        workbook.SaveAs(output);
    }

    private static void WriteRow(IXLWorksheet sheet, int row, ReportTable table, object[] values)
    {
        for (var c = 0; c < values.Length && c < table.Columns.Count; c++)
        {
            var cell = sheet.Cell(row, c + 1);
            switch (values[c])
            {
                case null:
                    break;
                case decimal d:
                    cell.Value = d;
                    break;
                case int i:
                    cell.Value = i;
                    break;
                case DateTime dt:
                    cell.Value = dt;
                    break;
                default:
                    cell.Value = values[c].ToString();
                    break;
            }
            if (table.Columns[c].Format != null)
            {
                cell.Style.NumberFormat.Format = table.Columns[c].Format;
            }
        }
    }
}
