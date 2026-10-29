using System;
using System.Data;
using Northwind.Inspections.Core;

namespace Northwind.Inspections.Infrastructure.Data
{
    /// <summary>
    /// Report requests are queued in dbo.ReportQueue. Several job runs may overlap (the scheduled task can be
    /// started by hand, or run on two servers), so each run claims one row at a time with UPDLOCK/READPAST.
    /// Status: 0 pending, 1 in progress, 2 sent, 3 abandoned.
    /// </summary>
    public sealed class ReportQueue
    {
        private readonly Db db;

        public ReportQueue(Db db)
        {
            this.db = db;
        }

        public long Enqueue(int inspectionId, string recipient, string requestedBy)
        {
            var id = db.Scalar(@"INSERT INTO dbo.ReportQueue (InspectionId, Recipient, RequestedBy, RequestedUtc, Status, Attempts, NotBeforeUtc)
OUTPUT inserted.Id VALUES (@i, @r, @by, SYSUTCDATETIME(), 0, 0, SYSUTCDATETIME())",
                Db.P("@i", inspectionId), Db.P("@r", SqlDbType.NVarChar, 256, recipient), Db.P("@by", SqlDbType.NVarChar, 128, requestedBy));
            return Convert.ToInt64(id);
        }

        public int Depth()
        {
            return Convert.ToInt32(db.Scalar("SELECT COUNT(*) FROM dbo.ReportQueue WHERE Status IN (0, 1)"));
        }

        /// <summary>Claims the next due request, including ones whose lock expired, or returns null.</summary>
        public ReportRequest ClaimNext(string worker)
        {
            const string sql = @"
WITH next AS (
    SELECT TOP (1) *
    FROM dbo.ReportQueue WITH (UPDLOCK, READPAST, ROWLOCK)
    WHERE (Status = 0 AND NotBeforeUtc <= SYSUTCDATETIME())
       OR (Status = 1 AND LockedUtc < DATEADD(MINUTE, -@timeout, SYSUTCDATETIME()))
    ORDER BY NotBeforeUtc, Id)
UPDATE next SET Status = 1, LockedBy = @worker, LockedUtc = SYSUTCDATETIME(), Attempts = Attempts + 1
OUTPUT inserted.Id, inserted.InspectionId, inserted.Recipient, inserted.RequestedBy, inserted.RequestedUtc, inserted.Attempts, inserted.LastError;";

            return db.Single(sql, r => new ReportRequest
            {
                Id = (long)r["Id"],
                InspectionId = (int)r["InspectionId"],
                Recipient = (string)r["Recipient"],
                RequestedBy = (string)r["RequestedBy"],
                RequestedUtc = (DateTime)r["RequestedUtc"],
                Attempts = (int)r["Attempts"],
                LastError = Db.Str(r, "LastError")
            },
            Db.P("@worker", SqlDbType.NVarChar, 128, worker),
            Db.P("@timeout", (int)ReportSchedule.LockTimeout.TotalMinutes));
        }

        public void Complete(long id)
        {
            db.Execute("UPDATE dbo.ReportQueue SET Status = 2, CompletedUtc = SYSUTCDATETIME(), LockedBy = NULL, LastError = NULL WHERE Id = @id", Db.P("@id", id));
        }

        /// <summary>Returns true when the request will be retried.</summary>
        public bool Fail(ReportRequest request, string error)
        {
            var next = ReportSchedule.NextAttemptUtc(request.Attempts, DateTime.UtcNow);
            db.Execute(@"UPDATE dbo.ReportQueue SET Status = @s, NotBeforeUtc = ISNULL(@next, NotBeforeUtc), LockedBy = NULL,
    LastError = @e, CompletedUtc = CASE WHEN @s = 3 THEN SYSUTCDATETIME() ELSE NULL END WHERE Id = @id",
                Db.P("@id", request.Id), Db.P("@s", next.HasValue ? 0 : 3), Db.P("@next", SqlDbType.DateTime2, 0, next),
                Db.P("@e", SqlDbType.NVarChar, 2000, error != null && error.Length > 2000 ? error.Substring(0, 2000) : error));
            return next.HasValue;
        }
    }
}
