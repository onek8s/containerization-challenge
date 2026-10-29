using CityPermits.Data.Reporting;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;

namespace CityPermits.BackOffice.Controllers;

public class ReportsController : Controller
{
    private readonly ReportQueries _reports;

    public ReportsController(ReportQueries reports) => _reports = reports;

    public async Task<IActionResult> Index(int? year)
    {
        var y = year ?? DateTime.Today.Year;
        ViewBag.Year = y;
        ViewBag.Workload = await _reports.WorkloadAsync(DateTime.Today);
        ViewBag.Performance = await _reports.PerformanceAsync(new DateTime(y, 1, 1), new DateTime(y + 1, 1, 1));
        return View();
    }

    public async Task<IActionResult> Excel(int? year)
    {
        var y = year ?? DateTime.Today.Year;
        using var pkg = new ExcelPackage();
        var perf = pkg.Workbook.Worksheets.Add("Performance");
        perf.Cells["A1"].LoadFromCollection(await _reports.PerformanceAsync(new DateTime(y, 1, 1), new DateTime(y + 1, 1, 1)), true);
        var income = pkg.Workbook.Worksheets.Add("Income");
        income.Cells["A1"].LoadFromCollection(await _reports.IncomeAsync(new DateTime(y, 1, 1), new DateTime(y + 1, 1, 1)), true);
        income.Column(4).Style.Numberformat.Format = "£#,##0.00";
        var outstanding = pkg.Workbook.Worksheets.Add("Outstanding");
        outstanding.Cells["A1"].LoadFromCollection(await _reports.OutstandingAsync(), true);
        foreach (var ws in pkg.Workbook.Worksheets)
        {
            ws.Row(1).Style.Font.Bold = true;
            ws.Cells[ws.Dimension.Address].AutoFitColumns();
        }
        return File(pkg.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"permits-{y}.xlsx");
    }
}
