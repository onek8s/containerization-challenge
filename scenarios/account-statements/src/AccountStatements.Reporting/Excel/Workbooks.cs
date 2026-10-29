using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Woodgrove.Statements.Core.Model;
using Woodgrove.Statements.Core.Statements;

namespace Woodgrove.Statements.Reporting.Excel
{
    /// <summary>Excel workbooks for credit control and finance (formulas, filters and frozen headers instead of a rendered report).</summary>
    public static class Workbooks
    {
        const string Money = "#,##0.00;[Red]-#,##0.00";
        const string DateFormat = "dd.mm.yyyy";
        static readonly XLColor Navy = XLColor.FromHtml("#1F3864");
        static readonly XLColor Light = XLColor.FromHtml("#DCE6F1");

        public static byte[] AgedDebt(IList<AgedDebtRow> rows, DateTime asOf, string creditController, string companyName)
        {
            using (var wb = NewWorkbook(companyName, "Aged debt"))
            {
                var ws = wb.Worksheets.Add("Aged debt");
                Title(ws, "Aged debt " + asOf.ToString("dd.MM.yyyy") + (string.IsNullOrWhiteSpace(creditController) ? "" : " - " + creditController), companyName);
                var headers = new[] { "Credit controller", "Customer", "Name", "On hold", "Credit limit", "Not due", "1-30", "31-60", "61-90", "Over 90",
                                      "Unallocated", "Total", "Over limit", "Level", "Last payment", "Last payment amount" };
                HeaderRow(ws, 4, headers);
                var r = 5;
                foreach (var row in rows)
                {
                    ws.Cell(r, 1).Value = row.CreditController;
                    ws.Cell(r, 2).Value = row.CustomerNo;
                    ws.Cell(r, 3).Value = row.CustomerName;
                    ws.Cell(r, 4).Value = row.OnHold ? "Yes" : "";
                    ws.Cell(r, 5).Value = row.CreditLimit;
                    ws.Cell(r, 6).Value = row.NotDue;
                    ws.Cell(r, 7).Value = row.Days1To30;
                    ws.Cell(r, 8).Value = row.Days31To60;
                    ws.Cell(r, 9).Value = row.Days61To90;
                    ws.Cell(r, 10).Value = row.Over90;
                    ws.Cell(r, 11).Value = row.Unallocated;
                    ws.Cell(r, 12).FormulaA1 = string.Format("SUM(F{0}:K{0})", r);
                    ws.Cell(r, 13).FormulaA1 = string.Format("IF(AND(E{0}>0,L{0}>E{0}),L{0}-E{0},0)", r);
                    ws.Cell(r, 14).Value = row.DunningLevel;
                    if (row.LastPaymentDate.HasValue) ws.Cell(r, 15).Value = row.LastPaymentDate.Value;
                    ws.Cell(r, 16).Value = row.LastPaymentAmount;
                    r++;
                }
                // Keep an empty row when there is no data so the total row never sums itself.
                if (r == 5) r = 6;
                var last = r - 1;
                ws.Cell(r, 1).Value = "Total";
                for (var col = 5; col <= 13; col++)
                    ws.Cell(r, col).FormulaA1 = string.Format("SUBTOTAL(9,{0}5:{0}{1})", ColumnLetter(col), last);
                ws.Cell(r, 16).FormulaA1 = string.Format("SUBTOTAL(9,P5:P{0})", last);
                var total = ws.Range(r, 1, r, headers.Length);
                total.Style.Font.Bold = true;
                total.Style.Fill.BackgroundColor = Light;
                total.Style.Border.TopBorder = XLBorderStyleValues.Thin;

                ws.Range(5, 5, r, 13).Style.NumberFormat.Format = Money;
                ws.Range(5, 16, r, 16).Style.NumberFormat.Format = Money;
                ws.Range(5, 15, r, 15).Style.NumberFormat.Format = DateFormat;
                if (rows.Count > 0)
                {
                    ws.Range(5, 9, last, 10).AddConditionalFormat().WhenGreaterThan(0).Font.SetFontColor(XLColor.FromHtml("#C00000")).Font.SetBold();
                    ws.Range(5, 13, last, 13).AddConditionalFormat().WhenGreaterThan(0).Fill.SetBackgroundColor(XLColor.FromHtml("#FCE4D6"));
                }
                ws.Range(4, 1, last, headers.Length).SetAutoFilter();
                Finish(ws, 4, new Dictionary<int, double> { { 1, 18 }, { 2, 10 }, { 3, 36 } });

                var summary = wb.Worksheets.Add("By controller");
                Title(summary, "Aged debt by credit controller " + asOf.ToString("dd.MM.yyyy"), companyName);
                HeaderRow(summary, 4, new[] { "Credit controller", "Customers", "Not due", "Overdue", "Over 90", "Total", "Overdue %" });
                var sr = 5;
                foreach (var g in rows.GroupBy(x => x.CreditController).OrderBy(x => x.Key))
                {
                    summary.Cell(sr, 1).Value = g.Key;
                    summary.Cell(sr, 2).Value = g.Count();
                    summary.Cell(sr, 3).FormulaA1 = string.Format("SUMIF('Aged debt'!A$5:A${0},A{1},'Aged debt'!F$5:F${0})", last, sr);
                    summary.Cell(sr, 4).FormulaA1 = string.Format("SUMIF('Aged debt'!A$5:A${0},A{1},'Aged debt'!G$5:G${0})+SUMIF('Aged debt'!A$5:A${0},A{1},'Aged debt'!H$5:H${0})" +
                                                                  "+SUMIF('Aged debt'!A$5:A${0},A{1},'Aged debt'!I$5:I${0})+SUMIF('Aged debt'!A$5:A${0},A{1},'Aged debt'!J$5:J${0})", last, sr);
                    summary.Cell(sr, 5).FormulaA1 = string.Format("SUMIF('Aged debt'!A$5:A${0},A{1},'Aged debt'!J$5:J${0})", last, sr);
                    summary.Cell(sr, 6).FormulaA1 = string.Format("SUMIF('Aged debt'!A$5:A${0},A{1},'Aged debt'!L$5:L${0})", last, sr);
                    summary.Cell(sr, 7).FormulaA1 = string.Format("IF(F{0}=0,0,D{0}/F{0})", sr);
                    sr++;
                }
                summary.Range(5, 3, Math.Max(5, sr), 6).Style.NumberFormat.Format = Money;
                summary.Range(5, 7, Math.Max(5, sr), 7).Style.NumberFormat.Format = "0.0%";
                Finish(summary, 4, new Dictionary<int, double> { { 1, 22 } });
                return Save(wb);
            }
        }

