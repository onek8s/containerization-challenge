using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Tailspin.CustomerCare.Domain
{
    public class QueueStats
    {
        public string QueueCode { get; set; }
        public string QueueName { get; set; }
        public int Open { get; set; }
        public int Unassigned { get; set; }
        public int AtRisk { get; set; }
        public int Breached { get; set; }
        public int CreatedToday { get; set; }
        public int ResolvedToday { get; set; }
        public double? AverageResolveHours { get; set; }
    }

    public class DigestMessage
    {
        public string Subject { get; set; }
        public string HtmlBody { get; set; }
    }

    /// <summary>Morning summary e-mail for supervisors.</summary>
    public static class DigestBuilder
    {
        public static DigestMessage Build(DateTime localDate, IEnumerable<QueueStats> stats, string portalUrl)
        {
            var list = (stats ?? Enumerable.Empty<QueueStats>()).OrderBy(s => s.QueueName).ToList();
            var culture = CultureInfo.GetCultureInfo("en-GB");

            var open = list.Sum(s => s.Open);
            var breached = list.Sum(s => s.Breached);

            var sb = new StringBuilder();
            sb.Append("<html><body style=\"font-family:Segoe UI,Arial,sans-serif;font-size:10pt\">");
            sb.AppendFormat(culture, "<h2>Customer Care, {0:dddd d MMMM yyyy}</h2>", localDate);
            sb.AppendFormat(culture, "<p>{0} open cases, {1} breached.</p>", open, breached);
            sb.Append("<table cellpadding=\"4\" cellspacing=\"0\" border=\"1\" style=\"border-collapse:collapse\">");
            sb.Append("<tr><th align=\"left\">Queue</th><th>Open</th><th>Unassigned</th><th>At risk</th><th>Breached</th><th>New yesterday</th><th>Resolved yesterday</th><th>Avg. resolve (h)</th></tr>");
            foreach (var s in list)
            {
                var style = s.Breached > 0 ? " style=\"background:#fde2e1\"" : string.Empty;
                sb.AppendFormat(culture, "<tr{0}><td>{1}</td><td align=\"right\">{2}</td><td align=\"right\">{3}</td><td align=\"right\">{4}</td><td align=\"right\">{5}</td><td align=\"right\">{6}</td><td align=\"right\">{7}</td><td align=\"right\">{8}</td></tr>",
                    style, Encode(s.QueueName), s.Open, s.Unassigned, s.AtRisk, s.Breached, s.CreatedToday, s.ResolvedToday,
                    s.AverageResolveHours.HasValue ? s.AverageResolveHours.Value.ToString("0.0", culture) : "-");
            }

            sb.Append("</table>");
            if (!string.IsNullOrEmpty(portalUrl))
            {
                sb.AppendFormat("<p><a href=\"{0}\">Open Customer Care</a></p>", Encode(portalUrl));
            }

            sb.Append("</body></html>");

            return new DigestMessage
            {
                Subject = string.Format(culture, "Customer Care daily summary {0:yyyy-MM-dd}: {1} open, {2} breached", localDate, open, breached),
                HtmlBody = sb.ToString()
            };
        }

        private static string Encode(string value)
        {
            return System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
