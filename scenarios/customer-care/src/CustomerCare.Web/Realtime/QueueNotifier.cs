using System;
using System.Diagnostics;
using System.Web;
using Microsoft.AspNet.SignalR;
using Tailspin.CustomerCare.Data;
using Tailspin.CustomerCare.Data.Services;
using Tailspin.CustomerCare.Domain;

namespace Tailspin.CustomerCare.Web.Realtime
{
    public class CaseChangeMessage
    {
        public int Id { get; set; }
        public string CaseNumber { get; set; }
        public string Subject { get; set; }
        public string Queue { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; }
        public string AssignedTo { get; set; }
        public string ResolveDue { get; set; }
        public string Sla { get; set; }
        public string Change { get; set; }
        public bool IsOpen { get; set; }
    }

    /// <summary>Pushes case changes to connected browsers.</summary>
    public static class QueueNotifier
    {
        private const string DashboardCacheKey = "care:dashboard";

        public static void OnCaseChanged(object sender, CaseChangedEventArgs e)
        {
            Publish(e.Case, e.Change);
        }

        public static void Publish(Case c, string change)
        {
            try
            {
                var queue = c.Queue ?? ReferenceData.QueueById(c.QueueId);
                var message = ToMessage(c, queue, change);
                var hub = GlobalHost.ConnectionManager.GetHubContext<QueueHub>();

                if (queue != null) hub.Clients.Group("queue:" + queue.Code).caseChanged(message);
                hub.Clients.Group("all").caseChanged(message);
                hub.Clients.Group("case:" + c.Id).caseUpdated(message);

                // Dashboard numbers are cached for a minute; drop them so the next refresh is current.
                HttpRuntime.Cache.Remove(DashboardCacheKey);
                hub.Clients.Group("all").statsChanged();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Could not publish change of case {0}: {1}", c != null ? c.CaseNumber : "?", ex.Message);
            }
        }

        public static string DashboardKey
        {
            get { return DashboardCacheKey; }
        }

        public static CaseChangeMessage ToMessage(Case c, Queue queue, string change)
        {
            var cal = ReferenceData.Calendar;
            return new CaseChangeMessage
            {
                Id = c.Id,
                CaseNumber = c.CaseNumber,
                Subject = c.Subject,
                Queue = queue != null ? queue.Code : null,
                Status = CaseWorkflow.DisplayName(c.Status),
                Priority = c.Priority.ToString(),
                AssignedTo = c.AssignedTo,
                ResolveDue = cal.ToLocal(c.ResolveDueUtc).ToString("dd.MM. HH:mm"),
                Sla = ReferenceData.SlaPolicy.Evaluate(c, DateTime.UtcNow).ToString(),
                Change = change,
                IsOpen = c.IsOpen
            };
        }
    }
}
