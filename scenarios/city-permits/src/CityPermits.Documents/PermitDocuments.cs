using CityPermits.Domain.Fees;
using CityPermits.Domain.Model;
using MigraDocCore.DocumentObjectModel;
using MigraDocCore.DocumentObjectModel.Tables;
using MigraDocCore.Rendering;
using QRCoder;

namespace CityPermits.Documents;

public sealed record GeneratedDocument(DocumentKind Kind, string FileName, byte[] Content, string ContentType = "application/pdf");

/// <summary>Letters, permits and certificates in the council's corporate layout (Brand guidelines 2021).</summary>
public sealed class PermitDocuments
{
    private readonly string _verifyBaseUrl;

    static PermitDocuments()
    {
        if (MigraDocCore.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes.ImageSource.ImageSourceImpl is null)
            MigraDocCore.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes.ImageSource.ImageSourceImpl =
                new PdfSharpCore.Utils.ImageSharpImageSource<SixLabors.ImageSharp.PixelFormats.Rgba32>();
    }

    public PermitDocuments(string verifyBaseUrl = "https://permits.ashford.gov.uk/verify/") => _verifyBaseUrl = verifyBaseUrl;

    public GeneratedDocument DecisionLetter(PermitCase c, byte[]? sitePlanPng)
    {
        var doc = NewDocument($"Decision {c.Reference}");
        var s = Letterhead(doc, c);
        var refused = c.Outcome == DecisionOutcome.Refused;
        s.AddParagraph(refused ? "Notice of refusal" : "Notice of decision", "Heading1");
        s.AddParagraph($"Application: {c.Title}");
        s.AddParagraph($"Site: {c.SiteAddress}");
        s.AddParagraph($"Decision date: {c.DecidedUtc:d MMMM yyyy}");
        s.AddParagraph(refused
            ? "The City of Ashford Council hereby REFUSES the application for the following reasons:"
            : "The City of Ashford Council hereby GRANTS the application subject to the conditions below.");
        if (!string.IsNullOrEmpty(c.DecisionNotes)) s.AddParagraph(c.DecisionNotes);
        if (c.Conditions.Count > 0)
        {
            s.AddParagraph("Conditions", "Heading2");
            foreach (var cond in c.Conditions.OrderBy(x => x.Order))
                s.AddParagraph($"{cond.Order}. {cond.Text}");
        }
        if (sitePlanPng is not null)
        {
            s.AddParagraph("Site location plan", "Heading2");
            var img = s.AddImage(ImageSource(sitePlanPng));
            img.Width = Unit.FromCentimeter(16);
        }
        s.AddParagraph(refused
            ? "You may appeal against this decision within 6 months of the date of this notice."
            : "This permission does not remove the need to comply with any other legislation.", "Small");
        return new GeneratedDocument(DocumentKind.DecisionLetter, $"{Safe(c.Reference)}-decision.pdf", Render(doc));
    }

    public GeneratedDocument PermitCertificate(PermitCase c)
    {
        var doc = NewDocument($"Permit {c.Reference}");
        var s = Letterhead(doc, c);
        var title = c.Kind switch
        {
            PermitKind.BusinessLicence => $"{FeeCalculator.Display(c.Business!.Category)} licence",
            PermitKind.BuildingPermit => "Building permit",
            _ => "Parking permit"
        };
        s.AddParagraph(title, "Heading1");
        var t = s.AddTable();
        t.Borders.Width = 0.5;
        t.AddColumn(Unit.FromCentimeter(5));
        t.AddColumn(Unit.FromCentimeter(11));
        void Row(string k, string? v) { var r = t.AddRow(); r.Cells[0].AddParagraph(k); r.Cells[1].AddParagraph(v ?? ""); }
        Row("Reference", c.Reference);
        Row("Holder", c.Applicant?.DisplayName);
        if (c.Business is { } b) Row("Trading name", b.TradingName);
        Row("Premises / site", c.SiteAddress);
        Row("Valid from", c.ValidFrom?.ToString("d MMM yyyy"));
        Row("Valid to", c.ValidTo?.ToString("d MMM yyyy"));
        if (c.Parking is { } p) { Row("Zone", p.ZoneCode); Row("Vehicle", p.Registration); Row("Serial", p.PermitSerial); }
        foreach (var cond in c.Conditions.OrderBy(x => x.Order)) Row($"Condition {cond.Order}", cond.Text);
        s.AddParagraph();
        var qr = s.AddImage(ImageSource(Qr(_verifyBaseUrl + Uri.EscapeDataString(c.Reference))));
        qr.Width = Unit.FromCentimeter(3.5);
        s.AddParagraph("Scan to verify this permit.", "Small");
        var kind = c.Kind == PermitKind.ParkingPermit ? DocumentKind.ParkingPermit : DocumentKind.PermitCertificate;
        return new GeneratedDocument(kind, $"{Safe(c.Reference)}-permit.pdf", Render(doc));
    }

