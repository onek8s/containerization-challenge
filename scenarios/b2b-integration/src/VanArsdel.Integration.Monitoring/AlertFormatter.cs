using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;

namespace VanArsdel.Integration.Monitoring
{
    public class AlertMail
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string HtmlBody { get; set; }
        public string TextBody { get; set; }
    }

    /// <summary>One mail per notify group and cycle, newest critical first.</summary>
    public static class AlertFormatter
    {
        public static IList<AlertMail> Format(AlertDecision decision, IDictionary<string, string> groupAddresses, string environment, string opsPortalUrl)
        {
            var mails = new List<AlertMail>();
            var groups = decision.Notify.Select(a => a.NotifyGroup ?? "integration").Concat(decision.Cleared.Select(c => c.NotifyGroup ?? "integration")).Distinct();
            foreach (var group in groups)
            {
                var alerts = decision.Notify.Where(a => (a.NotifyGroup ?? "integration") == group).OrderByDescending(a => a.Severity).ThenBy(a => a.Key).ToList();
                var cleared = decision.Cleared.Where(c => (c.NotifyGroup ?? "integration") == group).ToList();
                string to;
                if (!groupAddresses.TryGetValue(group, out to))
                {
                    to = groupAddresses.ContainsKey("integration") ? groupAddresses["integration"] : null;
                }

                var top = alerts.FirstOrDefault();
                var subject = "[B2B " + environment + "] " + (top == null
                    ? cleared.Count + " alert(s) cleared"
                    : top.Severity + ": " + top.Subject + (alerts.Count > 1 ? " (+" + (alerts.Count - 1) + " more)" : string.Empty));

                var text = new StringBuilder();
                var html = new StringBuilder();
                html.Append("<html><body style=\"font-family:Segoe UI,Arial,sans-serif;font-size:10pt\">");
                if (alerts.Count > 0)
                {
                    html.Append("<table border=\"1\" cellpadding=\"4\" cellspacing=\"0\" style=\"border-collapse:collapse\"><tr><th>Severity</th><th>SLA</th><th>Partner</th><th>Alert</th><th>Detail</th></tr>");
                    foreach (var a in alerts)
                    {
                        text.AppendLine(a.Severity + " " + a.SlaCode + " " + (a.PartnerId ?? "-") + ": " + a.Subject);
                        text.AppendLine("    " + a.Detail);
                        html.Append("<tr><td>").Append(a.Severity).Append("</td><td>").Append(E(a.SlaCode)).Append("</td><td>").Append(E(a.PartnerId ?? "-"))
                            .Append("</td><td>").Append(E(a.Subject)).Append("</td><td>").Append(E(a.Detail)).Append("</td></tr>");
                    }

                    html.Append("</table>");
                }

                if (cleared.Count > 0)
                {
                    text.AppendLine("Cleared:");
                    html.Append("<p>Cleared:</p><ul>");
                    foreach (var c in cleared)
                    {
                        text.AppendLine("    " + c.Subject + " (open since " + c.FirstRaisedUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC)");
                        html.Append("<li>").Append(E(c.Subject)).Append("</li>");
                    }

                    html.Append("</ul>");
                }

                if (!string.IsNullOrEmpty(opsPortalUrl))
                {
                    text.AppendLine("Operations portal: " + opsPortalUrl);
                    html.Append("<p><a href=\"").Append(E(opsPortalUrl)).Append("\">Operations portal</a></p>");
                }

                html.Append("</body></html>");
                mails.Add(new AlertMail { To = to, Subject = subject, TextBody = text.ToString(), HtmlBody = html.ToString() });
            }

            return mails;
        }

        private static string E(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
