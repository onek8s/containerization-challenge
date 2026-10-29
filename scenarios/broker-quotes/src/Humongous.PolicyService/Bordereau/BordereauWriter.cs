using System.Globalization;
using System.Text.Json;
using Humongous.Quoting.Data;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace Humongous.PolicyService.Bordereau;

/// <summary>
/// Monthly premium bordereau in the layout agreed with Lucerne Capacity (schedule 4 of the binding
/// authority agreement): one row per transaction, section split, then a summary sheet.
/// </summary>
public static class BordereauWriter
{
    private static readonly string[] Sections = ["MD", "BI", "EL", "PL"];

    private static readonly string[] Headers =
    [
        "UMR", "Coverholder", "Agency", "Agency name", "Policy number", "Insured", "Postcode", "Trade code",
        "Product version", "Inception", "Expiry", "Transaction", "Effective", "Processed",
        "Gross premium", "MD gross", "BI gross", "EL gross", "PL gross",
        "Commission", "Net premium", "IPT", "IPT rate", "Fee", "Currency"
    ];

    public static byte[] Write(string period, string umr, IReadOnlyList<BordereauLine> lines)
    {
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Premium");

        for (var c = 0; c < Headers.Length; c++)
        {
            sheet.Cells[1, c + 1].Value = Headers[c];
            sheet.Cells[1, c + 1].Style.Font.Bold = true;
            sheet.Cells[1, c + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            sheet.Cells[1, c + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0xDD, 0xE6, 0xEE));
        }

        var row = 2;
        foreach (var l in lines)
        {
            var split = JsonSerializer.Deserialize<Dictionary<string, decimal>>(l.SectionGrossJson) ?? [];
            var c = 1;
            sheet.Cells[row, c++].Value = umr;
            sheet.Cells[row, c++].Value = "Humongous Insurance Ltd";
            sheet.Cells[row, c++].Value = l.AgencyCode;
            sheet.Cells[row, c++].Value = l.AgencyName;
            sheet.Cells[row, c++].Value = l.PolicyNumber;
            sheet.Cells[row, c++].Value = l.InsuredName;
            sheet.Cells[row, c++].Value = l.Postcode;
            sheet.Cells[row, c++].Value = l.TradeCode;
            sheet.Cells[row, c++].Value = l.ProductVersion;
            Date(sheet.Cells[row, c++], l.InceptionDate);
            Date(sheet.Cells[row, c++], l.ExpiryDate);
            sheet.Cells[row, c++].Value = TransactionCode(l.TransactionType);
            Date(sheet.Cells[row, c++], l.EffectiveDate);
            Date(sheet.Cells[row, c++], l.CreatedUtc);
            Amount(sheet.Cells[row, c++], l.GrossPremium);
            foreach (var s in Sections)
                Amount(sheet.Cells[row, c++], split.GetValueOrDefault(s));
            Amount(sheet.Cells[row, c++], l.Commission);
            Amount(sheet.Cells[row, c++], l.GrossPremium - l.Commission);
            Amount(sheet.Cells[row, c++], l.Ipt);
            sheet.Cells[row, c++].Value = l.GrossPremium == 0 ? 0 : Math.Round(l.Ipt / l.GrossPremium, 2);
            Amount(sheet.Cells[row, c++], l.Fee);
            sheet.Cells[row, c].Value = "GBP";
            row++;
        }

        // Fixed widths: AutoFitColumns needs GDI+ on the server.
        for (var c = 1; c <= Headers.Length; c++)
            sheet.Column(c).Width = c is 4 or 6 ? 34 : 14;
        sheet.View.FreezePanes(2, 1);

        var summary = package.Workbook.Worksheets.Add("Summary");
        summary.Cells["A1"].Value = "Premium bordereau";
        summary.Cells["A1"].Style.Font.Bold = true;
        summary.Cells["A2"].Value = "UMR";
        summary.Cells["B2"].Value = umr;
        summary.Cells["A3"].Value = "Period";
        summary.Cells["B3"].Value = period;
        summary.Cells["A4"].Value = "Transactions";
        summary.Cells["B4"].Value = lines.Count;
        summary.Cells["A6"].Value = "Transaction";
        summary.Cells["B6"].Value = "Count";
        summary.Cells["C6"].Value = "Gross premium";
        summary.Cells["D6"].Value = "Commission";
        summary.Cells["E6"].Value = "IPT";
        var r = 7;
        foreach (var group in lines.GroupBy(l => TransactionCode(l.TransactionType)).OrderBy(g => g.Key))
        {
            summary.Cells[r, 1].Value = group.Key;
            summary.Cells[r, 2].Value = group.Count();
            Amount(summary.Cells[r, 3], group.Sum(l => l.GrossPremium));
            Amount(summary.Cells[r, 4], group.Sum(l => l.Commission));
            Amount(summary.Cells[r, 5], group.Sum(l => l.Ipt));
            r++;
        }
        summary.Cells[r, 1].Value = "Total";
        summary.Cells[r, 1].Style.Font.Bold = true;
        summary.Cells[r, 2].Value = lines.Count;
        Amount(summary.Cells[r, 3], lines.Sum(l => l.GrossPremium));
        Amount(summary.Cells[r, 4], lines.Sum(l => l.Commission));
        Amount(summary.Cells[r, 5], lines.Sum(l => l.Ipt));
        for (var c = 1; c <= 5; c++) summary.Column(c).Width = 18;

        package.Workbook.Properties.Title = $"Humongous Insurance premium bordereau {period}";
        package.Workbook.Properties.Company = "Humongous Insurance Ltd";
        return package.GetAsByteArray();
    }

    private static string TransactionCode(string type) => type switch
    {
        "NewBusiness" => "NB",
        "MidTermAdjustment" => "MTA",
        "Renewal" => "REN",
        "Cancellation" => "CAN",
        _ => type
    };

    private static void Date(ExcelRange cell, DateTime value)
    {
        cell.Value = value.Date;
        cell.Style.Numberformat.Format = "dd/mm/yyyy";
    }

    private static void Amount(ExcelRange cell, decimal value)
    {
        cell.Value = value;
        cell.Style.Numberformat.Format = "#,##0.00";
    }

    public static string FileName(string umr, string period) =>
        string.Create(CultureInfo.InvariantCulture, $"HUMONGOUS_{umr}_PREMIUM_{period.Replace("-", "")}.xlsx");
}
