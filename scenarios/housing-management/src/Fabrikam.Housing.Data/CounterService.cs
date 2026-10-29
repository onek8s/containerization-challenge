using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.Data
{
    /// <summary>
    /// Gap-free counters for human readable references (work orders, payment runs, Bacs file numbers).
    /// </summary>
    public class CounterService
    {
        private readonly HousingDbContext _db;
        private readonly ConnectionFactory _connections;

        public CounterService(HousingDbContext db, ConnectionFactory connections)
        {
            _db = db;
            _connections = connections;
        }

        public async Task<int> NextAsync(string name)
        {
            var table = _connections.Table("Counters");
            // A single UPDATE takes the row lock on SQL Server and the database lock on SQLite.
            var updated = await _db.Database.ExecuteSqlRawAsync($"UPDATE {table} SET Value = Value + 1 WHERE Name = {{0}}", name);
            if (updated == 0)
            {
                _db.Counters.Add(new Counter { Name = name, Value = 1 });
                await _db.SaveChangesAsync();
                return 1;
            }
            return await _db.Counters.AsNoTracking().Where(c => c.Name == name).Select(c => c.Value).SingleAsync();
        }
    }
}
