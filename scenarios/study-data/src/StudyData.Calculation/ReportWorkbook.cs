namespace StudyData.Calculation;

public record ReportWorkbookRequest(
    string TemplatePath,
    string OutputWorkbook,
    string? OutputPdf,
    IReadOnlyDictionary<string, object?> Values,
    IReadOnlyDictionary<string, IReadOnlyList<object?[]>> Tables,
    IReadOnlyList<string> PdfSheets,
    TimeSpan Timeout);

public record ReportWorkbookResult(string Workbook, string? Pdf);

public interface IReportWorkbookRunner
{
    Task<ReportWorkbookResult> RunAsync(ReportWorkbookRequest request, CancellationToken ct = default);
}
