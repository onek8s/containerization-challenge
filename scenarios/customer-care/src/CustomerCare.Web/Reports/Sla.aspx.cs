using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Tailspin.CustomerCare.Data;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;
using Tailspin.CustomerCare.Web.Security;
using Tailspin.CustomerCare.Web.Services;

namespace Tailspin.CustomerCare.Web.Reports
{
    public partial class SlaReport : CarePage
    {
        protected override CareRole RequiredRole
        {
            get { return CareRole.Supervisor; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;

            Queue.DataSource = ReferenceData.Queues;
            Queue.DataBind();

            var today = LocalTime.Now.Date;
            FromDate.Text = new DateTime(today.Year, today.Month, 1).AddMonths(-1).ToString("yyyy-MM-dd");
            ToDate.Text = new DateTime(today.Year, today.Month, 1).AddDays(-1).ToString("yyyy-MM-dd");
        }

        protected void ShowButton_Click(object sender, EventArgs e)
        {
            DateTime from, to;
            int? queueId;
            if (!ReadFilter(out from, out to, out queueId)) return;

            var policy = ReferenceData.SlaPolicy;
            var now = DateTime.UtcNow;
            var rows = new ReportRepository().Sla(ToUtc(from), ToUtc(to.AddDays(1)), queueId);

            Summary.DataSource = rows
                .GroupBy(r => new { r.QueueName, r.Priority })
                .OrderBy(g => g.Key.QueueName).ThenByDescending(g => g.Key.Priority)
                .Select(g => new
                {
                    Queue = g.Key.QueueName,
                    Priority = g.Key.Priority,
                    Cases = g.Count(),
                    ResponseMet = g.Count(r => r.FirstResponseUtc.HasValue && r.FirstResponseUtc <= r.ResponseDueUtc) / (double)g.Count(),
                    ResolutionMet = g.Count(r => policy.Evaluate(ToCase(r), now) == SlaState.Met) / (double)g.Count(),
                    Open = g.Count(r => r.Status != CaseStatus.Resolved && r.Status != CaseStatus.Closed)
                })
                .ToList();
            Summary.DataBind();
        }

        protected void DownloadButton_Click(object sender, EventArgs e)
        {
            DateTime from, to;
            int? queueId;
            if (!ReadFilter(out from, out to, out queueId)) return;

            var path = SlaReportFile.Create(from, to, queueId, () => new ReportRepository().Sla(ToUtc(from), ToUtc(to.AddDays(1)), queueId));

            Response.Clear();
            Response.ContentType = "text/csv";
            Response.AddHeader("Content-Disposition", "attachment; filename=\"" + Path.GetFileName(path) + "\"");
            Response.TransmitFile(path);
            Response.Flush();
            Context.ApplicationInstance.CompleteRequest();
        }

        private bool ReadFilter(out DateTime from, out DateTime to, out int? queueId)
        {
            queueId = string.IsNullOrEmpty(Queue.SelectedValue) ? (int?)null : int.Parse(Queue.SelectedValue);
            if (!DateTime.TryParseExact(FromDate.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out from) ||
                !DateTime.TryParseExact(ToDate.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out to) || to < from)
            {
                ShowMessage("Enter a valid date range.", true);
                to = from;
                return false;
            }

            if ((to - from).TotalDays > 366)
            {
                ShowMessage("The period can be at most one year.", true);
                return false;
            }

            return true;
        }

        private static DateTime ToUtc(DateTime localDate)
        {
            return ReferenceData.Calendar.ToUtc(localDate);
        }

        private static Case ToCase(SlaReportRow r)
        {
            return new Case { Status = r.Status, CreatedUtc = r.CreatedUtc, ResponseDueUtc = r.ResponseDueUtc, ResolveDueUtc = r.ResolveDueUtc, FirstResponseUtc = r.FirstResponseUtc, ResolvedUtc = r.ResolvedUtc };
        }
    }
}
