using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Lucerne.Lending.Integration;

namespace Lucerne.Lending.LabStubs;

/// <summary>Test double of the Nordic Credit consumer report service with fixed answers for the training customers.</summary>
public static class BureauStub
{
    private static readonly Dictionary<string, Action<ConsumerReport>> Fixtures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["150385-2481"] = r => { r.Score = 88; r.ReportedLoans = 2; },
        ["020790-317C"] = r => { r.Score = 61; r.ReportedLoans = 3; r.CreditInquiries12M = 2; },
        ["231178-426K"] = r => { r.Score = 96; r.ReportedLoans = 1; },
        ["110601A139N"] = r => { r.Score = 54; r.CreditInquiries12M = 7; },
        ["300888-562M"] = r =>
        {
            r.Score = 12;
            r.PaymentDefaultCount = 2;
            r.PaymentDefaults.Add(new PaymentDefault { RegisteredDate = DateTime.Today.AddMonths(-8), Creditor = "Kulutusluotto Oy", Type = "Court judgment", Amount = 2311.40m, Source = "District court" });
            r.PaymentDefaults.Add(new PaymentDefault { RegisteredDate = DateTime.Today.AddMonths(-3), Creditor = "Teleoperaattori Oyj", Type = "Creditor notice", Amount = 410.00m, Source = "Creditor" });
        },
        ["051259-2152"] = r => { r.Score = 90; },
        ["190492-6440"] = r => { r.Score = 72; },
        ["080883-3776"] = r => { r.Score = 40; r.CreditBlock = true; },
        ["271095-482J"] = r => { r.Score = 70; r.ReportedLoans = 1; },
        ["140772-159C"] = r => { r.Score = 35; r.ReportedLoans = 4; },
        ["121186-236D"] = r => { r.Score = 79; },
        ["090397-5289"] = r => { r.Score = 66; r.DebtRestructuring = true; }
    };

    public static async Task<IResult> HandleAsync(HttpRequest request)
    {
        var doc = await XDocument.LoadAsync(request.Body, LoadOptions.None, CancellationToken.None);
        XNamespace ns = BureauNamespaces.Service;
        var req = doc.Descendants(ns + "Request").FirstOrDefault();
        var personId = req?.Element(ns + "PersonId")?.Value ?? "";
        var userId = req?.Element(ns + "UserId")?.Value ?? "";
        var report = new ConsumerReport
        {
            RequestId = "NC" + DateTime.UtcNow.ToString("yyMMddHHmmssfff"),
            PersonId = personId,
            ResponseStatus = string.IsNullOrEmpty(userId) ? "AUTH_FAILED" : "OK",
            PersonFound = true,
            Score = 75,
            ScoreBand = "B"
        };
        if (personId.EndsWith("-000X", StringComparison.OrdinalIgnoreCase)) report.PersonFound = false;
        if (Fixtures.TryGetValue(personId, out var fixture)) fixture(report);
        report.ScoreBand = report.Score switch { >= 90 => "AAA", >= 80 => "AA", >= 60 => "A", >= 40 => "B", _ => "C" };
        return Results.Content(Envelope(report), "text/xml; charset=utf-8");
    }

    private static string Envelope(ConsumerReport report)
    {
        var serializer = new XmlSerializer(typeof(ConsumerReport), new XmlRootAttribute("Report") { Namespace = BureauNamespaces.Service });
        var body = new XDocument();
        using (var writer = body.CreateWriter()) serializer.Serialize(writer, report);
        XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
        XNamespace ns = BureauNamespaces.Service;
        var envelope = new XElement(soap + "Envelope", new XAttribute(XNamespace.Xmlns + "s", soap),
            new XElement(soap + "Body", new XElement(ns + "GetConsumerReportResponse", body.Root)));
        return envelope.ToString(SaveOptions.DisableFormatting);
    }
}
