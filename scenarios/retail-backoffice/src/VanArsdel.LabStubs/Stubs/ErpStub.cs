using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using VanArsdel.Integration.Erp;

namespace VanArsdel.LabStubs.Stubs;

/// <summary>
/// Merx ERP integration web service (SOAP 1.1, basicHttpBinding, DataContractSerializer wire format).
/// Keeps everything in memory; transfer orders produce a goods receipt at the receiving store.
/// </summary>
public class ErpStub
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Svc = ErpNamespaces.Service;
    private static readonly XNamespace Data = ErpNamespaces.Data;
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    private readonly ConcurrentDictionary<string, XElement> _items = new();
    private readonly ConcurrentQueue<string> _journal = new();
    private readonly ConcurrentDictionary<string, Receipt> _receipts = new();
    private readonly IConfiguration _configuration;
    private readonly ILogger<ErpStub> _logger;
    private int _documentNo = 100000;

    public ErpStub(IConfiguration configuration, ILogger<ErpStub> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public record ReceiptLine(string ItemNo, string VariantCode, decimal Quantity);

    public record Receipt(string ReceiptNo, string PurchaseOrderNo, string SiteCode, DateTime PostedAt, List<ReceiptLine> Lines)
    {
        public bool Acknowledged { get; set; }
    }

    public void Map(WebApplication app)
    {
        app.MapPost("/erp/ErpIntegration.svc", Handle);
        app.MapGet("/erp/ErpIntegration.svc", () => Results.Content("Merx ERP integration service (lab)."));
        app.MapGet("/erp/admin/state", () => Results.Json(new
        {
            items = _items.Keys.OrderBy(k => k),
            documents = _journal.ToArray(),
            receipts = _receipts.Values.OrderBy(r => r.ReceiptNo),
        }));
        // Lab: post a purchase order receipt, e.g. {"siteCode":"DC01","purchaseOrderNo":"PO-1","lines":[{"sku":"VA1001-BLK-M","quantity":20}]}
        app.MapPost("/erp/admin/receipts", (ReceiptRequest r) => Results.Json(AddReceipt(r.SiteCode, r.PurchaseOrderNo, r.Lines.Select(Split).ToList())));
    }

    public class ReceiptRequest
    {
        public string SiteCode { get; set; }
        public string PurchaseOrderNo { get; set; }
        public List<SkuQuantity> Lines { get; set; } = new();
    }

    public class SkuQuantity
    {
        public string Sku { get; set; }
        public int Quantity { get; set; }
    }

    private ReceiptLine Split(SkuQuantity l)
    {
        var prefix = _configuration["Lab:ErpItemPrefix"] ?? "VA";
        var dash = l.Sku.IndexOf('-');
        return dash < 0 ? new ReceiptLine(prefix + l.Sku, "", l.Quantity) : new ReceiptLine(prefix + l.Sku[..dash], l.Sku[(dash + 1)..], l.Quantity);
    }

    private Receipt AddReceipt(string site, string po, List<ReceiptLine> lines)
    {
        var receipt = new Receipt("GR" + Interlocked.Increment(ref _documentNo), po, site, DateTime.Now, lines);
        _receipts[receipt.ReceiptNo] = receipt;
        _logger.LogInformation("ERP goods receipt {ReceiptNo} at {Site} ({Lines} lines)", receipt.ReceiptNo, site, lines.Count);
        return receipt;
    }

    private async Task<IResult> Handle(HttpRequest request)
    {
        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes((_configuration["Lab:ErpUser"] ?? "INT_VANARSDEL") + ":" + (_configuration["Lab:ErpPassword"] ?? "dev")));
        if (request.Headers.Authorization != expected)
        {
            // WCF sends the credentials only after a challenge, like the real ERP web server.
            request.HttpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"ERP\"";
            return Results.StatusCode(401);
        }
        var action = request.Headers["SOAPAction"].ToString().Trim('"');
        var op = action[(action.LastIndexOf('/') + 1)..];
        var doc = await XDocument.LoadAsync(request.Body, LoadOptions.None, request.HttpContext.RequestAborted);
        var body = doc.Root!.Element(Soap + "Body")!.Elements().First();

        XElement result;
        switch (op)
        {
            case "Ping":
                result = new XElement(Svc + "PingResult", "Merx 9.2 lab " + DateTime.Now.ToString("s"));
                break;
            case "UpsertItem":
                var item = body.Element(Svc + "item")!;
                var itemNo = (string)item.Element(Data + "ItemNo");
                _items[itemNo] = item;
                result = Ok(op, itemNo);
                break;
            case "PostSalesJournal":
                result = Ok(op, Doc("SJ", (string)body.Element(Svc + "journal")!.Element(Data + "ExternalReference")));
                break;
            case "PostSalesInvoice":
                result = Ok(op, Doc("SI", (string)body.Element(Svc + "invoice")!.Element(Data + "ExternalReference")));
                break;
            case "CreateTransferOrder":
                var order = body.Element(Svc + "order")!;
                var reference = (string)order.Element(Data + "ExternalReference");
                var lines = order.Element(Data + "Lines")!.Elements(Data + "ErpTransferLine")
                    .Select(l => new ReceiptLine((string)l.Element(Data + "ItemNo"), (string)l.Element(Data + "VariantCode"), (decimal)l.Element(Data + "Quantity")))
                    .ToList();
                // The DC ships the same evening; the store receipts the delivery next morning.
                AddReceipt((string)order.Element(Data + "ToSite"), reference, lines);
                result = Ok(op, Doc("TO", reference));
                break;
            case "GetGoodsReceipts":
                var site = (string)body.Element(Svc + "siteCode");
                result = new XElement(Svc + "GetGoodsReceiptsResult", new XAttribute(XNamespace.Xmlns + "a", Data), new XAttribute(XNamespace.Xmlns + "i", Xsi),
                    _receipts.Values.Where(r => !r.Acknowledged && (site == "*" || r.SiteCode == site)).OrderBy(r => r.ReceiptNo).Select(r =>
                        new XElement(Data + "ErpGoodsReceipt",
                            new XElement(Data + "ReceiptNo", r.ReceiptNo),
                            new XElement(Data + "PurchaseOrderNo", r.PurchaseOrderNo),
                            new XElement(Data + "SiteCode", r.SiteCode),
                            new XElement(Data + "PostedAt", r.PostedAt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)),
                            new XElement(Data + "Lines", r.Lines.Select(l => new XElement(Data + "ErpTransferLine",
                                new XElement(Data + "ItemNo", l.ItemNo),
                                new XElement(Data + "VariantCode", l.VariantCode),
                                new XElement(Data + "Quantity", l.Quantity.ToString(CultureInfo.InvariantCulture))))))));
                break;
            case "AcknowledgeGoodsReceipt":
                var receiptNo = (string)body.Element(Svc + "receiptNo");
                if (_receipts.TryGetValue(receiptNo, out var receipt)) receipt.Acknowledged = true;
                result = Ok(op, receiptNo);
                break;
            default:
                return Fault("UnknownOperation", "Operation " + op + " is not supported");
        }

        var response = new XElement(Svc + op + "Response", result);
        var envelope = new XDocument(new XElement(Soap + "Envelope", new XAttribute(XNamespace.Xmlns + "s", Soap), new XElement(Soap + "Body", response)));
        return Results.Text(envelope.ToString(SaveOptions.DisableFormatting), "text/xml; charset=utf-8");
    }

    private string Doc(string prefix, string reference)
    {
        var no = prefix + Interlocked.Increment(ref _documentNo);
        _journal.Enqueue(no + " " + reference);
        _logger.LogInformation("ERP posted {DocumentNo} for {Reference}", no, reference);
        return no;
    }

    // DataContractSerializer reads members in Order: Success, DocumentNo, Message.
    private static XElement Ok(string op, string documentNo) =>
        new(Svc + op + "Result", new XAttribute(XNamespace.Xmlns + "a", Data), new XAttribute(XNamespace.Xmlns + "i", Xsi),
            new XElement(Data + "Success", "true"),
            new XElement(Data + "DocumentNo", documentNo),
            new XElement(Data + "Message", "OK"));

    private static IResult Fault(string code, string message)
    {
        var envelope = new XDocument(new XElement(Soap + "Envelope", new XAttribute(XNamespace.Xmlns + "s", Soap),
            new XElement(Soap + "Body", new XElement(Soap + "Fault",
                new XElement("faultcode", "s:Client"),
                new XElement("faultstring", message),
                new XElement("detail", new XElement(Data + "ErpFault",
                    new XElement(Data + "Code", code), new XElement(Data + "Message", message), new XElement(Data + "Retryable", "false")))))));
        return new SoapFaultResult(envelope.ToString(SaveOptions.DisableFormatting));
    }
}

internal class SoapFaultResult : IResult
{
    private readonly string _xml;

    public SoapFaultResult(string xml)
    {
        _xml = xml;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = 500;
        httpContext.Response.ContentType = "text/xml; charset=utf-8";
        return httpContext.Response.WriteAsync(_xml);
    }
}