    public GeneratedDocument RenewalReminder(PermitCase c, int reminderNumber, string renewUrl)
    {
        var doc = NewDocument($"Renewal {c.Reference}");
        var s = Letterhead(doc, c);
        s.AddParagraph(reminderNumber >= 3 ? "Final reminder: your permit is about to expire" : "Time to renew your permit", "Heading1");
        s.AddParagraph($"Your {c.Title.ToLowerInvariant()} ({c.Reference}) expires on {c.ValidTo:d MMMM yyyy}.");
        s.AddParagraph($"Renew online at {renewUrl} or call 01233 330000.");
        return new GeneratedDocument(DocumentKind.RenewalNotice, $"{Safe(c.Reference)}-renewal-{reminderNumber}.pdf", Render(doc));
    }

    public GeneratedDocument Invoice(PermitCase c, string invoiceNumber)
    {
        var doc = NewDocument($"Invoice {invoiceNumber}");
        var s = Letterhead(doc, c);
        s.AddParagraph($"Invoice {invoiceNumber}", "Heading1");
        var t = s.AddTable();
        t.Borders.Width = 0.5;
        t.AddColumn(Unit.FromCentimeter(12));
        t.AddColumn(Unit.FromCentimeter(4)).Format.Alignment = ParagraphAlignment.Right;
        foreach (var f in c.Fees.Where(f => !f.Waived))
        {
            var r = t.AddRow();
            r.Cells[0].AddParagraph($"{f.Code} {f.Description}");
            r.Cells[1].AddParagraph($"£{f.Amount:N2}");
        }
        var tot = t.AddRow();
        tot.Cells[0].AddParagraph("Total").Format.Font.Bold = true;
        tot.Cells[1].AddParagraph($"£{c.TotalFees:N2}").Format.Font.Bold = true;
        return new GeneratedDocument(DocumentKind.Invoice, $"{Safe(c.Reference)}-{invoiceNumber}.pdf", Render(doc));
    }

    private static Document NewDocument(string title)
    {
        var doc = new Document();
        doc.Info.Title = title;
        doc.Info.Author = "City of Ashford Council";
        var normal = doc.Styles["Normal"];
        normal.Font.Name = "Arial";
        normal.Font.Size = 10;
        normal.ParagraphFormat.SpaceAfter = 6;
        doc.Styles["Heading1"].Font.Size = 16;
        doc.Styles["Heading1"].Font.Bold = true;
        doc.Styles["Heading1"].ParagraphFormat.SpaceAfter = 10;
        doc.Styles["Heading2"].Font.Size = 12;
        doc.Styles["Heading2"].Font.Bold = true;
        doc.Styles.AddStyle("Small", "Normal").Font.Size = 8;
        return doc;
    }

    private static Section Letterhead(Document doc, PermitCase c)
    {
        var s = doc.AddSection();
        s.PageSetup.PageFormat = PageFormat.A4;
        s.PageSetup.TopMargin = Unit.FromCentimeter(3);
        var h = s.Headers.Primary.AddParagraph("City of Ashford Council · Civic Centre, Tannery Lane, Ashford TN23 1PL");
        h.Format.Font.Size = 8;
        var f = s.Footers.Primary.AddParagraph($"Reference {c.Reference} · permits@ashford.gov.uk · 01233 330000");
        f.Format.Font.Size = 8;
        if (c.Applicant is { } a)
        {
            s.AddParagraph(a.DisplayName);
            if (!string.IsNullOrEmpty(a.PostalAddress)) s.AddParagraph(a.PostalAddress);
            if (!string.IsNullOrEmpty(a.Postcode)) s.AddParagraph(a.Postcode);
        }
        s.AddParagraph(DateTime.Today.ToString("d MMMM yyyy")).Format.Alignment = ParagraphAlignment.Right;
        return s;
    }

    private static MigraDocCore.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes.ImageSource.IImageSource ImageSource(byte[] png) =>
        MigraDocCore.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes.ImageSource.FromBinary(Guid.NewGuid().ToString("N") + ".png", () => png);

    private static byte[] Qr(string text)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(8);
    }

    private static byte[] Render(Document doc)
    {
        var r = new PdfDocumentRenderer(true) { Document = doc };
        r.RenderDocument();
        using var ms = new MemoryStream();
        r.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    private static string Safe(string reference) => reference.Replace('/', '-');
}
