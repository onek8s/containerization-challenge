using System;
using System.Globalization;
using System.Net;
using System.Text;

namespace Northwind.Inspections.Core
{
    public static class ReportBuilder
    {
        private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-IE");

        public static string Subject(Inspection inspection)
        {
            return string.Format(Culture, "Inspection report {0} - {1}", inspection.ClaimNumber, inspection.PropertyAddress);
        }

        public static string Html(InspectionReport report, string portalBaseUrl)
        {
            var i = report.Inspection;
            var sb = new StringBuilder();
            sb.Append("<html><body style=\"font-family:Segoe UI,Arial,sans-serif;font-size:10pt\">");
            sb.AppendFormat("<h2>Property inspection {0}</h2>", E(i.ClaimNumber));
            sb.Append("<table cellpadding=\"4\" style=\"border-collapse:collapse\">");
            Row(sb, "Policy holder", i.PolicyHolder);
            Row(sb, "Property", i.PropertyAddress);
            Row(sb, "Peril", PerilName(i.PerilCode));
            Row(sb, "Inspected on", i.InspectedOn.ToString("d MMMM yyyy", Culture));
            Row(sb, "Adjuster", i.AdjusterName);
            Row(sb, "Estimated loss", i.EstimatedLoss.HasValue ? i.EstimatedLoss.Value.ToString("C0", Culture) : "not estimated");
            Row(sb, "Photos", report.Photos.Count.ToString(Culture));
            sb.Append("</table>");

            if (!string.IsNullOrWhiteSpace(i.Notes))
            {
                sb.Append("<h3>Adjuster notes</h3><p>");
                sb.Append(E(i.Notes).Replace("\r\n", "<br/>").Replace("\n", "<br/>"));
                sb.Append("</p>");
            }

            if (!string.IsNullOrEmpty(report.ContactSheetContentId))
            {
                sb.AppendFormat("<h3>Contact sheet</h3><img src=\"cid:{0}\" alt=\"Contact sheet\"/>", E(report.ContactSheetContentId));
            }

            sb.Append("<h3>Photos</h3><ol>");
            foreach (var p in report.Photos)
            {
                var link = (portalBaseUrl ?? string.Empty).TrimEnd('/') + "/media/" + p.Id.ToString("N");
                sb.AppendFormat("<li><a href=\"{0}\">{1}</a>{2}</li>", E(link), E(p.OriginalFileName),
                    string.IsNullOrWhiteSpace(p.Caption) ? string.Empty : " - " + E(p.Caption));
            }
            sb.Append("</ol>");
            sb.AppendFormat(Culture, "<p style=\"color:#777\">Generated {0:dd/MM/yyyy HH:mm} UTC by Property Inspections.</p>", report.GeneratedUtc);
            sb.Append("</body></html>");
            return sb.ToString();
        }

        public static string PerilName(string code)
        {
            switch ((code ?? string.Empty).ToUpperInvariant())
            {
                case "FIR": return "Fire";
                case "WAT": return "Escape of water";
                case "STO": return "Storm";
                case "FLO": return "Flood";
                case "SUB": return "Subsidence";
                case "THE": return "Theft";
                case "IMP": return "Impact";
                default: return string.IsNullOrEmpty(code) ? "Unknown" : code;
            }
        }

        private static void Row(StringBuilder sb, string label, string value)
        {
            sb.AppendFormat("<tr><td style=\"color:#555\">{0}</td><td>{1}</td></tr>", E(label), E(value));
        }

        private static string E(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
