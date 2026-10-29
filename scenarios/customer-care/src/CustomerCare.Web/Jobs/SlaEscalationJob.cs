using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Hangfire;
using Tailspin.CustomerCare.Data;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;
using Tailspin.CustomerCare.Web.Realtime;
using Tailspin.CustomerCare.Web.Security;
using Tailspin.CustomerCare.Web.Services;

namespace Tailspin.CustomerCare.Web.Jobs
{
    /// <summary>
    /// Every five minutes: recalculates the escalation level of all open cases and notifies the assignee,
    /// the queue mailbox or the contact centre manager when a case moves to a higher level.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(10 * 60)]
    public class SlaEscalationJob
    {
        private readonly CaseRepository _cases = new CaseRepository();

        public void Run()
        {
            var now = DateTime.UtcNow;
            var policy = ReferenceData.SlaPolicy;
            var changed = 0;

            foreach (var c in _cases.ListOpen())
            {
                var level = policy.EscalationLevel(c, now);
                if (level == c.EscalationLevel) continue;

                var recipients = level > c.EscalationLevel ? Recipients(c, level) : new List<string>();
                Db.Execute("care.usp_Case_SetEscalation", Db.P("@CaseId", c.Id), Db.P("@Level", level), Db.P("@NotifiedTo", string.Join(";", recipients)));
                c.EscalationLevel = level;
                changed++;

                if (recipients.Count > 0)
                {
                    BackgroundJob.Enqueue<SlaEscalationJob>(j => j.SendNotification(c.Id, level, recipients.ToArray()));
                }

                QueueNotifier.Publish(c, "sla");
            }

            if (changed > 0) Trace.TraceInformation("SLA escalation: {0} case(s) changed level.", changed);
        }

        [Queue("mail")]
        [AutomaticRetry(Attempts = 5)]
        public void SendNotification(int caseId, int level, string[] recipients)
        {
            var c = _cases.Get(caseId);
            if (c == null || !c.IsOpen) return;

            var label = level == 1 ? "at risk" : level == 2 ? "breached" : "seriously breached";
            var url = CareSettings.PortalUrl.TrimEnd('/') + "/cases/" + c.Id;
            var body = string.Format(
                "<p>Case <a href=\"{0}\">{1}</a> is <b>{2}</b>.</p><p>{3}<br/>Customer: {4}<br/>Queue: {5}<br/>Assigned to: {6}<br/>Resolve due: {7}</p>",
                url, c.CaseNumber, label, WebUtility.HtmlEncode(c.Subject), WebUtility.HtmlEncode(c.Customer.Name), WebUtility.HtmlEncode(c.Queue.Name),
                WebUtility.HtmlEncode(c.AssignedTo ?? "nobody"), LocalTime.Format(c.ResolveDueUtc));

            Mailer.Send(recipients, string.Format("[{0}] {1} {2}", c.CaseNumber, c.Subject, label), body);
        }

        private static List<string> Recipients(Case c, int level)
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(c.AssignedTo))
            {
                var assignee = UserDirectory.Find(c.AssignedTo);
                if (assignee != null && !string.IsNullOrEmpty(assignee.Email)) list.Add(assignee.Email);
            }

            if (level >= 2 && c.Queue != null && !string.IsNullOrEmpty(c.Queue.Email)) list.Add(c.Queue.Email);
            if (level >= 3 && !string.IsNullOrEmpty(CareSettings.ManagerEmail)) list.Add(CareSettings.ManagerEmail);
            if (level >= 2 && c.Customer != null && c.Customer.Segment == "LCO")
            {
                list.AddRange(UserDirectory.Members(CareRole.Supervisor).Select(m => m.Email).Where(e => !string.IsNullOrEmpty(e)));
            }

            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
