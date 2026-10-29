using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Data;

/// <summary>Allocates house shipment numbers from the ct.ShipmentNumbers sequence.</summary>
public sealed class ShipmentNumberAllocator
{
    private readonly ControlTowerDbContext _db;

    public ShipmentNumberAllocator(ControlTowerDbContext db) => _db = db;

    public async Task<string> NextAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        long next;
        if (_db.IsSqlite)
        {
            var count = await _db.Shipments.CountAsync(ct);
            next = count + 1;
        }
        else
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
            cmd.CommandText = "SELECT NEXT VALUE FOR ct.ShipmentNumbers";
            next = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
        }
        return ShipmentNumber.Format(now.Year, next % 1_000_000);
    }
}
