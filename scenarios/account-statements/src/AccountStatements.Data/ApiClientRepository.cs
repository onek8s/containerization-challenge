using System.Collections.Generic;
using System.Linq;
using Dapper;
using Woodgrove.Statements.Core.Model;

namespace Woodgrove.Statements.Data
{
    public class ApiClientRepository
    {
        const string Columns = "ClientId, Name, KeyPrefix, KeyHash, AllowedReports, Enabled, CreatedBy, CreatedAt, LastUsedAt";
        readonly Db _db;

        public ApiClientRepository(Db db) { _db = db; }

        public ApiClient FindByPrefix(string prefix)
        {
            using (var c = _db.Open())
                return c.QuerySingleOrDefault<ApiClient>("SELECT " + Columns + " FROM stm.ApiClient WHERE KeyPrefix = @prefix", new { prefix });
        }

        public IList<ApiClient> List()
        {
            using (var c = _db.Open())
                return c.Query<ApiClient>("SELECT " + Columns + " FROM stm.ApiClient ORDER BY Name").ToList();
        }

        public void Create(ApiClient client)
        {
            using (var c = _db.Open())
                c.Execute(@"INSERT stm.ApiClient (Name, KeyPrefix, KeyHash, AllowedReports, Enabled, CreatedBy)
                            VALUES (@Name, @KeyPrefix, @KeyHash, @AllowedReports, 1, @CreatedBy)", client);
        }

        public void SetEnabled(int clientId, bool enabled)
        {
            using (var c = _db.Open())
                c.Execute("UPDATE stm.ApiClient SET Enabled = @enabled WHERE ClientId = @clientId", new { clientId, enabled });
        }

        public void ReplaceKey(int clientId, string keyHash)
        {
            using (var c = _db.Open())
                c.Execute("UPDATE stm.ApiClient SET KeyHash = @keyHash WHERE ClientId = @clientId", new { clientId, keyHash });
        }

        public void Touch(int clientId)
        {
            using (var c = _db.Open())
                c.Execute("UPDATE stm.ApiClient SET LastUsedAt = SYSDATETIME() WHERE ClientId = @clientId", new { clientId });
        }
    }
}
