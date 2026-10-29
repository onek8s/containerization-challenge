using Hangfire;
using Tailspin.CustomerCare.Data;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Web.Services;

namespace Tailspin.CustomerCare.Web.Jobs
{
    /// <summary>After midnight: stores yesterday's queue figures for the trend charts.</summary>
    [AutomaticRetry(Attempts = 3)]
    public class StatsRollupJob
    {
        public void Run()
        {
            var yesterday = LocalTime.Now.Date.AddDays(-1);
            var repo = new ReportRepository();
            var stats = repo.QueueStats(ReferenceData.Calendar.ToUtc(yesterday));
            repo.SaveDailyStats(yesterday, stats);
        }
    }
}
