using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Woodgrove.Statements.Core.Model;

namespace Woodgrove.Statements.Data
{
    public class SubscriptionRepository
    {
        const string Columns = @"SubscriptionId, Name, ReportCode, Schedule, Parameters, OutputFormat, DeliveryMethod, Recipients, SharePath,
                                 FileNameTemplate, EmailSubject, Owner, Enabled, NextRunAt, LastRunAt, LastStatus, ClaimedBy, ClaimedUntil,
                                 ModifiedBy, ModifiedAt";
        readonly Db _db;

        public SubscriptionRepository(Db db) { _db = db; }

        public IList<Subscription> List()
        {
            using (var c = _db.Open())
                return c.Query<Subscription>("SELECT " + Columns + " FROM stm.Subscription ORDER BY Name").ToList();
        }

        public Subscription Get(int subscriptionId)
        {
            using (var c = _db.Open())
                return c.QuerySingleOrDefault<Subscription>("SELECT " + Columns + " FROM stm.Subscription WHERE SubscriptionId = @subscriptionId", new { subscriptionId });
        }

        public int Save(Subscription s)
        {
            using (var c = _db.Open())
            {
                if (s.SubscriptionId == 0)
                    return c.ExecuteScalar<int>(@"
INSERT stm.Subscription (Name, ReportCode, Schedule, Parameters, OutputFormat, DeliveryMethod, Recipients, SharePath, FileNameTemplate,
                         EmailSubject, Owner, Enabled, NextRunAt, ModifiedBy)
VALUES (@Name, @ReportCode, @Schedule, @Parameters, @OutputFormat, @DeliveryMethod, @Recipients, @SharePath, @FileNameTemplate,
        @EmailSubject, @Owner, @Enabled, @NextRunAt, @ModifiedBy);
SELECT CAST(SCOPE_IDENTITY() AS int);", s);

                c.Execute(@"
UPDATE stm.Subscription
   SET Name = @Name, ReportCode = @ReportCode, Schedule = @Schedule, Parameters = @Parameters, OutputFormat = @OutputFormat,
       DeliveryMethod = @DeliveryMethod, Recipients = @Recipients, SharePath = @SharePath, FileNameTemplate = @FileNameTemplate,
       EmailSubject = @EmailSubject, Owner = @Owner, Enabled = @Enabled, NextRunAt = @NextRunAt, ModifiedBy = @ModifiedBy, ModifiedAt = SYSDATETIME()
 WHERE SubscriptionId = @SubscriptionId", s);
                return s.SubscriptionId;
            }
        }

        public void Delete(int subscriptionId)
        {
            using (var c = _db.Open())
                c.Execute("UPDATE stm.Subscription SET Enabled = 0, NextRunAt = NULL WHERE SubscriptionId = @subscriptionId", new { subscriptionId });
        }

        /// <summary>Asks the distribution service to run the subscription at its next poll.</summary>
        public void RunNow(int subscriptionId, string requestedBy)
        {
            using (var c = _db.Open())
                c.Execute(@"UPDATE stm.Subscription SET NextRunAt = DATEADD(SECOND, -1, SYSDATETIME()), ModifiedBy = @requestedBy, ModifiedAt = SYSDATETIME()
                             WHERE SubscriptionId = @subscriptionId AND Enabled = 1", new { subscriptionId, requestedBy });
        }

        public IList<Subscription> WithoutNextRun()
        {
            using (var c = _db.Open())
                return c.Query<Subscription>("SELECT " + Columns + " FROM stm.Subscription WHERE Enabled = 1 AND NextRunAt IS NULL").ToList();
        }

        public void SetNextRun(int subscriptionId, DateTime? nextRunAt)
        {
            using (var c = _db.Open())
                c.Execute("UPDATE stm.Subscription SET NextRunAt = @nextRunAt WHERE SubscriptionId = @subscriptionId", new { subscriptionId, nextRunAt });
        }

        public IList<Subscription> ClaimDue(string host, DateTime now, int leaseMinutes, int maxCount)
        {
            using (var c = _db.Open())
            {
                var ids = c.Query<int>("stm.ClaimDueSubscriptions", new { Host = host, Now = now, LeaseMinutes = leaseMinutes, MaxCount = maxCount },
                                       commandType: System.Data.CommandType.StoredProcedure).ToList();
                if (ids.Count == 0) return new List<Subscription>();
                return c.Query<Subscription>("SELECT " + Columns + " FROM stm.Subscription WHERE SubscriptionId IN @ids ORDER BY NextRunAt", new { ids }).ToList();
            }
        }

        public void Complete(int subscriptionId, string host, DateTime ranAt, string status, DateTime? nextRunAt)
        {
            using (var c = _db.Open())
                c.Execute(@"UPDATE stm.Subscription
                               SET LastRunAt = @ranAt, LastStatus = @status, NextRunAt = @nextRunAt, ClaimedBy = NULL, ClaimedUntil = NULL
                             WHERE SubscriptionId = @subscriptionId AND (ClaimedBy = @host OR ClaimedBy IS NULL)",
                          new { subscriptionId, host, ranAt, status, nextRunAt });
        }

        public int StartRun(int subscriptionId, DateTime? scheduledFor, string runBy, string host)
        {
            using (var c = _db.Open())
                return c.ExecuteScalar<int>(@"
INSERT stm.SubscriptionRun (SubscriptionId, ScheduledFor, StartedAt, Status, RunBy, Host)
VALUES (@subscriptionId, @scheduledFor, SYSDATETIME(), 'Running', @runBy, @host);
SELECT CAST(SCOPE_IDENTITY() AS int);", new { subscriptionId, scheduledFor, runBy, host });
        }

        public void FinishRun(int runId, string status, int documents, int failures, string message)
        {
            using (var c = _db.Open())
                c.Execute(@"UPDATE stm.SubscriptionRun SET FinishedAt = SYSDATETIME(), Status = @status, Documents = @documents, Failures = @failures,
                                   Message = LEFT(@message, 2000) WHERE RunId = @runId", new { runId, status, documents, failures, message });
        }

        public IList<SubscriptionRun> Runs(int? subscriptionId, int max = 50)
        {
            using (var c = _db.Open())
                return c.Query<SubscriptionRun>(@"
SELECT TOP (@max) r.RunId, r.SubscriptionId, s.Name AS SubscriptionName, r.ScheduledFor, r.StartedAt, r.FinishedAt, r.Status,
       r.Documents, r.Failures, r.Message, r.RunBy, r.Host
  FROM stm.SubscriptionRun r JOIN stm.Subscription s ON s.SubscriptionId = r.SubscriptionId
 WHERE (@subscriptionId IS NULL OR r.SubscriptionId = @subscriptionId)
 ORDER BY r.StartedAt DESC, r.RunId DESC", new { max, subscriptionId }).ToList();
        }
    }
}
