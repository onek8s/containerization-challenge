using System;
using System.Linq;
using System.Net.Mail;
using System.Text;
using Lucerne.ProjectSites.Data;
using Lucerne.ProjectSites.Sync;

namespace Lucerne.ProjectSites.SyncService.Jobs
{
    /// <summary>Monday status mail to subscribers of the dashboards (weekly, or the first Monday of a month).</summary>
    public class StatusDigestJob : ISyncJob
    {
        private readonly Func<ProjectSitesContext> _db;
        private readonly ServiceSettings _settings;

        public StatusDigestJob(Func<ProjectSitesContext> db, ServiceSettings settings)
        {
            _db = db;
            _settings = settings;
        }

        public string Name
        {
            get { return "StatusDigest"; }
        }

        public SyncRun Run(DateTime nowUtc)
        {
            var run = new SyncRun();
            var today = nowUtc.Date;
            if (today.DayOfWeek != DayOfWeek.Monday)
            {
                run.Message = "not Monday";
                return run;
            }

            using (var db = _db())
            using (var smtp = new SmtpClient(_settings.SmtpHost))
            {
                var due = db.Subscriptions.Where(s => s.Email != null && (s.LastSentUtc == null || s.LastSentUtc < today)).ToList()
                    .Where(s => s.Frequency != "Monthly" || today.Day <= 7)
                    .ToList();

                foreach (var group in due.GroupBy(s => s.Email))
                {
                    var body = new StringBuilder("<p>Status of your projects:</p><ul>");
                    foreach (var s in group)
                    {
                        var number = s.ProjectNumber;
                        var latest = db.KpiSnapshots.Where(k => k.ProjectNumber == number).OrderByDescending(k => k.SnapshotDate).FirstOrDefault();
                        body.AppendFormat("<li><a href=\"{0}/projects/{1}\">{1}</a>: {2}</li>", _settings.DashboardsUrl, number, latest == null ? "no data" : latest.Overall);
                        s.LastSentUtc = nowUtc;
                    }

                    body.Append("</ul>");
                    smtp.Send(new MailMessage(_settings.MailFrom, group.Key, "Project status " + today.ToString("yyyy-MM-dd"), body.ToString()) { IsBodyHtml = true });
                    run.Created++;
                }

                db.SaveChanges();
            }

            return run;
        }
    }
}
