using System;
using System.Diagnostics;
using Hangfire;
using Tailspin.CustomerCare.Web.Services;

namespace Tailspin.CustomerCare.Web.Jobs
{
    /// <summary>Hourly: removes old report files and uploads of new cases that were never saved.</summary>
    [AutomaticRetry(Attempts = 0)]
    public class WorkFolderCleanupJob
    {
        public void Run()
        {
            var reports = WorkFolders.DeleteOlderThan(WorkFolders.Reports, TimeSpan.FromHours(1));
            var uploads = WorkFolders.DeleteOlderThan(WorkFolders.Uploads, TimeSpan.FromHours(12));
            if (reports + uploads > 0) Trace.TraceInformation("Work folder clean-up removed {0} report and {1} upload file(s).", reports, uploads);
        }
    }
}
