using System;
using Fabrikam.Housing.Data;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.Tests.Data
{
    /// <summary>A file-backed SQLite database per test class (Dapper queries open their own connections).</summary>
    public sealed class SqliteDatabase : IDisposable
    {
        public SqliteDatabase()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "housing-test-" + Guid.NewGuid().ToString("N") + ".db");
            ConnectionString = "Data Source=" + Path;
            Connections = new ConnectionFactory("Sqlite", ConnectionString);
            using (var db = CreateContext()) db.Database.EnsureCreated();
        }

        public string Path { get; }
        public string ConnectionString { get; }
        public ConnectionFactory Connections { get; }

        public HousingDbContext CreateContext()
        {
            return new HousingDbContext(new DbContextOptionsBuilder<HousingDbContext>().UseSqlite(ConnectionString).Options);
        }

        public void Dispose()
        {
            try { System.IO.File.Delete(Path); } catch (System.IO.IOException) { }
        }
    }
}
