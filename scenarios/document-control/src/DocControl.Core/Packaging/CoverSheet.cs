using System.Globalization;

namespace Litware.DocControl.Packaging;

public static class CoverSheet
{
    private const double Left = 50;
    private const double Right = SimplePdfWriter.PageWidth - 50;
    private const double RowHeight = 14;
    private const double BottomMargin = 780;

    public static byte[] Render(TransmittalHeader header, IReadOnlyList<PackagedFile> files)
    {
        var pdf = new SimplePdfWriter { Title = header.Number, Author = header.IssuedBy };
        var page = 0;
        double y = 0;

        void StartPage()
        {
            pdf.NewPage();
            page++;
            pdf.Text(Left, 60, "LITWARE ENGINEERING", 16, bold: true);
            pdf.Text(Left, 78, "Document transmittal", 12);
            pdf.Text(Right - 150, 60, header.Number, 12, bold: true);
            pdf.Text(Right - 150, 78, string.Create(CultureInfo.InvariantCulture, $"Page {page}"), 9);
            pdf.Line(Left, 88, Right, 88, 1);
            y = 108;

            if (page == 1)
            {
                Field("Project", $"{header.ProjectCode}  {header.ProjectName}");
                Field("To", string.IsNullOrWhiteSpace(header.RecipientName) ? header.RecipientCompany : $"{header.RecipientName}, {header.RecipientCompany}");
                Field("Purpose", header.Purpose);
                Field("Issued by", header.IssuedBy);
                Field("Date", header.IssuedUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                if (!string.IsNullOrWhiteSpace(header.Notes)) Field("Notes", header.Notes!);
                y += 10;
            }

            pdf.Text(Left, y, "Document number", 9, bold: true);
            pdf.Text(Left + 150, y, "Rev", 9, bold: true);
            pdf.Text(Left + 185, y, "Title", 9, bold: true);
            pdf.Text(Right - 60, y, "Size", 9, bold: true);
            pdf.Line(Left, y + 4, Right, y + 4);
            y += RowHeight + 2;
        }

        void Field(string label, string value)
        {
            pdf.Text(Left, y, label, 10, bold: true);
            pdf.Text(Left + 80, y, Truncate(value, 80), 10);
            y += RowHeight + 2;
        }

        StartPage();
        foreach (var f in files)
        {
            if (y > BottomMargin) StartPage();
            pdf.Text(Left, y, f.DocumentNumber, 9);
            pdf.Text(Left + 150, y, f.Revision, 9);
            pdf.Text(Left + 185, y, Truncate(f.Title, 52), 9);
            pdf.Text(Right - 60, y, FormatSize(f.Size), 9);
            y += RowHeight;
        }

        if (y > BottomMargin - 60) StartPage();
        y += 30;
        pdf.Line(Left, y, Left + 200, y);
        pdf.Text(Left, y + 12, "Received by (name, date, signature)", 8);
        pdf.Text(Left, y + 30, "Please return a signed copy or acknowledge by e-mail within 5 working days.", 8);

        return pdf.ToArray();
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):0.#} MB"),
    };

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
