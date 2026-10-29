using System;
using System.Collections.Generic;
using System.Diagnostics;
using Lucerne.ProjectSites.Core;

namespace Lucerne.ProjectSites.Sync
{
    public interface ISyncJob
    {
        string Name { get; }
        SyncRun Run(DateTime nowUtc);
    }

    public class SyncOptions
    {
        public string HubUrl { get; set; }

        /// <summary>Weeks of approved timesheets re-read on every run (late approvals and corrections).</summary>
        public int TimesheetWeeks { get; set; } = 12;

        /// <summary>Overlap subtracted from watermarks to cover clock differences with the source systems.</summary>
        public TimeSpan WatermarkOverlap { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>Account recorded as requester of automatic site requests.</summary>
        public string ServiceLogin { get; set; } = "i:0#.w|corp\\svc-ps-sync";

        public bool CreateSiteRequests { get; set; } = true;
    }

    /// <summary>Runs jobs one after the other and records each run; a failed job does not stop the others.</summary>
    public class SyncEngine
    {
        private readonly IEnumerable<ISyncJob> _jobs;
        private readonly ISyncStateStore _state;
        private readonly IActivityLog _log;

        public SyncEngine(IEnumerable<ISyncJob> jobs, ISyncStateStore state, IActivityLog log)
        {
            _jobs = jobs;
            _state = state;
            _log = log ?? NullActivityLog.Instance;
            Clock = () => DateTime.UtcNow;
        }

        /// <summary>The time passed to the jobs (replaced in tests and lab runs).</summary>
        public Func<DateTime> Clock { get; set; }

        public IList<SyncRun> RunAll(Func<string, bool> filter = null)
        {
            var runs = new List<SyncRun>();
            foreach (var job in _jobs)
            {
                if (filter != null && !filter(job.Name))
                {
                    continue;
                }

                runs.Add(RunOne(job));
            }

            return runs;
        }

        public SyncRun RunOne(ISyncJob job)
        {
            var started = Clock();
            var sw = Stopwatch.StartNew();
            SyncRun run;
            try
            {
                _log.Info("Starting " + job.Name);
                run = job.Run(started);
                run.Succeeded = run.Failed == 0;
            }
            catch (Exception ex)
            {
                _log.Error(job.Name + " failed", ex);
                run = new SyncRun { Succeeded = false, Message = ex.GetBaseException().Message };
            }

            run.Job = job.Name;
            run.StartedUtc = started;
            run.FinishedUtc = started + sw.Elapsed;
            _state.RecordRun(run);
            _log.Info(string.Format("{0} finished in {1:0.0}s: {2} created, {3} updated, {4} failed. {5}", job.Name, sw.Elapsed.TotalSeconds, run.Created, run.Updated, run.Failed, run.Message));
            return run;
        }
    }
}
