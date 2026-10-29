using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using OfficeOpenXml.Table;
using VanArsdel.Integration.Monitoring;

namespace VanArsdel.Integration.Tracking.Exports
{
    /// <summary>Excel exports (EPPlus 4.5, the last LGPL version) for customer service and partner managers.</summary>
    public static class WorkbookExporter
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private const string DateTimeFormat = "yyyy-mm-dd hh:mm";

        public static byte[] Orders(IEnumerable<OrderActivityRow> rows, string title)
        {
            using (var package = new ExcelPackage())
            {
                var sheet = package.Workbook.Worksheets.Add("Orders");
                string[] headers =
                {
                    "Partner", "PO number", "ERP order", "Status", "Lines", "Value", "Received", "Validated", "Credit hold",
                    "Released", "Sent to ERP", "Confirmed", "Despatched", "ASN sent", "Invoiced", "Rejected", "Reason"
                };

                Title(sheet, title, headers.Length);
                for (var c = 0; c < headers.Length; c++)
                {
                    sheet.Cells[3, c + 1].Value = headers[c];
                }

                var r = 4;
                foreach (var o in rows)
                {
                    sheet.Cells[r, 1].Value = o.PartnerId;
                    sheet.Cells[r, 2].Value = o.OrderNumber;
                    sheet.Cells[r, 3].Value = o.ErpOrderNumber;
                    sheet.Cells[r, 4].Value = o.Status;
                    sheet.Cells[r, 5].Value = o.LineCount;
                    sheet.Cells[r, 6].Value = o.OrderValue;
                    Date(sheet, r, 7, o.ReceivedUtc);
                    Date(sheet, r, 8, o.ValidatedUtc);
                    Date(sheet, r, 9, o.CreditHoldUtc);
                    Date(sheet, r, 10, o.ReleasedUtc);
                    Date(sheet, r, 11, o.SentToErpUtc);
                    Date(sheet, r, 12, o.ConfirmedUtc);
                    Date(sheet, r, 13, o.DespatchedUtc);
                    Date(sheet, r, 14, o.AsnSentUtc);
                    Date(sheet, r, 15, o.InvoicedUtc);
                    Date(sheet, r, 16, o.RejectedUtc);
                    sheet.Cells[r, 17].Value = o.RejectReason ?? o.HoldReason;
                    r++;
                }

                sheet.Column(6).Style.Numberformat.Format = "#,##0.00";
                Table(sheet, 3, r - 1, headers.Length, "OrderTable");
                return package.GetAsByteArray();
            }
        }

        public static byte[] PartnerKpis(IEnumerable<PartnerKpi> kpis, string title)
        {
            using (var package = new ExcelPackage())
            {
                var sheet = package.Workbook.Worksheets.Add("Partner KPIs");
                string[] headers = { "Partner", "Orders", "Rejected", "Reject rate", "Credit holds", "Median min to ERP", "Avg h to despatch", "Interchanges", "Acks on time", "Acks late/missing", "Ack on-time rate" };
                Title(sheet, title, headers.Length);
                for (var c = 0; c < headers.Length; c++)
                {
                    sheet.Cells[3, c + 1].Value = headers[c];
                }

                var r = 4;
                foreach (var k in kpis)
                {
                    sheet.Cells[r, 1].Value = k.PartnerId;
                    sheet.Cells[r, 2].Value = k.Orders;
                    sheet.Cells[r, 3].Value = k.Rejected;
                    sheet.Cells[r, 4].Value = k.RejectRate;
                    sheet.Cells[r, 5].Value = k.CreditHolds;
                    sheet.Cells[r, 6].Value = k.MedianMinutesToErp;
                    sheet.Cells[r, 7].Value = k.AverageHoursToDespatch;
                    sheet.Cells[r, 8].Value = k.Interchanges;
                    sheet.Cells[r, 9].Value = k.AcksOnTime;
                    sheet.Cells[r, 10].Value = k.AcksLateOrMissing;
                    sheet.Cells[r, 11].Value = k.AckOnTimeRate;
                    r++;
                }

                sheet.Column(4).Style.Numberformat.Format = "0.0%";
                sheet.Column(11).Style.Numberformat.Format = "0.0%";
                sheet.Column(6).Style.Numberformat.Format = "0.0";
                sheet.Column(7).Style.Numberformat.Format = "0.0";

                if (r > 4)
                {
                    // red when more than 2 % of a partner's orders were rejected
                    var rule = sheet.ConditionalFormatting.AddGreaterThan(new ExcelAddress(4, 4, r - 1, 4));
                    rule.Formula = "0.02";
                    rule.Style.Font.Color.Color = Color.DarkRed;
                }

                Table(sheet, 3, r - 1, headers.Length, "KpiTable");
                return package.GetAsByteArray();
            }
        }

