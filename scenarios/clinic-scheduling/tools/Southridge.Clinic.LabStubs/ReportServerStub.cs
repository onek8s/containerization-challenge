using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Southridge.Clinic.LabStubs;

/// <summary>
/// ReportExecution2005.asmx stand-in: LoadReport, SetExecutionParameters and Render (PDF, Excel or HTML)
/// return a one-page document listing the report path and parameter values.
/// </summary>
public sealed class ReportServerStub(TrafficLog log)
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Rs = "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices";

    private readonly ConcurrentDictionary<string, (string Report, List<(string Name, string Value)> Parameters)> _executions = new();

    public async Task<IResult> HandleAsync(HttpRequest request)
    {
        var envelope = await XDocument.LoadAsync(request.Body, LoadOptions.None, request.HttpContext.RequestAborted);
        var body = envelope.Root!.Element(Soap + "Body")!.Elements().First();
        var executionId = envelope.Root.Element(Soap + "Header")?.Element(Rs + "ExecutionHeader")?.Element(Rs + "ExecutionID")?.Value;

        switch (body.Name.LocalName)
        {
            case "LoadReport":
            {
                var path = body.Element(Rs + "Report")?.Value ?? "";
                var id = Guid.NewGuid().ToString("N")[..24];
                _executions[id] = (path, new List<(string, string)>());
                return Reply("LoadReportResponse", new XElement(Rs + "executionInfo", ExecutionInfo(id, path)), id);
            }

            case "SetExecutionParameters":
            {
                if (executionId == null || !_executions.TryGetValue(executionId, out var execution))
                {
                    return Fault("rsExecutionNotFound", "The report execution has expired or cannot be found.");
                }

                execution.Parameters.Clear();
                foreach (var p in body.Element(Rs + "Parameters")?.Elements(Rs + "ParameterValue") ?? [])
                {
                    execution.Parameters.Add((p.Element(Rs + "Name")?.Value ?? "", p.Element(Rs + "Value")?.Value ?? ""));
                }

                return Reply("SetExecutionParametersResponse", new XElement(Rs + "executionInfo", ExecutionInfo(executionId, execution.Report)), executionId);
            }

            case "Render":
            {
                if (executionId == null || !_executions.TryRemove(executionId, out var execution))
                {
                    return Fault("rsExecutionNotFound", "The report execution has expired or cannot be found.");
                }

                var format = body.Element(Rs + "Format")?.Value ?? "PDF";
                var lines = new List<string> { "Southridge Medical Group", execution.Report, "Rendered " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) };
                lines.AddRange(execution.Parameters.Select(p => p.Name + " = " + p.Value));
                var (content, extension, mime) = format.ToUpperInvariant() switch
                {
                    "EXCEL" => (Encoding.UTF8.GetBytes(string.Join("\r\n", lines)), "xls", "application/vnd.ms-excel"),
                    "HTML4.0" or "MHTML" => (Encoding.UTF8.GetBytes("<html><body><pre>" + System.Net.WebUtility.HtmlEncode(string.Join("\n", lines)) + "</pre></body></html>"), "html", "text/html"),
                    _ => (MinimalPdf.Create(lines), "pdf", "application/pdf")
                };
                log.Add("REPORTSERVER", "in", "Render", format, execution.Report + " (" + string.Join(", ", execution.Parameters.Select(p => p.Name + "=" + p.Value)) + ")");
                return Reply("RenderResponse", new object[]
                {
                    new XElement(Rs + "Result", Convert.ToBase64String(content)),
                    new XElement(Rs + "Extension", extension),
                    new XElement(Rs + "MimeType", mime),
                    new XElement(Rs + "Encoding", "")
                }, null);
            }

            default:
                return Fault("rsOperationNotSupported", "Operation " + body.Name.LocalName + " is not supported.");
        }
    }

    private static object[] ExecutionInfo(string id, string path) =>
    [
        new XElement(Rs + "HasSnapshot", "false"),
        new XElement(Rs + "NeedsProcessing", "true"),
        new XElement(Rs + "AllowQueryExecution", "true"),
        new XElement(Rs + "CredentialsRequired", "false"),
        new XElement(Rs + "ParametersRequired", "false"),
        new XElement(Rs + "ExpirationDateTime", DateTime.Now.AddMinutes(10).ToString("s", CultureInfo.InvariantCulture)),
        new XElement(Rs + "ExecutionDateTime", DateTime.Now.ToString("s", CultureInfo.InvariantCulture)),
        new XElement(Rs + "NumPages", "1"),
        new XElement(Rs + "HasDocumentMap", "false"),
        new XElement(Rs + "ExecutionID", id),
        new XElement(Rs + "ReportPath", path),
        new XElement(Rs + "AutoRefreshInterval", "0")
    ];

    private static IResult Reply(string operation, object content, string? executionId)
    {
        var header = new XElement(Soap + "Header",
            new XElement(Rs + "ServerInfoHeader", new XElement(Rs + "ReportServerVersionNumber", "2009.0100.4000.00"), new XElement(Rs + "ReportServerEdition", "Standard")));
        if (executionId != null)
        {
            header.AddFirst(new XElement(Rs + "ExecutionHeader", new XElement(Rs + "ExecutionID", executionId)));
        }

        var doc = new XDocument(new XElement(Soap + "Envelope", new XAttribute(XNamespace.Xmlns + "soap", Soap), header,
            new XElement(Soap + "Body", new XElement(Rs + operation, new XAttribute("xmlns", Rs), content))));
        return Results.Text(doc.ToString(SaveOptions.DisableFormatting), "text/xml; charset=utf-8", Encoding.UTF8);
    }

    private static IResult Fault(string code, string message)
    {
        var doc = new XDocument(new XElement(Soap + "Envelope", new XAttribute(XNamespace.Xmlns + "soap", Soap),
            new XElement(Soap + "Body", new XElement(Soap + "Fault",
                new XElement("faultcode", "soap:Client"),
                new XElement("faultstring", message),
                new XElement("detail", new XElement(Rs + "ErrorCode", code))))));
        return Results.Text(doc.ToString(SaveOptions.DisableFormatting), "text/xml; charset=utf-8", Encoding.UTF8, 500);
    }
}

/// <summary>One-page PDF with Helvetica text lines.</summary>
public static class MinimalPdf
{
    public static byte[] Create(IEnumerable<string> lines)
    {
        var text = new StringBuilder("BT /F1 11 Tf 56 780 Td 14 TL\n");
        foreach (var line in lines)
        {
            text.Append('(').Append(line.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")).Append(") Tj T*\n");
        }

        text.Append("ET");
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(text.ToString())} >>\nstream\n{text}\nendstream"
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }

        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n0 ").Append(objects.Length + 1).Append("\n0000000000 65535 f \n");
        foreach (var o in offsets)
        {
            pdf.Append(o.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        pdf.Append("trailer\n<< /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
