using Humongous.Documents.Wording;
using Humongous.Quoting.Contracts.Messages;
using Humongous.Quoting.Domain;
using MigraDocCore.DocumentObjectModel;
using MigraDocCore.DocumentObjectModel.Tables;
using MigraDocCore.Rendering;
using Microsoft.Extensions.Options;

namespace Humongous.Documents.Rendering;

public sealed class DocumentOptions
{
    public const string SectionName = "Documents";

    public string FontFamily { get; set; } = "Arial";
    public string InsurerName { get; set; } = "Humongous Insurance Ltd";
    public string InsurerAddress { get; set; } = "2 Wharf Street, Leeds LS2 7EQ";
    public string FcaStatement { get; set; } = "Humongous Insurance Ltd is authorised and regulated by the Financial Conduct Authority (FRN 700123).";
    public string ElCertificateSignatory { get; set; } = "Head of Underwriting";
}

/// <summary>Lays out quote and policy documents and renders them to PDF.</summary>
public sealed class DocumentComposer(ClauseLibrary clauses, IOptions<DocumentOptions> options)
{
    private static readonly Color Brand = new(0x0B, 0x3D, 0x5C);

    public (string FileName, byte[] Pdf) Render(DocumentModel model)
    {
        var document = NewDocument(TitleFor(model.Type));
        var section = document.LastSection;

        switch (model.Type)
        {
            case DocumentType.QuoteSchedule:
                QuoteSchedule(section, model);
                break;
            case DocumentType.RenewalInvitation:
                RenewalInvitation(section, model);
                break;
            case DocumentType.PolicySchedule:
                PolicySchedule(section, model);
                break;
            case DocumentType.ElCertificate:
                ElCertificate(section, model);
                break;
            case DocumentType.AdjustmentEndorsement:
                Endorsement(section, model);
                break;
            case DocumentType.CancellationNotice:
                Cancellation(section, model);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(model), model.Type, "Unknown document type.");
        }

        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Info.Title = TitleFor(model.Type);
        renderer.PdfDocument.Info.Author = options.Value.InsurerName;
        renderer.PdfDocument.Save(stream, false);
        return (FileNameFor(model), stream.ToArray());
    }

    private Document NewDocument(string title)
    {
        var o = options.Value;
        var document = new Document();
        document.Info.Title = title;
        var normal = document.Styles[StyleNames.Normal];
        normal.Font.Name = o.FontFamily;
        normal.Font.Size = 9.5;
        var heading = document.Styles[StyleNames.Heading1];
        heading.Font.Size = 15;
        heading.Font.Bold = true;
        heading.Font.Color = Brand;
        heading.ParagraphFormat.SpaceAfter = 8;
        var heading2 = document.Styles[StyleNames.Heading2];
        heading2.Font.Size = 11;
        heading2.Font.Bold = true;
        heading2.Font.Color = Brand;
        heading2.ParagraphFormat.SpaceBefore = 10;
        heading2.ParagraphFormat.SpaceAfter = 4;

        var section = document.AddSection();
        section.PageSetup = document.DefaultPageSetup.Clone();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(3);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2);

        var header = section.Headers.Primary.AddParagraph();
        header.AddFormattedText(o.InsurerName, TextFormat.Bold).Color = Brand;
        header.AddLineBreak();
        header.AddText(o.InsurerAddress);
        header.Format.Font.Size = 9;

        var footer = section.Footers.Primary.AddParagraph();
        footer.AddText(o.FcaStatement + "  Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();
        footer.Format.Font.Size = 7.5;

        section.AddParagraph(title, StyleNames.Heading1);
        return document;
    }

    private void QuoteSchedule(Section section, DocumentModel m)
    {
        var q = m.Quote!;
        KeyValues(section,
            ("Quote reference", q.Reference),
            ("Broker", $"{m.Agency.Name} ({m.Agency.Code})"),
            ("Broker reference", q.BrokerReference ?? ""),
            ("Proposed insured", q.InsuredName),
            ("Proposed inception", q.InceptionDate.ToString("dd MMMM yyyy")),
            ("Quote valid until", q.ValidUntil?.ToString("dd MMMM yyyy") ?? ""),
            ("Product", $"Business Combined {q.ProductVersion}"),
            ("Status", q.Status == QuoteStatus.Accepted ? "Terms agreed by underwriter" : "Quoted"));
        RiskSection(section, q.Risk);
        PremiumTable(section, q.Premium!);
        if (q.UnderwriterNote is { Length: > 0 } note)
        {
            section.AddParagraph("Underwriter's note", StyleNames.Heading2);
            section.AddParagraph(note);
        }
        Clauses(section, q.Clauses);
        section.AddParagraph(
            "This quotation is based on the information provided by the broker. Any change to that information must be " +
            "disclosed before cover is bound. The quotation is not a contract of insurance.").Format.SpaceBefore = 10;
    }

    private void RenewalInvitation(Section section, DocumentModel m)
    {
        var q = m.Quote!;
        var p = m.Policy!;
        section.AddParagraph($"Policy {p.PolicyNumber} for {p.InsuredName} expires on {p.ExpiryDate:dd MMMM yyyy}. " +
                             "We are pleased to invite renewal on the terms below.");
        KeyValues(section,
            ("Renewal quote", q.Reference),
            ("Renewal date", q.InceptionDate.ToString("dd MMMM yyyy")),
            ("Expiring gross premium", Money.Format(p.AnnualGrossPremium)),
            ("Renewal gross premium", Money.Format(q.GrossPremium ?? 0)),
            ("Change", p.AnnualGrossPremium == 0 ? "" : $"{(q.GrossPremium ?? 0) / p.AnnualGrossPremium - 1:P1}"));
        PremiumTable(section, q.Premium!);
        Clauses(section, q.Clauses);
        section.AddParagraph("Please review the sums insured and the information held and tell us of any changes before the renewal date.")
            .Format.SpaceBefore = 10;
    }

    private void PolicySchedule(Section section, DocumentModel m)
    {
        var p = m.Policy!;
        KeyValues(section,
            ("Policy number", p.PolicyNumber),
            ("Insured", p.InsuredName),
            ("Address", $"{p.Risk.Insured.AddressLine1}, {p.Risk.Insured.Town} {p.Postcode}"),
            ("Business", TradeList.Find(p.TradeCode)?.Description ?? p.TradeCode),
            ("Period of insurance", $"{p.InceptionDate:dd MMMM yyyy} to {p.ExpiryDate:dd MMMM yyyy} both days inclusive"),
            ("Broker", $"{m.Agency.Name} ({m.Agency.Code})"),
            ("Product", $"Business Combined {p.ProductVersion}"));
        RiskSection(section, p.Risk);
        PremiumTable(section, p.Premium);
        Clauses(section, p.Clauses);
    }

    private void ElCertificate(Section section, DocumentModel m)
    {
        var p = m.Policy!;
        section.AddParagraph("Certificate of Employers' Liability Insurance", StyleNames.Heading2);
        section.AddParagraph("(Where required by regulation 5 of the Employers' Liability (Compulsory Insurance) Regulations 1998, " +
                             "one or more copies of this certificate must be displayed at each place of business at which the policy " +
                             "holder employs persons covered by the policy.)").Format.Font.Italic = true;
        KeyValues(section,
            ("1. Policy number", p.PolicyNumber),
            ("2. Name of policy holder", p.InsuredName),
            ("3. Date of commencement of insurance policy", p.InceptionDate.ToString("dd MMMM yyyy")),
            ("4. Date of expiry of insurance policy", p.ExpiryDate.ToString("dd MMMM yyyy")),
            ("Employer's Reference Number", "Exempt / to be provided by the policy holder"));
        section.AddParagraph(
            "We hereby certify that subject to paragraph 2: 1. the policy to which this certificate relates satisfies the " +
            "requirements of the relevant law applicable in Great Britain, Northern Ireland, the Isle of Man, the Island of " +
            "Jersey, the Island of Guernsey and the Island of Alderney; 2. the minimum amount of cover provided by this policy " +
            $"is no less than {Money.Format(EmployersLiabilityCover.StandardLimit)}.").Format.SpaceBefore = 10;
        section.AddParagraph($"Signed on behalf of {options.Value.InsurerName} (Authorised Insurer)").Format.SpaceBefore = 16;
        section.AddParagraph(options.Value.ElCertificateSignatory);
    }

    private void Endorsement(Section section, DocumentModel m)
    {
        var p = m.Policy!;
        var t = m.Transaction!;
        KeyValues(section,
            ("Policy number", p.PolicyNumber),
            ("Insured", p.InsuredName),
            ("Effective date", t.EffectiveDate.ToString("dd MMMM yyyy")),
            ("Reason", t.Reason ?? ""),
            (t.GrossPremium >= 0 ? "Additional premium" : "Return premium", Money.Format(Math.Abs(t.GrossPremium))),
            ("Insurance premium tax", Money.Format(Math.Abs(t.Ipt))),
            ("Total", Money.Format(Math.Abs(t.GrossPremium + t.Ipt))));
        section.AddParagraph("With effect from the date shown the policy is amended as set out in the revised schedule below. " +
                             "All other terms and conditions remain unaltered.").Format.SpaceBefore = 8;
        RiskSection(section, p.Risk);
        PremiumTable(section, p.Premium);
    }

    private void Cancellation(Section section, DocumentModel m)
    {
        var p = m.Policy!;
        var t = m.Transaction!;
        KeyValues(section,
            ("Policy number", p.PolicyNumber),
            ("Insured", p.InsuredName),
            ("Cancelled from", t.EffectiveDate.ToString("dd MMMM yyyy")),
            ("Reason", t.Reason ?? ""),
            ("Return premium", Money.Format(-t.GrossPremium)),
            ("Return IPT", Money.Format(-t.Ipt)));
        section.AddParagraph("Cover under this policy ends at 23:59 on the date shown. The policy fee is not refundable.")
            .Format.SpaceBefore = 8;
    }

    private static void RiskSection(Section section, RiskDetails risk)
    {
        section.AddParagraph("Cover", StyleNames.Heading2);
        var rows = new List<(string, string)>();
        if (risk.MaterialDamage is { } md)
        {
            rows.Add(("Buildings", Money.Format(md.Buildings)));
            rows.Add(("Contents", Money.Format(md.Contents)));
            rows.Add(("Stock", Money.Format(md.Stock)));
            rows.Add(("Computer equipment", Money.Format(md.ComputerEquipment)));
            rows.Add(("Material damage excess", Money.Format(md.Excess)));
        }
        if (risk.BusinessInterruption is { } bi)
            rows.Add(("Gross profit", $"{Money.Format(bi.GrossProfit)}, indemnity period {bi.IndemnityPeriodMonths} months"));
        if (risk.EmployersLiability is { } el)
            rows.Add(("Employers' liability", $"{Money.Format(EmployersLiabilityCover.StandardLimit)}; {el.Employees} employees, wageroll {Money.Format(el.Wageroll)}"));
        if (risk.PublicLiability is { } pl)
            rows.Add(("Public and products liability", Money.Format(pl.LimitOfIndemnity)));
        KeyValues(section, [.. rows]);
    }

    private static void PremiumTable(Section section, PremiumBreakdown premium)
    {
        section.AddParagraph("Premium", StyleNames.Heading2);
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.LightGray;
        table.AddColumn(Unit.FromCentimeter(8));
        table.AddColumn(Unit.FromCentimeter(4)).Format.Alignment = ParagraphAlignment.Right;
        var header = table.AddRow();
        header.Shading.Color = new Color(0xDD, 0xE6, 0xEE);
        header.Cells[0].AddParagraph("Section");
        header.Cells[1].AddParagraph("Gross premium");
        header.Format.Font.Bold = true;
        foreach (var s in premium.Sections)
            Row(table, s.Section.DisplayName(), Money.Format(s.Gross));
        Row(table, "Gross premium", Money.Format(premium.GrossPremium), bold: true);
        Row(table, $"Insurance premium tax at {premium.IptRate:P0}", Money.Format(premium.Ipt));
        if (premium.Fee > 0) Row(table, "Policy fee", Money.Format(premium.Fee));
        Row(table, "Total payable", Money.Format(premium.Total), bold: true);
    }

    private void Clauses(Section section, IReadOnlyList<string> codes)
    {
        if (codes.Count == 0) return;
        section.AddParagraph("Endorsements", StyleNames.Heading2);
        foreach (var code in codes)
        {
            var clause = clauses.Get(code);
            var p = section.AddParagraph();
            p.AddFormattedText($"{code} {clause.Title}. ", TextFormat.Bold);
            p.AddText(clause.Text);
            p.Format.SpaceAfter = 4;
        }
    }

    private static void KeyValues(Section section, params (string Key, string Value)[] rows)
    {
        var table = section.AddTable();
        table.AddColumn(Unit.FromCentimeter(6));
        table.AddColumn(Unit.FromCentimeter(11));
        foreach (var (key, value) in rows)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(key).Format.Font.Bold = true;
            row.Cells[1].AddParagraph(value);
            row.BottomPadding = 2;
        }
    }

    private static void Row(Table table, string label, string amount, bool bold = false)
    {
        var row = table.AddRow();
        row.Cells[0].AddParagraph(label);
        row.Cells[1].AddParagraph(amount);
        row.Format.Font.Bold = bold;
    }

    private static string TitleFor(DocumentType type) => type switch
    {
        DocumentType.QuoteSchedule => "Quotation schedule",
        DocumentType.PolicySchedule => "Policy schedule",
        DocumentType.ElCertificate => "Employers' liability certificate",
        DocumentType.AdjustmentEndorsement => "Endorsement",
        DocumentType.CancellationNotice => "Notice of cancellation",
        DocumentType.RenewalInvitation => "Renewal invitation",
        _ => type.ToString()
    };

    private static string FileNameFor(DocumentModel m)
    {
        var reference = (m.Policy?.PolicyNumber ?? m.Quote?.Reference ?? "document").Replace('/', '-');
        var suffix = m.Type switch
        {
            DocumentType.QuoteSchedule => "quote",
            DocumentType.PolicySchedule => "schedule",
            DocumentType.ElCertificate => "el-certificate",
            DocumentType.AdjustmentEndorsement => $"endorsement-{m.Transaction?.EffectiveDate:yyyyMMdd}",
            DocumentType.CancellationNotice => "cancellation",
            DocumentType.RenewalInvitation => $"renewal-{m.Quote?.Reference}",
            _ => "document"
        };
        return $"{reference}-{suffix}.pdf";
    }
}
