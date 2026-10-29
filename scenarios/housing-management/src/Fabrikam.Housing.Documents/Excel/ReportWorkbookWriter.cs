using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Fabrikam.Housing.Compliance;
using Fabrikam.Housing.Data.Queries;
using Fabrikam.Housing.Repairs;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using OfficeOpenXml;
using OfficeOpenXml.ConditionalFormatting;
using OfficeOpenXml.Style;

namespace Fabrikam.Housing.Documents.Excel
{
    /// <summary>
    /// Management reports as Excel workbooks (EPPlus), and the rent roll as .xls (NPOI) because the
    /// finance month end workbook links to it with external references.
    /// </summary>
    public class ReportWorkbookWriter : IReportWorkbookWriter
    {
        private static readonly Color Brand = Color.FromArgb(0, 87, 107);

        public byte[] Arrears(IList<ArrearsRow> rows, DateTime asAt)
        {
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Arrears");
                Title(ws, "Current tenant arrears", asAt, 11);
                var headers = new[] { "Tenancy", "Property", "Address", "Postcode", "Patch", "Lead tenant", "Weekly charge", "Balance", "Weeks", "Stage", "Last payment" };
                Header(ws, 3, headers);
                var r = 4;
                foreach (var a in rows)
                {
                    ws.Cells[r, 1].Value = a.TenancyReference;
                    ws.Cells[r, 2].Value = a.PropertyReference;
                    ws.Cells[r, 3].Value = a.Address;
                    ws.Cells[r, 4].Value = a.Postcode;
                    ws.Cells[r, 5].Value = a.PatchCode;
                    ws.Cells[r, 6].Value = a.LeadTenant;
                    ws.Cells[r, 7].Value = a.WeeklyCharge;
                    ws.Cells[r, 8].Value = a.Balance;
                    ws.Cells[r, 9].Value = a.WeeksInArrears;
                    ws.Cells[r, 10].Value = ((Rent.ArrearsStage)a.Stage).ToString();
                    ws.Cells[r, 11].Value = a.LastPayment;
                    r++;
                }
                var last = Math.Max(4, r - 1);
                ws.Cells[4, 7, last, 8].Style.Numberformat.Format = "£#,##0.00";
                ws.Cells[4, 9, last, 9].Style.Numberformat.Format = "0.0";
                ws.Cells[4, 11, last, 11].Style.Numberformat.Format = "dd/mm/yyyy";
                ws.Cells[r, 7].Value = "Total";
                ws.Cells[r, 7].Style.Font.Bold = true;
                ws.Cells[r, 8].Formula = $"SUM(H4:H{last})";
                ws.Cells[r, 8].Style.Numberformat.Format = "£#,##0.00";
                ws.Cells[r, 8].Style.Font.Bold = true;

                var over8 = ws.ConditionalFormatting.AddGreaterThanOrEqual(new ExcelAddress(4, 9, last, 9));
                over8.Formula = "8";
                over8.Style.Fill.BackgroundColor.Color = Color.FromArgb(255, 199, 206);
                var over4 = ws.ConditionalFormatting.AddBetween(new ExcelAddress(4, 9, last, 9));
                over4.Formula = "4";
                over4.Formula2 = "7.9";
                over4.Style.Fill.BackgroundColor.Color = Color.FromArgb(255, 235, 156);

                Finish(ws, 3, headers.Length, last);
                return package.GetAsByteArray();
            }
        }

