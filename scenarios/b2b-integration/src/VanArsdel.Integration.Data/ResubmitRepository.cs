using System;
using System.Collections.Generic;
using System.Data;

namespace VanArsdel.Integration.Data
{
    public class ResubmitEntry
    {
        public long ResubmitId { get; set; }
        public Guid OriginalInstanceId { get; set; }
        public Guid OriginalMessageId { get; set; }
        public string ServiceName { get; set; }
        public string PartnerId { get; set; }
        public string MessageType { get; set; }
        public string Reason { get; set; }
        public bool BodyEdited { get; set; }
        public string ResubmittedBy { get; set; }
        public DateTime ResubmittedUtc { get; set; }
        public string Target { get; set; }
        public bool OriginalTerminated { get; set; }
    }

    /// <summary>Audit of the ops portal's resubmissions (dbo.ResubmitLog); edited bodies are kept in dbo.ResubmitBody.</summary>
    public class ResubmitRepository
    {
        private readonly IntegrationDatabase _db;

        public ResubmitRepository(IntegrationDatabase db)
        {
            _db = db;
        }

        public long Log(ResubmitEntry entry, byte[] originalBody, byte[] submittedBody)
        {
            using (var cmd = _db.Procedure("dbo.usp_LogResubmission"))
            {
                _db.In(cmd, "@OriginalInstanceId", DbType.Guid, entry.OriginalInstanceId);
                _db.In(cmd, "@OriginalMessageId", DbType.Guid, entry.OriginalMessageId);
                _db.In(cmd, "@ServiceName", DbType.String, entry.ServiceName);
                _db.In(cmd, "@PartnerId", DbType.String, entry.PartnerId);
                _db.In(cmd, "@MessageType", DbType.String, entry.MessageType);
                _db.In(cmd, "@Reason", DbType.String, entry.Reason);
                _db.In(cmd, "@BodyEdited", DbType.Boolean, entry.BodyEdited);
                _db.In(cmd, "@ResubmittedBy", DbType.String, entry.ResubmittedBy);
                _db.In(cmd, "@Target", DbType.String, entry.Target);
                _db.In(cmd, "@OriginalBody", DbType.Binary, originalBody);
                _db.In(cmd, "@SubmittedBody", DbType.Binary, submittedBody);
                return Convert.ToInt64(_db.Database.ExecuteScalar(cmd));
            }
        }

        public void MarkOriginalTerminated(long resubmitId)
        {
            using (var cmd = _db.Text("UPDATE dbo.ResubmitLog SET OriginalTerminated = 1 WHERE ResubmitId = @Id"))
            {
                _db.In(cmd, "@Id", DbType.Int64, resubmitId);
                _db.Database.ExecuteNonQuery(cmd);
            }
        }

        public IList<ResubmitEntry> Recent(int days)
        {
            var list = new List<ResubmitEntry>();
            using (var cmd = _db.Text(@"SELECT TOP (500) ResubmitId, OriginalInstanceId, OriginalMessageId, ServiceName, PartnerId, MessageType, Reason,
       BodyEdited, ResubmittedBy, ResubmittedUtc, Target, OriginalTerminated
FROM dbo.ResubmitLog WHERE ResubmittedUtc >= DATEADD(DAY, -@Days, SYSUTCDATETIME()) ORDER BY ResubmittedUtc DESC"))
            {
                _db.In(cmd, "@Days", DbType.Int32, days);
                using (var r = _db.Database.ExecuteReader(cmd))
                {
                    while (r.Read())
                    {
                        list.Add(new ResubmitEntry
                        {
                            ResubmitId = Convert.ToInt64(r["ResubmitId"]),
                            OriginalInstanceId = (Guid)r["OriginalInstanceId"],
                            OriginalMessageId = (Guid)r["OriginalMessageId"],
                            ServiceName = IntegrationDatabase.S(r, "ServiceName"),
                            PartnerId = IntegrationDatabase.S(r, "PartnerId"),
                            MessageType = IntegrationDatabase.S(r, "MessageType"),
                            Reason = IntegrationDatabase.S(r, "Reason"),
                            BodyEdited = IntegrationDatabase.B(r, "BodyEdited"),
                            ResubmittedBy = IntegrationDatabase.S(r, "ResubmittedBy"),
                            ResubmittedUtc = IntegrationDatabase.D(r, "ResubmittedUtc").GetValueOrDefault(),
                            Target = IntegrationDatabase.S(r, "Target"),
                            OriginalTerminated = IntegrationDatabase.B(r, "OriginalTerminated")
                        });
                    }
                }
            }

            return list;
        }
    }
}
