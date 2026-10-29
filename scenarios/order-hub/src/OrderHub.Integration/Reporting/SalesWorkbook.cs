using System;
using System.Collections.Generic;
using System.Linq;
using Litware.OrderHub.Integration.Data;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace Litware.OrderHub.Integration.Reporting
{
    /// <summary>Daily sales workbook for the sales and finance teams.</summary>
    public static class SalesWorkbook
    {
        public static string FileName(DateTime day)
        {
            return "sales-" + day.ToString("yyyy-MM-dd") + ".xlsx";
        }

        public static byte[] Build(DateTime day, IList<SalesLine> lines)
        {
            using (var package = new ExcelPackage())
            {
                package.Workbook.Properties.Title = "Order Hub sales " + day.ToString("yyyy-MM-dd");
                package.Workbook.Properties.Company = "Litware Outdoor";

                var invoices = package.Workbook.Worksheets.Add("Invoice lines");
                string[] headers = { "Invoice", "Order", "Channel", "Invoiced (UTC)", "Country", "Currency", "SKU", "Description", "Qty", "Unit price", "Amount" };
                for (int c = 0; c < headers.Length; c++) invoices.Cells[1, c + 1].Value = headers[c];

                int row = 2;
                foreach (var line in lines)
                {
                    invoices.Cells[row, 1].Value = line.InvoiceNumber;
                    invoices.Cells[row, 2].Value = line.OrderNumber;
                    invoices.Cells[row, 3].Value = line.Channel;
                    invoices.Cells[row, 4].Value = line.InvoicedUtc;
                    invoices.Cells[row, 5].Value = line.ShipCountry;
                    invoices.Cells[row, 6].Value = line.Currency;
                    invoices.Cells[row, 7].Value = line.Sku;
                    invoices.Cells[row, 8].Value = line.Description;
                    invoices.Cells[row, 9].Value = line.Quantity;
                    invoices.Cells[row, 10].Value = line.UnitPrice;
                    invoices.Cells[row, 11].Value = line.LineAmount;
                    row++;
                }

                int last = Math.Max(row - 1, 1);
                FormatHeader(invoices.Cells[1, 1, 1, headers.Length]);
                invoices.Cells[2, 4, last, 4].Style.Numberformat.Format = "yyyy-mm-dd hh:mm";
                invoices.Cells[2, 10, last, 11].Style.Numberformat.Format = "#,##0.00";
                if (lines.Count > 0)
                {
                    invoices.Cells[1, 1, last, headers.Length].AutoFilter = true;
                    invoices.Cells[row, 8].Value = "Total";
                    invoices.Cells[row, 9].Formula = "SUM(I2:I" + last + ")";
                    invoices.Cells[row, 11].Formula = "SUM(K2:K" + last + ")";
                    invoices.Cells[row, 11].Style.Numberformat.Format = "#,##0.00";
                    invoices.Cells[row, 1, row, headers.Length].Style.Font.Bold = true;
                }
                invoices.View.FreezePanes(2, 1);

                AddSummary(package, "By SKU", "SKU", lines.GroupBy(l => l.Sku + "\t" + l.Description), true);
                AddSummary(package, "By channel", "Channel", lines.GroupBy(l => l.Channel), false);

                foreach (var sheet in package.Workbook.Worksheets)
                {
                    if (sheet.Dimension != null) sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                }
                return package.GetAsByteArray();
            }
        }

        private static void AddSummary(ExcelPackage package, string name, string keyHeader, IEnumerable<IGrouping<string, SalesLine>> groups, bool withDescription)
        {
            var sheet = package.Workbook.Worksheets.Add(name);
            int col = 1;
            sheet.Cells[1, col++].Value = keyHeader;
            if (withDescription) sheet.Cells[1, col++].Value = "Description";
            sheet.Cells[1, col++].Value = "Orders";
            sheet.Cells[1, col++].Value = "Qty";
            sheet.Cells[1, col].Value = "Amount";
            FormatHeader(sheet.Cells[1, 1, 1, col]);

            int row = 2;
            foreach (var g in groups.OrderByDescending(g => g.Sum(l => l.LineAmount)))
            {
                var parts = g.Key.Split('\t');
                int c = 1;
                sheet.Cells[row, c++].Value = parts[0];
                if (withDescription) sheet.Cells[row, c++].Value = parts.Length > 1 ? parts[1] : null;
                sheet.Cells[row, c++].Value = g.Select(l => l.OrderNumber).Distinct().Count();
                sheet.Cells[row, c++].Value = g.Sum(l => l.Quantity);
                sheet.Cells[row, c].Value = g.Sum(l => l.LineAmount);
                sheet.Cells[row, c].Style.Numberformat.Format = "#,##0.00";
                row++;
            }
        }

        private static void FormatHeader(ExcelRange header)
        {
            header.Style.Font.Bold = true;
            header.Style.Fill.PatternType = ExcelFillStyle.Solid;
            header.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0xDD, 0xEB, 0xF7));
        }
    }
}