        public byte[] RepairsPerformance(IList<RepairsPerformanceRow> rows, DateTime from, DateTime to)
        {
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Performance");
                ws.Cells[1, 1].Value = $"Responsive repairs performance {from:dd/MM/yyyy} to {to:dd/MM/yyyy}";
                ws.Cells[1, 1].Style.Font.Size = 14;
                ws.Cells[1, 1].Style.Font.Bold = true;
                var headers = new[] { "Priority", "Contractor", "Completed", "In target", "% in target", "Average days", "Open overdue" };
                Header(ws, 3, headers);
                var r = 4;
                foreach (var p in rows)
                {
                    ws.Cells[r, 1].Value = ((RepairPriority)p.Priority).ToString();
                    ws.Cells[r, 2].Value = p.ContractorCode;
                    ws.Cells[r, 3].Value = p.Completed;
                    ws.Cells[r, 4].Value = p.CompletedInTarget;
                    ws.Cells[r, 5].Value = p.PercentInTarget / 100.0;
                    ws.Cells[r, 6].Value = p.AverageDays;
                    ws.Cells[r, 7].Value = p.OpenOverdue;
                    r++;
                }
                var last = Math.Max(4, r - 1);
                ws.Cells[4, 5, last, 5].Style.Numberformat.Format = "0.0%";
                ws.Cells[4, 6, last, 6].Style.Numberformat.Format = "0.0";

                var bar = ws.ConditionalFormatting.AddDatabar(new ExcelAddress(4, 5, last, 5), Brand);
                bar.LowValue.Type = eExcelConditionalFormattingValueObjectType.Num;
                bar.LowValue.Value = 0;
                bar.HighValue.Type = eExcelConditionalFormattingValueObjectType.Num;
                bar.HighValue.Value = 1;

                ws.Cells[r + 1, 1].Value = "Targets: emergency 24 hours, urgent 5 working days, routine 20 working days, planned 90 days.";
                ws.Cells[r + 1, 1].Style.Font.Italic = true;
                Finish(ws, 3, headers.Length, last);
                return package.GetAsByteArray();
            }
        }

        public byte[] Compliance(IList<ComplianceRow> rows, DateTime asAt)
        {
            using (var package = new ExcelPackage())
            {
                var summary = package.Workbook.Worksheets.Add("Summary");
                var detail = package.Workbook.Worksheets.Add("Certificates");
                Title(detail, "Compliance certificates", asAt, 8);
                var headers = new[] { "Property", "Address", "Patch", "Type", "Certificate", "Inspected", "Expires", "Status" };
                Header(detail, 3, headers);
                var r = 4;
                foreach (var c in rows)
                {
                    var status = c.ExpiresOn == null ? ComplianceStatus.NoCertificate
                        : c.ExpiresOn.Value.Date < asAt.Date ? ComplianceStatus.Overdue
                        : (c.ExpiresOn.Value.Date - asAt.Date).TotalDays <= ComplianceRules.DueSoonDays ? ComplianceStatus.DueSoon
                        : ComplianceStatus.Compliant;
                    detail.Cells[r, 1].Value = c.PropertyReference;
                    detail.Cells[r, 2].Value = c.Address;
                    detail.Cells[r, 3].Value = c.PatchCode;
                    detail.Cells[r, 4].Value = ((ComplianceType)c.Type).ToString();
                    detail.Cells[r, 5].Value = c.CertificateNumber;
                    detail.Cells[r, 6].Value = c.InspectedOn;
                    detail.Cells[r, 7].Value = c.ExpiresOn;
                    detail.Cells[r, 8].Value = status.ToString();
                    var fill = status == ComplianceStatus.Overdue ? Color.FromArgb(255, 199, 206)
                        : status == ComplianceStatus.DueSoon ? Color.FromArgb(255, 235, 156) : Color.FromArgb(198, 239, 206);
                    detail.Cells[r, 8].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    detail.Cells[r, 8].Style.Fill.BackgroundColor.SetColor(fill);
                    r++;
                }
                var last = Math.Max(4, r - 1);
                detail.Cells[4, 6, last, 7].Style.Numberformat.Format = "dd/mm/yyyy";
                Finish(detail, 3, headers.Length, last);

                Title(summary, "Compliance summary", asAt, 5);
                Header(summary, 3, new[] { "Type", "Compliant", "Due soon", "Overdue", "% compliant" });
                var row = 4;
                foreach (ComplianceType type in Enum.GetValues(typeof(ComplianceType)))
                {
                    var name = type.ToString();
                    summary.Cells[row, 1].Value = name;
                    summary.Cells[row, 2].Formula = $"COUNTIFS(Certificates!D:D,\"{name}\",Certificates!H:H,\"Compliant\")";
                    summary.Cells[row, 3].Formula = $"COUNTIFS(Certificates!D:D,\"{name}\",Certificates!H:H,\"DueSoon\")";
                    summary.Cells[row, 4].Formula = $"COUNTIFS(Certificates!D:D,\"{name}\",Certificates!H:H,\"Overdue\")";
                    summary.Cells[row, 5].Formula = $"IF(SUM(B{row}:D{row})=0,\"\",(B{row}+C{row})/SUM(B{row}:D{row}))";
                    summary.Cells[row, 5].Style.Numberformat.Format = "0.0%";
                    row++;
                }
                Finish(summary, 3, 5, row - 1);
                return package.GetAsByteArray();
            }
        }

        public byte[] RentRollXls(IList<RentRollRow> rows, DateTime asAt)
        {
            var workbook = new HSSFWorkbook();
            var sheet = workbook.CreateSheet("RentRoll");
            var bold = workbook.CreateFont();
            bold.IsBold = true;
            var headerStyle = workbook.CreateCellStyle();
            headerStyle.SetFont(bold);
            headerStyle.FillForegroundColor = NPOI.HSSF.Util.HSSFColor.Grey25Percent.Index;
            headerStyle.FillPattern = FillPattern.SolidForeground;
            var money = workbook.CreateCellStyle();
            money.DataFormat = workbook.CreateDataFormat().GetFormat("#,##0.00");

            var title = sheet.CreateRow(0);
            title.CreateCell(0).SetCellValue("RENT ROLL");
            title.CreateCell(1).SetCellValue(asAt.ToString("dd/MM/yyyy"));

            // Column order is fixed: the finance workbook reads columns A-J by position.
            var headers = new[] { "PropRef", "Address", "Estate", "Beds", "Status", "TenancyRef", "NetRent", "ServiceCharge", "Water", "Total" };
            var h = sheet.CreateRow(2);
            for (var i = 0; i < headers.Length; i++)
            {
                var cell = h.CreateCell(i);
                cell.SetCellValue(headers[i]);
                cell.CellStyle = headerStyle;
            }
            var r = 3;
            foreach (var x in rows)
            {
                var row = sheet.CreateRow(r);
                row.CreateCell(0).SetCellValue(x.PropertyReference);
                row.CreateCell(1).SetCellValue(x.Address);
                row.CreateCell(2).SetCellValue(x.EstateCode);
                row.CreateCell(3).SetCellValue(x.Bedrooms);
                row.CreateCell(4).SetCellValue(((Assets.PropertyStatus)x.Status).ToString().ToUpperInvariant());
                row.CreateCell(5).SetCellValue(x.TenancyReference ?? "");
                Money(row, 6, x.NetRent, money);
                Money(row, 7, x.ServiceCharge, money);
                Money(row, 8, x.WaterCharge, money);
                var total = row.CreateCell(9);
                total.SetCellFormula($"SUM(G{r + 1}:I{r + 1})");
                total.CellStyle = money;
                r++;
            }
            var totals = sheet.CreateRow(r);
            totals.CreateCell(5).SetCellValue("TOTAL");
            for (var c = 6; c <= 9; c++)
            {
                var col = (char)('A' + c);
                var cell = totals.CreateCell(c);
                cell.SetCellFormula($"SUM({col}4:{col}{r})");
                cell.CellStyle = money;
            }
            sheet.SetColumnWidth(1, 40 * 256);
            sheet.CreateFreezePane(0, 3);

            using (var ms = new MemoryStream())
            {
                workbook.Write(ms);
                return ms.ToArray();
            }
        }

        private static void Money(IRow row, int col, decimal value, ICellStyle style)
        {
            var cell = row.CreateCell(col);
            cell.SetCellValue((double)value);
            cell.CellStyle = style;
        }

        private static void Title(ExcelWorksheet ws, string text, DateTime asAt, int columns)
        {
            ws.Cells[1, 1].Value = text + " at " + asAt.ToString("dd/MM/yyyy");
            ws.Cells[1, 1, 1, columns].Merge = true;
            ws.Cells[1, 1].Style.Font.Size = 14;
            ws.Cells[1, 1].Style.Font.Bold = true;
        }

        private static void Header(ExcelWorksheet ws, int row, IList<string> headers)
        {
            for (var i = 0; i < headers.Count; i++) ws.Cells[row, i + 1].Value = headers[i];
            using (var range = ws.Cells[row, 1, row, headers.Count])
            {
                range.Style.Font.Bold = true;
                range.Style.Font.Color.SetColor(Color.White);
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(Brand);
            }
        }

        private static void Finish(ExcelWorksheet ws, int headerRow, int columns, int lastRow)
        {
            ws.Cells[headerRow, 1, Math.Max(headerRow, lastRow), columns].AutoFilter = true;
            ws.View.FreezePanes(headerRow + 1, 1);
            ws.Cells[headerRow, 1, Math.Max(headerRow, lastRow), columns].AutoFitColumns(8, 60);
            ws.PrinterSettings.Orientation = eOrientation.Landscape;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
        }
    }
}
