using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace StudyData.Calculation.Excel;

/// <summary>
/// Fills a report workbook template (DSMB safety tables) on an Excel instance of the pool: listing ranges
/// are written as arrays, the workbook is recalculated, saved as .xlsx and the summary sheets are exported as PDF.
/// </summary>
[SupportedOSPlatform("windows")]
public class ExcelReportRunner : IReportWorkbookRunner
{
    private const int XlOpenXmlWorkbook = 51;
    private const int XlTypePdf = 0;
    private const int XlQualityStandard = 0;

    private readonly ExcelPool _pool;
    private readonly ILogger<ExcelReportRunner> _log;

    public ExcelReportRunner(ExcelPool pool, ILogger<ExcelReportRunner> log)
    {
        _pool = pool;
        _log = log;
    }

    public Task<ReportWorkbookResult> RunAsync(ReportWorkbookRequest request, CancellationToken ct = default) =>
        _pool.UseAsync(instance => instance.Automate(app =>
        {
            dynamic wb = app.Workbooks.Open(request.TemplatePath, 0, true);
            try
            {
                foreach (var (name, value) in request.Values)
                {
                    wb.Names.Item(name).RefersToRange.Value2 = value;
                }

                foreach (var (startName, rows) in request.Tables)
                {
                    if (rows.Count == 0)
                    {
                        continue;
                    }

                    var cols = rows.Max(r => r.Length);
                    var data = new object?[rows.Count, cols];
                    for (var r = 0; r < rows.Count; r++)
                    {
                        for (var c = 0; c < rows[r].Length; c++)
                        {
                            data[r, c] = rows[r][c];
                        }
                    }

                    dynamic start = wb.Names.Item(startName).RefersToRange;
                    dynamic target = start.Resize(rows.Count, cols);
                    target.Value2 = data;
                    Marshal.ReleaseComObject(target);
                    Marshal.ReleaseComObject(start);
                }

                app.CalculateFull();
                Directory.CreateDirectory(Path.GetDirectoryName(request.OutputWorkbook)!);
                wb.SaveAs(request.OutputWorkbook, XlOpenXmlWorkbook);

                string? pdf = null;
                if (request.OutputPdf is not null)
                {
                    dynamic sheets = wb.Worksheets.Item(request.PdfSheets.ToArray());
                    sheets.Select();
                    // ExportAsFixedFormat(Type, Filename, Quality, IncludeDocProperties, IgnorePrintAreas)
                    wb.ActiveSheet.ExportAsFixedFormat(XlTypePdf, request.OutputPdf, XlQualityStandard, true, false);
                    Marshal.ReleaseComObject(sheets);
                    pdf = request.OutputPdf;
                }

                _log.LogInformation("Report workbook {Output} written on Excel #{Number}", request.OutputWorkbook, instance.Number);
                return new ReportWorkbookResult(request.OutputWorkbook, pdf);
            }
            finally
            {
                wb.Close(false);
                Marshal.FinalReleaseComObject(wb);
            }
        }, request.Timeout), ct);
}
