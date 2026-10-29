using System.Collections.Generic;
using System.Data;
using Northwind.Inspections.Core;

namespace Northwind.Inspections.Infrastructure.Data
{
    public sealed class AdjusterRepository
    {
        private const string Columns = "Id, Login, DisplayName, Email, Company, PasswordHash, IsActive, IsSupervisor, LastLoginUtc";
        private readonly Db db;

        public AdjusterRepository(Db db)
        {
            this.db = db;
        }

        public Adjuster FindByLogin(string login)
        {
            return db.Single("SELECT " + Columns + " FROM dbo.Adjusters WHERE Login = @login", Map, Db.P("@login", SqlDbType.NVarChar, 64, login));
        }

        public Adjuster Get(int id)
        {
            return db.Single("SELECT " + Columns + " FROM dbo.Adjusters WHERE Id = @id", Map, Db.P("@id", id));
        }

        public List<Adjuster> All()
        {
            return db.Query("SELECT " + Columns + " FROM dbo.Adjusters ORDER BY DisplayName", Map);
        }

        public void RecordLogin(int id, bool success)
        {
            db.Execute(success
                ? "UPDATE dbo.Adjusters SET LastLoginUtc = SYSUTCDATETIME(), FailedLogins = 0 WHERE Id = @id"
                : "UPDATE dbo.Adjusters SET FailedLogins = FailedLogins + 1, IsActive = CASE WHEN FailedLogins + 1 >= 8 THEN 0 ELSE IsActive END WHERE Id = @id",
                Db.P("@id", id));
        }

        public void SetPassword(int id, string passwordHash)
        {
            db.Execute("UPDATE dbo.Adjusters SET PasswordHash = @hash, FailedLogins = 0 WHERE Id = @id", Db.P("@id", id), Db.P("@hash", passwordHash));
        }

        public bool VerifyPartner(string userName, string password)
        {
            var hash = db.Scalar("SELECT PasswordHash FROM dbo.Partners WHERE UserName = @user AND IsActive = 1",
                Db.P("@user", SqlDbType.NVarChar, 64, userName)) as string;
            return PasswordHasher.Verify(password, hash);
        }

        private static Adjuster Map(IDataRecord r)
        {
            return new Adjuster
            {
                Id = (int)r["Id"],
                Login = (string)r["Login"],
                DisplayName = (string)r["DisplayName"],
                Email = Db.Str(r, "Email"),
                Company = Db.Str(r, "Company"),
                PasswordHash = Db.Str(r, "PasswordHash"),
                IsActive = (bool)r["IsActive"],
                IsSupervisor = (bool)r["IsSupervisor"],
                LastLoginUtc = Db.Date(r, "LastLoginUtc")
            };
        }
    }
}
