using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Mail;
using System.Text;
using Contoso.Purchasing.Core;
using Contoso.Purchasing.Data;

namespace Contoso.Purchasing.Jobs
{
    /// <summary>
    /// Sends one e-mail per approver (or approver mailbox) listing the requests that have waited too long.
    /// </summary>
    internal class ReminderJob
    {
        private readonly Database _db;
        private readonly ApprovalPolicy _approvals = new ApprovalPolicy();
        private readonly ReminderPolicy _reminders;
        private readonly string _portalUrl;
        private readonly bool _mailEnabled;

        public ReminderJob(Database db)
        {
            _db = db;
            _reminders = new ReminderPolicy(
                int.Parse(ConfigurationManager.AppSettings["Reminders.FirstAfterBusinessDays"]),
                int.Parse(ConfigurationManager.AppSettings["Reminders.RepeatAfterBusinessDays"]));
            _portalUrl = ConfigurationManager.AppSettings["PortalUrl"].TrimEnd('/');
            _mailEnabled = bool.Parse(ConfigurationManager.AppSettings["Mail.Enabled"]);
        }

        public int Run(DateTime now)
        {
            var requests = new RequestRepository(_db);
            var costCenters = new CatalogRepository(_db).CostCenters().ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

            var due = requests.ByStatus(RequestStatus.InApproval).Where(r => _reminders.IsDue(r, now)).ToList();
            JobLog.Info(due.Count + " request(s) need a reminder.");

            var byRecipient = new Dictionary<string, List<PurchaseRequest>>(StringComparer.OrdinalIgnoreCase);
            foreach (var request in due)
            {
                var recipient = Recipient(request, costCenters);
                if (string.IsNullOrEmpty(recipient))
                {
                    JobLog.Error("No reminder recipient for " + request.Number + " (cost center " + request.CostCenterCode + ").");
                    continue;
                }

                List<PurchaseRequest> list;
                if (!byRecipient.TryGetValue(recipient, out list))
                {
                    byRecipient[recipient] = list = new List<PurchaseRequest>();
                }
                list.Add(request);
            }

            var failures = 0;
            using (var smtp = new SmtpClient())
            {
                foreach (var pair in byRecipient)
                {
                    try
                    {
                        Send(smtp, pair.Key, pair.Value, now);
                        foreach (var request in pair.Value)
                        {
                            requests.MarkReminded(request.Id, now);
                        }
                        JobLog.Info("Reminder sent to " + pair.Key + ": " + string.Join(", ", pair.Value.Select(r => r.Number)));
                    }
                    catch (SmtpException ex)
                    {
                        failures++;
                        JobLog.Error("Could not send reminder to " + pair.Key + ": " + ex.Message);
                    }
                }
            }

            return failures;
        }

        private string Recipient(PurchaseRequest request, IDictionary<string, CostCenter> costCenters)
        {
            switch (_approvals.NextStep(request))
            {
                case ApprovalStep.Manager:
                    CostCenter costCenter;
                    return costCenters.TryGetValue(request.CostCenterCode, out costCenter) ? costCenter.ManagerEmail : null;
                case ApprovalStep.Finance:
                    return ConfigurationManager.AppSettings["Mail.FinanceMailbox"];
                case ApprovalStep.Director:
                    return ConfigurationManager.AppSettings["Mail.DirectorsMailbox"];
                default:
                    return null;
            }
        }

        private void Send(SmtpClient smtp, string recipient, IList<PurchaseRequest> requests, DateTime now)
        {
            var body = new StringBuilder();
            body.Append("<p>The following purchase requests are waiting for your approval:</p>");
            body.Append("<table border=\"1\" cellpadding=\"4\" cellspacing=\"0\" style=\"border-collapse:collapse;font-family:Segoe UI,Arial;font-size:13px\">");
            body.Append("<tr><th>Request</th><th>Title</th><th>Requester</th><th>Net</th><th>Waiting (business days)</th></tr>");
            foreach (var r in requests)
            {
                body.AppendFormat("<tr><td><a href=\"{0}/requests/details/{1}\">{2}</a></td><td>{3}</td><td>{4}</td><td align=\"right\">{5:N2} {6}</td><td align=\"right\">{7}</td></tr>",
                    _portalUrl, r.Id, r.Number, Html(r.Title), Html(r.RequesterName), r.NetTotal, r.Currency,
                    BusinessCalendar.BusinessDaysBetween(r.LastActivityAt.Value, now));
            }
            body.Append("</table><p>Open <a href=\"" + _portalUrl + "/approvals\">your approval list</a> to decide.</p>");
            body.Append("<p style=\"color:#888\">This message was sent automatically by Purchase Requests.</p>");

            using (var message = new MailMessage())
            {
                message.From = new MailAddress(ConfigurationManager.AppSettings["Mail.From"]);
                message.To.Add(recipient);
                message.Subject = requests.Count == 1
                    ? "Reminder: " + requests[0].Number + " is waiting for your approval"
                    : "Reminder: " + requests.Count + " purchase requests are waiting for your approval";
                message.IsBodyHtml = true;
                message.Body = body.ToString();

                if (_mailEnabled)
                {
                    smtp.Send(message);
                }
                else
                {
                    JobLog.Info("Mail disabled, not sending: " + message.Subject + " -> " + recipient);
                }
            }
        }

        private static string Html(string value)
        {
            return System.Net.WebUtility.HtmlEncode(value ?? "");
        }
    }
}
