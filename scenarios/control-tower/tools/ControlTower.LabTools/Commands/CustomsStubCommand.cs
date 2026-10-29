using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Proseware.ControlTower.Integration.Customs;

namespace Proseware.ControlTower.LabTools.Commands;

/// <summary>
/// Stand-in for the customs declaration service: accepts declarations with an MRN, releases them
/// after a delay and puts every n-th declaration under control first.
/// </summary>
public sealed class CustomsStubCommand : ILabCommand
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Svc = CustomsNamespaces.Service;
    private static readonly XNamespace Data = CustomsNamespaces.Data;

    private readonly ConcurrentDictionary<string, (string Mrn, DateTime Accepted, bool Control)> _declarations = new();
    private int _count;

    public string Name => "customs-stub";

    public string Usage => "[--prefix http://+:8099/declaration/v2/] [--release-seconds 60] [--control-every 5] [--reject-office XX000000]";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var a = new LabArgs(args);
        var prefix = a.Get("prefix", "http://+:8099/declaration/v2/");
        var release = TimeSpan.FromSeconds(a.GetInt("release-seconds", 60));
        var controlEvery = a.GetInt("control-every", 5);
        var rejectOffice = a.Get("reject-office");

        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        Console.WriteLine($"Customs stub listening on {prefix} (release after {release.TotalSeconds:0} s, every {controlEvery}. under control).");
        using var reg = cancellationToken.Register(listener.Stop);

        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var request = XDocument.Parse(await reader.ReadToEndAsync());
                var body = request.Root!.Element(Soap + "Body")!.Elements().First();
                var response = body.Name.LocalName switch
                {
                    "SubmitDeclaration" => Submit(body, controlEvery, rejectOffice),
                    "GetDeclarationStatus" => Status(body, release),
                    _ => throw new InvalidOperationException("Unknown operation " + body.Name.LocalName)
                };
                await WriteAsync(context, 200, new XDocument(new XElement(Soap + "Envelope", new XElement(Soap + "Body", response))));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fault: " + ex.Message);
                var fault = new XElement(Soap + "Fault", new XElement("faultcode", "s:Client"), new XElement("faultstring", ex.Message));
                await WriteAsync(context, 500, new XDocument(new XElement(Soap + "Envelope", new XElement(Soap + "Body", fault))));
            }
        }
        return 0;
    }

    private XElement Submit(XElement operation, int controlEvery, string? rejectOffice)
    {
        var req = operation.Elements().First();
        string V(string name) => req.Element(Data + name)?.Value ?? "";
        var lrn = V("Lrn");
        var office = V("OfficeOfDeclaration");
        var errors = new List<(string Code, string Pointer, string Text)>();
        if (string.IsNullOrEmpty(lrn)) errors.Add(("12", "Lrn", "LRN missing"));
        if (office == rejectOffice) errors.Add(("12", "OfficeOfDeclaration", $"Office {office} is not valid for this procedure"));
        if (_declarations.ContainsKey(lrn)) errors.Add(("15", "Lrn", "LRN already used"));

        var accepted = errors.Count == 0;
        string? mrn = null;
        if (accepted)
        {
            var n = Interlocked.Increment(ref _count);
            var type = V("DeclarationType") == "EX" ? "E" : "I";
            mrn = $"{DateTime.UtcNow:yy}FI{n:D9}{type}{n % 10}{(char)('A' + n % 26)}".PadRight(18, '0')[..18];
            _declarations[lrn] = (mrn, DateTime.UtcNow, controlEvery > 0 && n % controlEvery == 0);
        }
        Console.WriteLine($"Submit {lrn} ({V("DeclarationType")}, {req.Element(Data + "GoodsItems")?.Elements().Count()} items): {(accepted ? "accepted " + mrn : "rejected")}");

        return new XElement(Svc + "SubmitDeclarationResponse",
            new XElement(Svc + "SubmitDeclarationResult",
                new XElement(Data + "MessageId", V("MessageId")),
                new XElement(Data + "Accepted", accepted ? "true" : "false"),
                mrn is null ? null : new XElement(Data + "Mrn", mrn),
                accepted ? new XElement(Data + "AcceptanceDate", XmlDate(DateTime.UtcNow)) : null,
                Errors(errors)));
    }

    private XElement Status(XElement operation, TimeSpan release)
    {
        var req = operation.Elements().First();
        var lrn = req.Element(Data + "Lrn")?.Value ?? "";
        string status;
        string? mrn = null;
        string? control = null;
        DateTime? date = null;
        if (!_declarations.TryGetValue(lrn, out var d))
        {
            status = "REJECTED";
        }
        else
        {
            mrn = d.Mrn;
            var age = DateTime.UtcNow - d.Accepted;
            if (age < release) (status, date) = ("ACCEPTED", d.Accepted);
            else if (d.Control && age < release * 4) (status, date, control) = ("UNDER_CONTROL", d.Accepted + release, "DOCUMENTARY");
            else (status, date) = ("RELEASED", DateTime.UtcNow);
        }
        Console.WriteLine($"Status {lrn}: {status}");
        return new XElement(Svc + "GetDeclarationStatusResponse",
            new XElement(Svc + "GetDeclarationStatusResult",
                mrn is null ? null : new XElement(Data + "Mrn", mrn),
                new XElement(Data + "Status", status),
                date is null ? null : new XElement(Data + "StatusDate", XmlDate(date.Value)),
                control is null ? null : new XElement(Data + "ControlType", control),
                Errors(mrn is null ? new List<(string, string, string)> { ("90", "Lrn", "Unknown declaration") } : new())));
    }

    private static XElement Errors(List<(string Code, string Pointer, string Text)> errors) =>
        new(Data + "Errors", errors.Select(e => new XElement(Data + "FunctionalError",
            new XElement(Data + "Code", e.Code), new XElement(Data + "Pointer", e.Pointer), new XElement(Data + "Text", e.Text))));

    private static string XmlDate(DateTime d) => d.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    private static async Task WriteAsync(HttpListenerContext context, int status, XDocument doc)
    {
        var bytes = Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));
        context.Response.StatusCode = status;
        context.Response.ContentType = "text/xml; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }
}
