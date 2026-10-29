using System;
using System.Collections.Generic;
using System.Data;
using VanArsdel.Integration.Data;

namespace VanArsdel.Integration.Tracking.Security
{
    public class SqlPortalUserStore : IPortalUserStore
    {
        private const string Columns = "UserId, UserName, DisplayName, Email, PasswordHash, PartnerId, Roles, Locked, FailedAttempts, LockedUntilUtc, LastLoginUtc, PasswordChangedUtc, MustChangePassword";
        private readonly IntegrationDatabase _db;

        public SqlPortalUserStore(IntegrationDatabase db)
        {
            _db = db;
        }

        public PortalUser Find(string userName)
        {
            using (var cmd = _db.Text("SELECT " + Columns + " FROM dbo.PortalUser WHERE UserName = @UserName"))
            {
                _db.In(cmd, "@UserName", DbType.String, userName);
                var users = Read(cmd);
                return users.Count == 0 ? null : users[0];
            }
        }

        public IList<PortalUser> All()
        {
            using (var cmd = _db.Text("SELECT " + Columns + " FROM dbo.PortalUser ORDER BY PartnerId, UserName"))
            {
                return Read(cmd);
            }
        }

        public void Save(PortalUser user, string changedBy)
        {
            using (var cmd = _db.Procedure("dbo.usp_SavePortalUser"))
            {
                _db.In(cmd, "@UserName", DbType.String, user.UserName);
                _db.In(cmd, "@DisplayName", DbType.String, user.DisplayName);
                _db.In(cmd, "@Email", DbType.String, user.Email);
                _db.In(cmd, "@PasswordHash", DbType.String, user.PasswordHash);
                _db.In(cmd, "@PartnerId", DbType.String, (object)user.PartnerId ?? DBNull.Value);
                _db.In(cmd, "@Roles", DbType.String, user.Roles);
                _db.In(cmd, "@Locked", DbType.Boolean, user.Locked);
                _db.In(cmd, "@MustChangePassword", DbType.Boolean, user.MustChangePassword);
                _db.In(cmd, "@ChangedBy", DbType.String, changedBy);
                _db.Database.ExecuteNonQuery(cmd);
            }
        }

        public void RecordLogin(string userName, bool succeeded, int failedAttempts, DateTime? lockedUntilUtc, DateTime atUtc, string address)
        {
            using (var cmd = _db.Procedure("dbo.usp_RecordPortalLogin"))
            {
                _db.In(cmd, "@UserName", DbType.String, userName);
                _db.In(cmd, "@Succeeded", DbType.Boolean, succeeded);
                _db.In(cmd, "@FailedAttempts", DbType.Int32, failedAttempts);
                _db.In(cmd, "@LockedUntilUtc", DbType.DateTime2, (object)lockedUntilUtc ?? DBNull.Value);
                _db.In(cmd, "@AtUtc", DbType.DateTime2, atUtc);
                _db.In(cmd, "@ClientAddress", DbType.String, address);
                _db.Database.ExecuteNonQuery(cmd);
            }
        }

        private IList<PortalUser> Read(System.Data.Common.DbCommand cmd)
        {
            var list = new List<PortalUser>();
            using (var r = _db.Database.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new PortalUser
                    {
                        UserId = IntegrationDatabase.I(r, "UserId"),
                        UserName = IntegrationDatabase.S(r, "UserName"),
                        DisplayName = IntegrationDatabase.S(r, "DisplayName"),
                        Email = IntegrationDatabase.S(r, "Email"),
                        PasswordHash = IntegrationDatabase.S(r, "PasswordHash"),
                        PartnerId = IntegrationDatabase.S(r, "PartnerId"),
                        Roles = IntegrationDatabase.S(r, "Roles"),
                        Locked = IntegrationDatabase.B(r, "Locked"),
                        FailedAttempts = IntegrationDatabase.I(r, "FailedAttempts"),
                        LockedUntilUtc = IntegrationDatabase.D(r, "LockedUntilUtc"),
                        LastLoginUtc = IntegrationDatabase.D(r, "LastLoginUtc"),
                        PasswordChangedUtc = IntegrationDatabase.D(r, "PasswordChangedUtc").GetValueOrDefault(),
                        MustChangePassword = IntegrationDatabase.B(r, "MustChangePassword")
                    });
                }
            }

            return list;
        }
    }
}
