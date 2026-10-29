using System.Globalization;
using System.Net;
using System.Text;

namespace ContosoFoods.TradeRebates.Core;

/// <summary>Renders the quarterly rebate statement sent to the distributor (HTML) and its detail (CSV).</summary>
public static class StatementRenderer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string FileBaseName(StatementHeader h) => $"TR-{h.Quarter}-{h.DistributorCode}";

    public static string RelativeFolder(StatementHeader h) => Path.Combine(h.StatementFolder, h.Quarter);

    public static string RenderHtml(StatementDocument doc, DateTime printedAt)
    {
        StatementHeader h = doc.Header;
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<title>").Append(E(FileBaseName(h))).AppendLine("</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;font-size:10pt;margin:24px}h1{font-size:16pt}" +
                      "table{border-collapse:collapse;width:100%}th,td{border-bottom:1px solid #ccc;padding:4px 6px;text-align:left}" +
                      "td.n,th.n{text-align:right}.total td{font-weight:bold;border-top:2px solid #000}.meta td{border:none;padding:1px 12px 1px 0}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine("<p><strong>Contoso Foods Oy</strong><br>Trade Marketing / Rebates<br>PL 120, 00101 Helsinki</p>");
        sb.Append("<h1>Rebate statement ").Append(E(h.Quarter)).AppendLine("</h1>");
        sb.AppendLine("<table class=\"meta\">");
        Meta(sb, "Distributor", $"{h.DistributorName} ({h.DistributorCode})");
        Meta(sb, "Vendor number", h.ErpVendorNo);
        Meta(sb, "Statement", h.StatementId.ToString(Inv));
        Meta(sb, "Printed", printedAt.ToString("yyyy-MM-dd HH:mm", Inv));
        if (!string.IsNullOrEmpty(h.BankAccountMasked))
            Meta(sb, "Paid to account", h.BankAccountMasked);
        sb.AppendLine("</table>");

        sb.AppendLine("<table><thead><tr><th>Agreement</th><th>Period</th><th class=\"n\">Quantity</th><th class=\"n\">Net value</th>" +
                      "<th class=\"n\">YTD basis</th><th class=\"n\">Rate %</th><th class=\"n\">YTD rebate</th><th class=\"n\">This period</th></tr></thead><tbody>");
        foreach (StatementLine l in doc.Lines)
        {
            decimal basis = l.Basis == "Q" ? l.YtdQuantity : l.YtdNetAmount;
            sb.Append("<tr><td>").Append(E(l.AgreementNo)).Append("<br><small>").Append(E(l.Description)).Append("</small></td>")
              .Append("<td>").Append(Periods.Format(l.Period)).Append("</td>")
              .Append("<td class=\"n\">").Append(N(l.Quantity, 0)).Append("</td>")
              .Append("<td class=\"n\">").Append(N(l.NetAmount)).Append("</td>")
              .Append("<td class=\"n\">").Append(N(basis, l.Basis == "Q" ? 0 : 2)).Append("</td>")
              .Append("<td class=\"n\">").Append(N(l.RatePercent)).Append("</td>")
              .Append("<td class=\"n\">").Append(N(l.YtdRebate)).Append("</td>")
              .Append("<td class=\"n\">").Append(N(l.AccruedAmount)).AppendLine("</td></tr>");
        }
        sb.Append("<tr class=\"total\"><td colspan=\"7\">Credit note total (").Append(E(h.Currency)).Append(")</td><td class=\"n\">")
          .Append(N(h.Amount)).AppendLine("</td></tr>");
        sb.AppendLine("</tbody></table>");

        var next = doc.Lines.Where(l => l.ToNextTier.HasValue).GroupBy(l => l.AgreementNo).Select(g => g.OrderBy(x => x.Period).Last()).ToList();
        if (next.Count > 0)
        {
            sb.AppendLine("<h2>Next tier</h2><ul>");
            foreach (StatementLine l in next)
            {
                string unit = l.Basis == "Q" ? "units" : h.Currency;
                sb.Append("<li>").Append(E(l.AgreementNo)).Append(": ").Append(N(l.ToNextTier!.Value, l.Basis == "Q" ? 0 : 2))
                  .Append(' ').Append(E(unit)).AppendLine(" to the next tier.</li>");
            }
            sb.AppendLine("</ul>");
        }

        sb.AppendLine("<p>The amount is credited to your account with Contoso Foods and settled with the next payment run. " +
                      "Questions: rebates@contosofoods.example</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    public static string RenderCsv(StatementDocument doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("StatementId;Quarter;DistributorCode;AgreementNo;Period;Basis;Quantity;NetAmount;YtdQuantity;YtdNetAmount;RatePercent;YtdRebate;AccruedAmount;Currency");
        foreach (StatementLine l in doc.Lines)
        {
            sb.Append(doc.Header.StatementId.ToString(Inv)).Append(';')
              .Append(doc.Header.Quarter).Append(';')
              .Append(Csv(doc.Header.DistributorCode)).Append(';')
              .Append(Csv(l.AgreementNo)).Append(';')
              .Append(l.Period.ToString(Inv)).Append(';')
              .Append(l.Basis).Append(';')
              .Append(l.Quantity.ToString("0.####", Inv)).Append(';')
              .Append(l.NetAmount.ToString("0.00", Inv)).Append(';')
              .Append(l.YtdQuantity.ToString("0.####", Inv)).Append(';')
              .Append(l.YtdNetAmount.ToString("0.00", Inv)).Append(';')
              .Append(l.RatePercent.ToString("0.####", Inv)).Append(';')
              .Append(l.YtdRebate.ToString("0.00", Inv)).Append(';')
              .Append(l.AccruedAmount.ToString("0.00", Inv)).Append(';')
              .AppendLine(doc.Header.Currency);
        }
        return sb.ToString();
    }

    private static void Meta(StringBuilder sb, string label, string value) =>
        sb.Append("<tr><td>").Append(E(label)).Append("</td><td>").Append(E(value)).AppendLine("</td></tr>");

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string N(decimal d, int decimals = 2) => d.ToString("N" + decimals.ToString(Inv), Inv);

    private static string Csv(string s) => s.Contains(';') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