        public static byte[] Portfolio(IList<PortfolioRow> rows, DateTime asOf, string companyName)
        {
            using (var wb = NewWorkbook(companyName, "Lease portfolio"))
            {
                var ws = wb.Worksheets.Add("Contracts");
                Title(ws, "Active lease contracts " + asOf.ToString("dd.MM.yyyy"), companyName);
                var headers = new[] { "Asset category", "Contract", "Customer", "Name", "Start", "End", "Monthly rent", "Asset value", "Residual value",
                                      "Months left", "Remaining rent", "Ends in" };
                HeaderRow(ws, 4, headers);
                var r = 5;
                foreach (var row in rows)
                {
                    ws.Cell(r, 1).Value = row.AssetCategory;
                    ws.Cell(r, 2).Value = row.ContractNo;
                    ws.Cell(r, 3).Value = row.CustomerNo;
                    ws.Cell(r, 4).Value = row.CustomerName;
                    ws.Cell(r, 5).Value = row.StartDate;
                    ws.Cell(r, 6).Value = row.EndDate;
                    ws.Cell(r, 7).Value = row.MonthlyRent;
                    ws.Cell(r, 8).Value = row.AssetValue;
                    ws.Cell(r, 9).Value = row.ResidualValue;
                    ws.Cell(r, 10).Value = row.RemainingMonths;
                    ws.Cell(r, 11).FormulaA1 = string.Format("G{0}*J{0}", r);
                    ws.Cell(r, 12).Value = row.EndBand;
                    r++;
                }
                var last = Math.Max(5, r - 1);
                if (rows.Count > 0)
                {
                    var table = ws.Range(4, 1, last, headers.Length).CreateTable("Contracts");
                    table.Theme = XLTableTheme.TableStyleLight9;
                    table.ShowTotalsRow = true;
                    table.Field("Monthly rent").TotalsRowFunction = XLTotalsRowFunction.Sum;
                    table.Field("Asset value").TotalsRowFunction = XLTotalsRowFunction.Sum;
                    table.Field("Residual value").TotalsRowFunction = XLTotalsRowFunction.Sum;
                    table.Field("Remaining rent").TotalsRowFunction = XLTotalsRowFunction.Sum;
                    table.Field("Asset category").TotalsRowLabel = "Total";
                }
                ws.Range(5, 5, last + 1, 6).Style.NumberFormat.Format = DateFormat;
                ws.Range(5, 7, last + 1, 9).Style.NumberFormat.Format = Money;
                ws.Range(5, 11, last + 1, 11).Style.NumberFormat.Format = Money;
                Finish(ws, 4, new Dictionary<int, double> { { 1, 24 }, { 4, 34 } });

                var cat = wb.Worksheets.Add("By category");
                Title(cat, "Portfolio by asset category " + asOf.ToString("dd.MM.yyyy"), companyName);
                HeaderRow(cat, 4, new[] { "Asset category", "Contracts", "Monthly rent", "Remaining rent", "Share" });
                var cr = 5;
                var categories = rows.Select(x => x.AssetCategory).Distinct().OrderBy(x => x).ToList();
                foreach (var category in categories)
                {
                    cat.Cell(cr, 1).Value = category;
                    cat.Cell(cr, 2).FormulaA1 = string.Format("COUNTIF(Contracts!A$5:A${0},A{1})", last, cr);
                    cat.Cell(cr, 3).FormulaA1 = string.Format("SUMIF(Contracts!A$5:A${0},A{1},Contracts!G$5:G${0})", last, cr);
                    cat.Cell(cr, 4).FormulaA1 = string.Format("SUMIF(Contracts!A$5:A${0},A{1},Contracts!K$5:K${0})", last, cr);
                    cr++;
                }
                var catLast = Math.Max(5, cr - 1);
                for (var i = 5; i < cr; i++)
                    cat.Cell(i, 5).FormulaA1 = string.Format("IF(SUM(D$5:D${0})=0,0,D{1}/SUM(D$5:D${0}))", catLast, i);
                cat.Range(5, 3, catLast, 4).Style.NumberFormat.Format = Money;
                cat.Range(5, 5, catLast, 5).Style.NumberFormat.Format = "0.0%";
                Finish(cat, 4, new Dictionary<int, double> { { 1, 26 } });
                return Save(wb);
            }
        }

