using System;
using System.Collections.Generic;
using VanArsdel.Integration.Monitoring;

namespace VanArsdel.Integration.Data
{
    /// <summary>
    /// Read-only queries against the BizTalk MessageBox (connection "BizTalkMsgBoxDb", NOLOCK, short timeout).
    /// Used by the SLA monitor for counts only; anything that changes instances goes through BizTalkOperations.
    /// nState: 1 ready to run, 2 active, 4 suspended (resumable), 8 dehydrated, 16 completed with discarded messages,
    /// 32 suspended (not resumable).
    /// </summary>
    public class BizTalkDatabaseQueries
    {
        private const string SuspendedSql = @"
SELECT  s.nvcName            AS ServiceName,
        h.nvcApplicationName AS HostName,
        COUNT(*)             AS InstanceCount,
        MIN(i.dtSuspendTimeStamp) AS OldestSuspended,
        MAX(CASE WHEN i.nState = 4 THEN 1 ELSE 0 END) AS AnyResumable,
        MAX(i.nvcErrorDescription) AS ErrorSample
FROM    dbo.Instances i WITH (NOLOCK)
JOIN    dbo.Services s WITH (NOLOCK) ON s.uidServiceID = i.uidServiceID
JOIN    dbo.Applications h WITH (NOLOCK) ON h.uidAppID = i.uidAppOwnerID
WHERE   i.nState IN (4, 32)
GROUP BY s.nvcName, h.nvcApplicationName";

        private const string SpoolSql = "SELECT COUNT_BIG(*) FROM dbo.Spool WITH (NOLOCK)";

        private readonly IntegrationDatabase _msgBox;

        public BizTalkDatabaseQueries(IntegrationDatabase msgBox)
        {
            _msgBox = msgBox;
        }

        public IList<SuspendedInstanceGroup> SuspendedGroups()
        {
            var list = new List<SuspendedInstanceGroup>();
            using (var cmd = _msgBox.Text(SuspendedSql, 15))
            using (var r = _msgBox.Database.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new SuspendedInstanceGroup
                    {
                        ServiceName = IntegrationDatabase.S(r, "ServiceName"),
                        HostName = IntegrationDatabase.S(r, "HostName"),
                        Count = IntegrationDatabase.I(r, "InstanceCount"),
                        OldestSuspendedUtc = IntegrationDatabase.D(r, "OldestSuspended").GetValueOrDefault(DateTime.UtcNow),
                        Resumable = IntegrationDatabase.B(r, "AnyResumable"),
                        ErrorSample = Truncate(IntegrationDatabase.S(r, "ErrorSample"), 400)
                    });
                }
            }

            return list;
        }

        public long SpoolDepth()
        {
            using (var cmd = _msgBox.Text(SpoolSql, 15))
            {
                return Convert.ToInt64(_msgBox.Database.ExecuteScalar(cmd));
            }
        }

        private static string Truncate(string s, int max)
        {
            return s == null || s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}
