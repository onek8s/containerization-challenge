using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using Southridge.Clinic.Data;

namespace Southridge.Clinic.Documents
{
    /// <summary>Excel 97-2003 workbooks (.xls) built with NPOI for the billing office and practice managers.</summary>
    public static class ExcelExports
    {
        public static byte[] AgingWorkbook(ReportsDataSet.ArAgingDataTable aging, DateTime asOf)
        {
            var workbook = new HSSFWorkbook();
            Styles styles = new Styles(workbook);

            ISheet detail = workbook.CreateSheet("Claims");
            WriteTable(detail, aging, styles, new[] { "PayerName", "ClaimNumber", "Mrn", "PatientName", "DateOfService", "Outstanding", "AgeDays", "Bucket", "StatusText" },
                new[] { "Payer", "Claim", "MRN", "Patient", "Date of service", "Outstanding", "Age (days)", "Bucket", "Status" });

            ISheet summary = workbook.CreateSheet("Summary");
            string[] buckets = new[] { "0-30", "31-60", "61-90", "91-120", "120+" };
            IRow title = summary.CreateRow(0);
            title.CreateCell(0).SetCellValue("Accounts receivable aging as of " + asOf.ToString("MM/dd/yyyy"));
            title.GetCell(0).CellStyle = styles.Header;
            IRow head = summary.CreateRow(2);
            head.CreateCell(0).SetCellValue("Payer");
            for (int i = 0; i < buckets.Length; i++)
            {
                head.CreateCell(i + 1).SetCellValue(buckets[i]);
            }

            head.CreateCell(buckets.Length + 1).SetCellValue("Total");
            foreach (ICell c in head.Cells)
            {
                c.CellStyle = styles.Header;
            }

            var totals = new SortedDictionary<string, decimal[]>();
            foreach (ReportsDataSet.ArAgingRow row in aging)
            {
                decimal[] t;
                if (!totals.TryGetValue(row.PayerName, out t))
                {
                    t = new decimal[buckets.Length];
                    totals.Add(row.PayerName, t);
                }

                t[Array.IndexOf(buckets, row.Bucket)] += row.Outstanding;
            }

            int r = 3;
            foreach (KeyValuePair<string, decimal[]> payer in totals)
            {
                IRow row = summary.CreateRow(r);
                row.CreateCell(0).SetCellValue(payer.Key);
                for (int i = 0; i < buckets.Length; i++)
                {
                    ICell cell = row.CreateCell(i + 1);
                    cell.SetCellValue((double)payer.Value[i]);
                    cell.CellStyle = styles.Money;
                }

                ICell total = row.CreateCell(buckets.Length + 1);
                total.CellFormula = string.Format("SUM(B{0}:F{0})", r + 1);
                total.CellStyle = styles.Money;
                r++;
            }

            IRow grand = summary.CreateRow(r);
            grand.CreateCell(0).SetCellValue("Total");
            grand.GetCell(0).CellStyle = styles.Header;
            for (int i = 1; i <= buckets.Length + 1; i++)
            {
                ICell cell = grand.CreateCell(i);
                string col = CellReference.ConvertNumToColString(i);
                cell.CellFormula = string.Format("SUM({0}4:{0}{1})", col, r);
                cell.CellStyle = styles.Money;
            }

            for (int i = 0; i <= buckets.Length + 1; i++)
            {
                summary.AutoSizeColumn(i);
            }

            return Save(workbook);
        }

        public static byte[] TableWorkbook(DataTable table, string sheetName)
        {
            var workbook = new HSSFWorkbook();
            Styles styles = new Styles(workbook);
            ISheet sheet = workbook.CreateSheet(sheetName);
            var columns = new List<string>();
            foreach (DataColumn c in table.Columns)
            {
                columns.Add(c.ColumnName);
            }

            WriteTable(sheet, table, styles, columns.ToArray(), columns.ToArray());
            return Save(workbook);
        }

        private static void WriteTable(ISheet sheet, DataTable table, Styles styles, string[] columns, string[] headers)
        {
            IRow head = sheet.CreateRow(0);
            for (int i = 0; i < headers.Length; i++)
            {
                ICell cell = head.CreateCell(i);
                cell.SetCellValue(headers[i]);
                cell.CellStyle = styles.Header;
            }

            int r = 1;
            foreach (DataRow dataRow in table.Rows)
            {
                IRow row = sheet.CreateRow(r++);
                for (int i = 0; i < columns.Length; i++)
                {
                    object value = dataRow[columns[i]];
                    ICell cell = row.CreateCell(i);
                    if (value == DBNull.Value)
                    {
                        continue;
                    }

                    if (value is DateTime)
                    {
                        cell.SetCellValue((DateTime)value);
                        cell.CellStyle = styles.Date;
                    }
                    else if (value is decimal)
                    {
                        cell.SetCellValue((double)(decimal)value);
                        cell.CellStyle = styles.Money;
                    }
                    else if (value is int || value is short || value is byte || value is double)
                    {
                        cell.SetCellValue(Convert.ToDouble(value));
                    }
                    else
                    {
                        cell.SetCellValue(Convert.ToString(value));
                    }
                }
            }

            sheet.CreateFreezePane(0, 1);
            for (int i = 0; i < columns.Length; i++)
            {
                sheet.AutoSizeColumn(i);
            }
        }

        private static byte[] Save(HSSFWorkbook workbook)
        {
            using (var stream = new MemoryStream())
            {
                workbook.Write(stream);
                return stream.ToArray();
            }
        }

        private class Styles
        {
            public Styles(HSSFWorkbook workbook)
            {
                IFont bold = workbook.CreateFont();
                bold.Boldweight = (short)FontBoldWeight.BOLD;
                Header = workbook.CreateCellStyle();
                Header.SetFont(bold);
                Header.FillForegroundColor = NPOI.HSSF.Util.HSSFColor.GREY_25_PERCENT.index;
                Header.FillPattern = FillPatternType.SOLID_FOREGROUND;
                Money = workbook.CreateCellStyle();
                Money.DataFormat = workbook.CreateDataFormat().GetFormat("$#,##0.00");
                Date = workbook.CreateCellStyle();
                Date.DataFormat = workbook.CreateDataFormat().GetFormat("mm/dd/yyyy");
            }

            public ICellStyle Header { get; private set; }

            public ICellStyle Money { get; private set; }

            public ICellStyle Date { get; private set; }
        }
    }
}
