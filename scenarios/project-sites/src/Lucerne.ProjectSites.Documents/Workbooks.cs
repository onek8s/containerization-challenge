using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Lucerne.ProjectSites.Domain;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace Lucerne.ProjectSites.Documents
{
    public class PortfolioRow
    {
        public Project Project { get; set; }
        public ProjectStatus Status { get; set; }
    }

    public class HoursRow
    {
        public string ProjectNumber { get; set; }
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string Office { get; set; }
        public string Discipline { get; set; }
        public IsoWeek Week { get; set; }
        public decimal Hours { get; set; }
    }

    /// <summary>Excel exports (EPPlus): portfolio status, deliverable register, hours by week.</summary>
    public static class Workbooks
    {
        public static byte[] Portfolio(IEnumerable<PortfolioRow> rows, DateTime asOf)
        {
            using (var package = new ExcelPackage())
            {
                package.Workbook.Properties.Title = "Project portfolio " + asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                package.Workbook.Properties.Company = "Lucerne Engineering";
                var ws = package.Workbook.Worksheets.Add("Portfolio");
                var headers = new[] { "Project", "Name", "Client", "Office", "Stage", "PM", "Overall", "Schedule", "Budget", "Risk", "% complete", "Hours", "Budget hours", "Burn", "Overdue deliverables", "Summary" };
                Header(ws, headers);

                var r = 2;
                foreach (var row in rows.OrderBy(x => x.Project.OfficeCode).ThenBy(x => x.Project.Number))
                {
                    var p = row.Project;
                    var s = row.Status ?? new ProjectStatus { ProjectNumber = p.Number };
                    ws.Cells[r, 1].Value = p.Number;
                    ws.Cells[r, 2].Value = p.Name;
                    ws.Cells[r, 3].Value = p.ClientName;
                    ws.Cells[r, 4].Value = p.OfficeCode;
                    ws.Cells[r, 5].Value = p.Stage.ToString();
                    ws.Cells[r, 6].Value = Claims.AccountName(p.ProjectManagerLogin);
                    Rag(ws.Cells[r, 7], s.Overall);
                    Rag(ws.Cells[r, 8], s.Schedule);
                    Rag(ws.Cells[r, 9], s.Budget);
                    Rag(ws.Cells[r, 10], s.Risk);
                    ws.Cells[r, 11].Value = (double)p.PercentComplete / 100d;
                    ws.Cells[r, 11].Style.Numberformat.Format = "0%";
                    ws.Cells[r, 12].Value = (double)s.HoursToDate;
                    ws.Cells[r, 13].Value = (double)p.BudgetHours;
                    ws.Cells[r, 14].Value = (double)s.BurnRatio;
                    ws.Cells[r, 14].Style.Numberformat.Format = "0.00";
                    ws.Cells[r, 15].Value = s.DeliverablesOverdue;
                    ws.Cells[r, 16].Value = s.Summary;
                    r++;
                }

                ws.Cells[2, 12, Math.Max(2, r - 1), 13].Style.Numberformat.Format = "#,##0";
                Finish(ws, headers.Length, r - 1, new double[] { 10, 36, 28, 8, 10, 16, 10, 10, 10, 10, 11, 10, 12, 8, 12, 60 });

                var summary = package.Workbook.Worksheets.Add("By office");
                Header(summary, new[] { "Office", "Projects", "Green", "Amber", "Red", "Not assessed" });
                var sr = 2;
                foreach (var g in rows.GroupBy(x => x.Project.OfficeCode).OrderBy(g => g.Key))
                {
                    summary.Cells[sr, 1].Value = g.Key;
                    summary.Cells[sr, 2].Value = g.Count();
                    summary.Cells[sr, 3].Value = g.Count(x => x.Status != null && x.Status.Overall == RagStatus.Green);
                    summary.Cells[sr, 4].Value = g.Count(x => x.Status != null && x.Status.Overall == RagStatus.Amber);
                    summary.Cells[sr, 5].Value = g.Count(x => x.Status != null && x.Status.Overall == RagStatus.Red);
                    summary.Cells[sr, 6].Value = g.Count(x => x.Status == null || x.Status.Overall == RagStatus.NotAssessed);
                    sr++;
                }

                Finish(summary, 6, sr - 1, new double[] { 10, 10, 10, 10, 10, 14 });
                return package.GetAsByteArray();
            }
        }

        public static byte[] DeliverableRegister(string projectNumber, IEnumerable<Deliverable> deliverables, DateTime today)
        {
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add(projectNumber);
                var headers = new[] { "Document number", "Title", "Discipline", "Type", "Revision", "Status", "Planned", "Forecast", "Issued", "Overdue" };
                Header(ws, headers);
                var r = 2;
                foreach (var d in deliverables.OrderBy(x => x.DocumentNumber, StringComparer.Ordinal))
                {
                    ws.Cells[r, 1].Value = d.DocumentNumber;
                    ws.Cells[r, 2].Value = d.Title;
                    ws.Cells[r, 3].Value = d.DisciplineCode;
                    ws.Cells[r, 4].Value = d.DocumentType;
                    ws.Cells[r, 5].Value = d.Revision;
                    ws.Cells[r, 6].Value = d.Status.ToString();
                    ws.Cells[r, 7].Value = d.PlannedDate;
                    ws.Cells[r, 8].Value = d.ForecastDate;
                    ws.Cells[r, 9].Value = d.IssuedDate;
                    var overdue = d.IsOverdue(today);
                    ws.Cells[r, 10].Value = overdue ? "Yes" : string.Empty;
                    if (overdue)
                    {
                        Fill(ws.Cells[r, 1, r, headers.Length], Color.FromArgb(0xFF, 0xC7, 0xCE));
                    }

                    r++;
                }

                ws.Cells[2, 7, Math.Max(2, r - 1), 9].Style.Numberformat.Format = "yyyy-mm-dd";
                Finish(ws, headers.Length, r - 1, new double[] { 26, 44, 10, 8, 9, 14, 12, 12, 12, 9 });
                return package.GetAsByteArray();
            }
        }

        public static byte[] HoursByWeek(IEnumerable<HoursRow> rows)
        {
            var list = rows.ToList();
            var weeks = list.Select(x => x.Week).Distinct().OrderBy(w => w).ToList();
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Hours");
                var headers = new[] { "Project", "Employee", "Name", "Office", "Discipline" }.Concat(weeks.Select(w => w.ToString())).Concat(new[] { "Total" }).ToArray();
                Header(ws, headers);
                var r = 2;
                foreach (var g in list.GroupBy(x => new { x.ProjectNumber, x.EmployeeId }).OrderBy(g => g.Key.ProjectNumber).ThenBy(g => g.Key.EmployeeId))
                {
                    var first = g.First();
                    ws.Cells[r, 1].Value = g.Key.ProjectNumber;
                    ws.Cells[r, 2].Value = g.Key.EmployeeId;
                    ws.Cells[r, 3].Value = first.EmployeeName;
                    ws.Cells[r, 4].Value = first.Office;
                    ws.Cells[r, 5].Value = first.Discipline;
                    for (var i = 0; i < weeks.Count; i++)
                    {
                        var h = g.Where(x => x.Week.Equals(weeks[i])).Sum(x => x.Hours);
                        if (h != 0)
                        {
                            ws.Cells[r, 6 + i].Value = (double)h;
                        }
                    }

                    var totalCol = 6 + weeks.Count;
                    ws.Cells[r, totalCol].Formula = "SUM(" + ws.Cells[r, 6].Address + ":" + ws.Cells[r, Math.Max(6, totalCol - 1)].Address + ")";
                    r++;
                }

                Finish(ws, headers.Length, r - 1, null);
                return package.GetAsByteArray();
            }
        }

        private static void Header(ExcelWorksheet ws, string[] headers)
        {
            for (var c = 0; c < headers.Length; c++)
            {
                ws.Cells[1, c + 1].Value = headers[c];
            }

            var range = ws.Cells[1, 1, 1, headers.Length];
            range.Style.Font.Bold = true;
            range.Style.Font.Color.SetColor(Color.White);
            Fill(range, Color.FromArgb(0x1F, 0x4E, 0x79));
        }

        private static void Finish(ExcelWorksheet ws, int columns, int lastRow, double[] widths)
        {
            ws.View.FreezePanes(2, 1);
            ws.Cells[1, 1, Math.Max(1, lastRow), columns].AutoFilter = true;
            for (var c = 1; c <= columns; c++)
            {
                // Fixed widths: AutoFitColumns needs GDI+ for text measurement.
                ws.Column(c).Width = widths != null && c <= widths.Length ? widths[c - 1] : 11;
            }
        }

        private static void Rag(ExcelRange cell, RagStatus status)
        {
            cell.Value = status == RagStatus.NotAssessed ? "-" : status.ToString();
            var hex = RagColours.Hex(status);
            Fill(cell, Color.FromArgb(Convert.ToInt32(hex.Substring(0, 2), 16), Convert.ToInt32(hex.Substring(2, 2), 16), Convert.ToInt32(hex.Substring(4, 2), 16)));
        }

        private static void Fill(ExcelRange range, Color colour)
        {
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(colour);
        }
    }
}
