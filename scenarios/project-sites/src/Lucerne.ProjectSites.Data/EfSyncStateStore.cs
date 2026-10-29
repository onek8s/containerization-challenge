using System;
using System.Linq;
using Lucerne.ProjectSites.Data.Entities;
using Lucerne.ProjectSites.Sync;

namespace Lucerne.ProjectSites.Data
{
    public class EfSyncStateStore : ISyncStateStore
    {
        private readonly Func<ProjectSitesContext> _factory;

        public EfSyncStateStore(Func<ProjectSitesContext> factory)
        {
            _factory = factory;
        }

        public DateTime? GetWatermark(string name)
        {
            using (var db = _factory())
            {
                var w = db.Watermarks.Find(name);
                return w == null ? (DateTime?)null : DateTime.SpecifyKind(w.ValueUtc, DateTimeKind.Utc);
            }
        }

        public void SetWatermark(string name, DateTime valueUtc)
        {
            using (var db = _factory())
            {
                var w = db.Watermarks.Find(name);
                if (w == null)
                {
                    w = new SyncWatermark { Name = name };
                    db.Watermarks.Add(w);
                }

                w.ValueUtc = valueUtc;
                w.UpdatedUtc = DateTime.UtcNow;
                db.SaveChanges();
            }
        }

        public void RecordRun(SyncRun run)
        {
            using (var db = _factory())
            {
                db.Runs.Add(new SyncRunRecord
                {
                    Job = run.Job,
                    StartedUtc = run.StartedUtc,
                    FinishedUtc = run.FinishedUtc,
                    Succeeded = run.Succeeded,
                    Created = run.Created,
                    Updated = run.Updated,
                    Failed = run.Failed,
                    Message = run.Message == null ? null : run.Message.Substring(0, Math.Min(2000, run.Message.Length)),
                    MachineName = Environment.MachineName
                });
                db.SaveChanges();
            }
        }
    }

    public class KpiRepository
    {
        private readonly Func<ProjectSitesContext> _factory;

        public KpiRepository(Func<ProjectSitesContext> factory)
        {
            _factory = factory;
        }

        /// <summary>One snapshot per project and day; a later run on the same day replaces it.</summary>
        public void Save(KpiSnapshot snapshot)
        {
            using (var db = _factory())
            {
                var existing = db.KpiSnapshots.FirstOrDefault(k => k.ProjectNumber == snapshot.ProjectNumber && k.SnapshotDate == snapshot.SnapshotDate);
                if (existing != null)
                {
                    snapshot.Id = existing.Id;
                    db.Entry(existing).CurrentValues.SetValues(snapshot);
                }
                else
                {
                    db.KpiSnapshots.Add(snapshot);
                }

                db.SaveChanges();
            }
        }

        public KpiSnapshot[] History(string projectNumber, DateTime fromDate)
        {
            using (var db = _factory())
            {
                return db.KpiSnapshots.AsNoTracking()
                    .Where(k => k.ProjectNumber == projectNumber && k.SnapshotDate >= fromDate)
                    .OrderBy(k => k.SnapshotDate)
                    .ToArray();
            }
        }

        public ProjectSubscription[] SubscriptionsFor(string userLogin)
        {
            using (var db = _factory())
            {
                return db.Subscriptions.AsNoTracking().Where(s => s.UserLogin == userLogin).ToArray();
            }
        }

        public void Subscribe(string userLogin, string email, string projectNumber, string frequency)
        {
            using (var db = _factory())
            {
                if (!db.Subscriptions.Any(s => s.UserLogin == userLogin && s.ProjectNumber == projectNumber))
                {
                    db.Subscriptions.Add(new ProjectSubscription
                    {
                        UserLogin = userLogin,
                        Email = email,
                        ProjectNumber = projectNumber,
                        Frequency = frequency,
                        CreatedUtc = DateTime.UtcNow
                    });
                    db.SaveChanges();
                }
            }
        }
    }
}
