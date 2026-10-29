using System;
using System.Threading.Tasks;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;

namespace Fabrikam.Housing.Data
{
    /// <summary>
    /// Writes the audit trail with Dapper.Contrib so audit rows are stored even when the EF unit of work fails.
    /// </summary>
    public class AuditWriter
    {
        private readonly ConnectionFactory _connections;

        public AuditWriter(ConnectionFactory connections)
        {
            _connections = connections;
        }

        public static string ApplicationName { get; set; } = "Housing";

        public async Task WriteAsync(string actor, string entityType, string entityKey, string action, object details = null)
        {
            using (var connection = _connections.Open())
            {
                var row = new AuditRow
                {
                    At = DateTime.Now,
                    Actor = actor ?? "system",
                    Application = ApplicationName,
                    EntityType = entityType,
                    EntityKey = entityKey,
                    Action = action,
                    Details = details == null ? null : details as string ?? JsonConvert.SerializeObject(details)
                };
                if (_connections.IsSqlite)
                    await connection.InsertAsync(new SqliteAuditRow(row));
                else
                    await connection.InsertAsync(row);
            }
        }

        [Table("housing.AuditEntries")]
        public class AuditRow
        {
            [Key] public long Id { get; set; }
            public DateTime At { get; set; }
            public string Actor { get; set; }
            public string Application { get; set; }
            public string EntityType { get; set; }
            public string EntityKey { get; set; }
            public string Action { get; set; }
            public string Details { get; set; }
        }

        [Table("AuditEntries")]
        public class SqliteAuditRow : AuditRow
        {
            public SqliteAuditRow() { }

            public SqliteAuditRow(AuditRow r)
            {
                At = r.At; Actor = r.Actor; Application = r.Application; EntityType = r.EntityType;
                EntityKey = r.EntityKey; Action = r.Action; Details = r.Details;
            }
        }
    }
}
