using ClosedXML.Excel;

namespace StudyData.Calculation.Development;

/// <summary>
/// Development stand-in: fills the template with ClosedXML and saves it. Formulas are recalculated when the
/// file is opened; no PDF is produced.
/// </summary>
public class DevelopmentReportRunner : IReportWorkbookRunner
{
    public Task<ReportWorkbookResult> RunAsync(ReportWorkbookRequest request, CancellationToken ct = default)
    {
        using var wb = new XLWorkbook(request.TemplatePath);
        foreach (var (name, value) in request.Values)
        {
            wb.NamedRange(name).Ranges.First().FirstCell().Value = XLCellValue.FromObject(value);
        }

        foreach (var (startName, rows) in request.Tables)
        {
            var start = wb.NamedRange(startName).Ranges.First().FirstCell();
            for (var r = 0; r < rows.Count; r++)
            {
                for (var c = 0; c < rows[r].Length; c++)
                {
                    start.CellBelow(r).CellRight(c).Value = XLCellValue.FromObject(rows[r][c]);
                }
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(request.OutputWorkbook)!);
        wb.SaveAs(request.OutputWorkbook);
        return Task.FromResult(new ReportWorkbookResult(request.OutputWorkbook, null));
    }
}
