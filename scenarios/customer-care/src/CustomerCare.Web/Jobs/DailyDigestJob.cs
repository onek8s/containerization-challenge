using System.Linq;
using Hangfire;
using Tailspin.CustomerCare.Data;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;
using Tailspin.CustomerCare.Web.Security;
using Tailspin.CustomerCare.Web.Services;

namespace Tailspin.CustomerCare.Web.Jobs
{
    /// <summary>Weekday mornings: queue summary e-mail to all supervisors.</summary>
    [AutomaticRetry(Attempts = 3)]
    public class DailyDigestJob
    {
        public void Run()
        {
            var today = LocalTime.Now.Date;
            var yesterdayStartUtc = ReferenceData.Calendar.ToUtc(today.AddDays(-1));
            var stats = new ReportRepository().QueueStats(yesterdayStartUtc);
            var message = DigestBuilder.Build(today, stats, CareSettings.PortalUrl);

            var to = UserDirectory.Members(CareRole.Supervisor).Select(m => m.Email).ToList();
            if (!string.IsNullOrEmpty(CareSettings.ManagerEmail)) to.Add(CareSettings.ManagerEmail);

            Mailer.Send(to, message.Subject, message.HtmlBody);
        }
    }
}