        public static byte[] Transactions(IList<TransactionRow> rows, DateTime from, DateTime to, string companyName)
        {
            using (var wb = NewWorkbook(companyName, "AR transactions"))
            {
                var ws = wb.Worksheets.Add("Transactions");
                Title(ws, "Ledger transactions " + from.ToString("dd.MM.yyyy") + (to > from ? " - " + to.ToString("dd.MM.yyyy") : ""), companyName);
                var headers = new[] { "Posting date", "Customer", "Name", "Credit controller", "Type", "Document", "Contract", "Due date", "Description",
                                      "Amount", "Open amount", "Reference" };
                HeaderRow(ws, 4, headers);
                var r = 5;
                foreach (var row in rows)
                {
                    ws.Cell(r, 1).Value = row.PostingDate;
                    ws.Cell(r, 2).Value = row.CustomerNo;
                    ws.Cell(r, 3).Value = row.CustomerName;
                    ws.Cell(r, 4).Value = row.CreditController;
                    ws.Cell(r, 5).Value = row.EntryType;
                    // Document and reference numbers are text: keep leading zeros and stop Excel from showing them as numbers.
                    ws.Cell(r, 6).SetValue(row.DocumentNo ?? "");
                    ws.Cell(r, 6).Style.NumberFormat.Format = "@";
                    ws.Cell(r, 7).Value = row.ContractNo;
                    if (row.DueDate.HasValue) ws.Cell(r, 8).Value = row.DueDate.Value;
                    ws.Cell(r, 9).Value = row.Description;
                    ws.Cell(r, 10).Value = row.Amount;
                    ws.Cell(r, 11).Value = row.OpenAmount;
                    ws.Cell(r, 12).SetValue(row.PaymentReference ?? "");
                    ws.Cell(r, 12).Style.NumberFormat.Format = "@";
                    r++;
                }
                if (r == 5) r = 6;
                var last = r - 1;
                ws.Cell(r, 9).Value = "Total";
                ws.Cell(r, 10).FormulaA1 = string.Format("SUBTOTAL(9,J5:J{0})", last);
                ws.Cell(r, 11).FormulaA1 = string.Format("SUBTOTAL(9,K5:K{0})", last);
                ws.Range(r, 1, r, headers.Length).Style.Font.Bold = true;
                ws.Range(5, 1, r, 1).Style.NumberFormat.Format = DateFormat;
                ws.Range(5, 8, r, 8).Style.NumberFormat.Format = DateFormat;
                ws.Range(5, 10, r, 11).Style.NumberFormat.Format = Money;
                ws.Range(4, 1, last, headers.Length).SetAutoFilter();
                Finish(ws, 4, new Dictionary<int, double> { { 3, 34 }, { 9, 40 } });

                var types = wb.Worksheets.Add("Summary");
                Title(types, "Totals by entry type", companyName);
                HeaderRow(types, 4, new[] { "Type", "Entries", "Amount" });
                var tr = 5;
                foreach (var type in new[] { "INV", "CRN", "PAY", "ADJ", "INT" })
                {
                    types.Cell(tr, 1).Value = type;
                    types.Cell(tr, 2).FormulaA1 = string.Format("COUNTIF(Transactions!E$5:E${0},A{1})", last, tr);
                    types.Cell(tr, 3).FormulaA1 = string.Format("SUMIF(Transactions!E$5:E${0},A{1},Transactions!J$5:J${0})", last, tr);
                    tr++;
                }
                types.Range(5, 3, tr, 3).Style.NumberFormat.Format = Money;
                Finish(types, 4, null);
                return Save(wb);
            }
        }

