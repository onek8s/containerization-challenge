using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using ProductReturns.Data;

namespace ProductReturns.Tests
{
    /// <summary>SQLite database file with the sample ERP data, deleted after the test.</summary>
    public sealed class TestDatabase : IDisposable
    {
        public TestDatabase()
        {
            Folder = Path.Combine(Path.GetTempPath(), "returns-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Folder);
            ConnectionString = "Data Source=" + Path.Combine(Folder, "returns.db");
            using (var db = CreateContext())
            {
                db.Database.EnsureCreated();
                SampleData.Load(db);
            }
        }

        public string Folder { get; }

        public string ConnectionString { get; }

        public ReturnsDbContext CreateContext() =>
            new ReturnsDbContext(new DbContextOptionsBuilder<ReturnsDbContext>().UseSqlite(ConnectionString).Options);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Folder, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