        public static byte[] Volumes(DataTable volumes, string title)
        {
            using (var package = new ExcelPackage())
            {
                var sheet = package.Workbook.Worksheets.Add("Volumes");
                Title(sheet, title, Math.Max(1, volumes.Columns.Count));
                sheet.Cells[3, 1].LoadFromDataTable(volumes, true, TableStyles.Medium2);
                for (var c = 0; c < volumes.Columns.Count; c++)
                {
                    if (volumes.Columns[c].DataType == typeof(DateTime))
                    {
                        sheet.Column(c + 1).Style.Numberformat.Format = "yyyy-mm-dd";
                    }
                }

                if (volumes.Rows.Count > 0 && volumes.Columns.Count > 1)
                {
                    var chart = (OfficeOpenXml.Drawing.Chart.ExcelLineChart)sheet.Drawings.AddChart("Daily", OfficeOpenXml.Drawing.Chart.eChartType.Line);
                    chart.Title.Text = "Documents per day";
                    chart.SetPosition(2, 0, volumes.Columns.Count + 1, 0);
                    chart.SetSize(720, 360);
                    var last = 3 + volumes.Rows.Count;
                    for (var c = 2; c <= volumes.Columns.Count; c++)
                    {
                        var series = chart.Series.Add(ExcelCellBase.GetAddress(4, c, last, c), ExcelCellBase.GetAddress(4, 1, last, 1));
                        series.Header = volumes.Columns[c - 1].ColumnName;
                    }
                }

                sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                return package.GetAsByteArray();
            }
        }

        private static void Title(ExcelWorksheet sheet, string title, int columns)
        {
            sheet.Cells[1, 1].Value = title;
            sheet.Cells[1, 1, 1, columns].Merge = true;
            sheet.Cells[1, 1].Style.Font.Size = 14;
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[2, 1].Value = "Generated " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " (times in UTC)";
            sheet.Cells[2, 1].Style.Font.Italic = true;
            sheet.Cells[2, 1].Style.Font.Color.SetColor(Color.Gray);
        }

        private static void Date(ExcelWorksheet sheet, int row, int col, DateTime? value)
        {
            if (value.HasValue)
            {
                sheet.Cells[row, col].Value = value.Value;
                sheet.Cells[row, col].Style.Numberformat.Format = DateTimeFormat;
            }
        }

        private static void Table(ExcelWorksheet sheet, int headerRow, int lastRow, int columns, string name)
        {
            if (lastRow <= headerRow)
            {
                sheet.Cells[headerRow, 1, headerRow, columns].Style.Font.Bold = true;
                return;
            }

            var table = sheet.Tables.Add(sheet.Cells[headerRow, 1, lastRow, columns], name);
            table.TableStyle = TableStyles.Medium2;
            sheet.View.FreezePanes(headerRow + 1, 1);
            sheet.Cells[headerRow, 1, lastRow, columns].AutoFitColumns(8, 40);
            sheet.Cells[headerRow, 1, headerRow, columns].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
    }
}