        static XLWorkbook NewWorkbook(string companyName, string title)
        {
            var wb = new XLWorkbook();
            wb.Properties.Author = companyName;
            wb.Properties.Company = companyName;
            wb.Properties.Title = title;
            wb.Properties.Created = DateTime.Now;
            wb.Style.Font.FontName = "Segoe UI";
            wb.Style.Font.FontSize = 9;
            return wb;
        }

        static void Title(IXLWorksheet ws, string title, string companyName)
        {
            ws.Cell(1, 1).Value = title;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontColor = Navy;
            ws.Cell(2, 1).Value = companyName + ", created " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#595959");
        }

        static void HeaderRow(IXLWorksheet ws, int row, string[] headers)
        {
            for (var i = 0; i < headers.Length; i++)
                ws.Cell(row, i + 1).Value = headers[i];
            var range = ws.Range(row, 1, row, headers.Length);
            range.Style.Font.Bold = true;
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Fill.BackgroundColor = Navy;
            range.Style.Alignment.WrapText = true;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(row).Height = 28;
        }

        static void Finish(IXLWorksheet ws, int headerRow, IDictionary<int, double> fixedWidths)
        {
            ws.SheetView.FreezeRows(headerRow);
            ws.Columns().AdjustToContents(headerRow, 12d, 40d);
            if (fixedWidths != null)
                foreach (var w in fixedWidths) ws.Column(w.Key).Width = w.Value;
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.SetRowsToRepeatAtTop(headerRow, headerRow);
        }

        static string ColumnLetter(int column)
        {
            var letters = "";
            while (column > 0)
            {
                var m = (column - 1) % 26;
                letters = (char)('A' + m) + letters;
                column = (column - m) / 26;
            }
            return letters;
        }

        static byte[] Save(XLWorkbook wb)
        {
            using (var ms = new MemoryStream())
            {
                wb.SaveAs(ms);
                return ms.ToArray();
            }
        }
    }
}
