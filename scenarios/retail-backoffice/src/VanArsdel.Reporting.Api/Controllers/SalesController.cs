using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using VanArsdel.BuildingBlocks.Caching;
using VanArsdel.Reporting.Api.Data;

namespace VanArsdel.Reporting.Api.Controllers;

/// <summary>Small aggregates for the back office start page.</summary>
[ApiController]
[Route("api/sales")]
[Authorize(Policy = Policies.Read)]
public class SalesController : ControllerBase
{
    private readonly ReportingDb _db;
    private readonly IDistributedCache _cache;

    public SalesController(ReportingDb db, IDistributedCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public class DayTotal
    {
        public DateTime BusinessDate { get; set; }
        public string Channel { get; set; }
        public decimal NetEur { get; set; }
        public int Units { get; set; }
    }

    /// <summary>Net sales per day and channel, cached for five minutes.</summary>
    [HttpGet("daily")]
    public Task<List<DayTotal>> Daily([FromQuery] int days = 28, [FromQuery] string market = null)
    {
        days = Math.Clamp(days, 1, 400);
        var key = $"sales:daily:{days}:{market}:{DateTime.Today:yyyyMMdd}";
        return _cache.GetOrSetJsonAsync(key, TimeSpan.FromMinutes(5), async () =>
        {
            await using var connection = await _db.OpenAsync();
            var rows = await connection.QueryAsync<DayTotal>(
                "SELECT BusinessDate, Channel, SUM(NetAmountEur) AS NetEur, SUM(Quantity) AS Units FROM FactSales " +
                "WHERE BusinessDate >= @from AND (@market IS NULL OR Market = @market) GROUP BY BusinessDate, Channel ORDER BY BusinessDate, Channel",
                new { from = DateTime.Today.AddDays(-days), market });
            return rows.ToList();
        });
    }

    public class StoreRank
    {
        public string LocationCode { get; set; }
        public string Name { get; set; }
        public decimal NetEur { get; set; }
    }

    [HttpGet("stores/top")]
    public async Task<IEnumerable<StoreRank>> TopStores([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int take = 10)
    {
        await using var connection = await _db.OpenAsync();
        return await connection.QueryAsync<StoreRank>(
            _db.Top("f.LocationCode, COALESCE(s.Name, f.LocationCode) AS Name, SUM(f.NetAmountEur) AS NetEur",
                "FROM FactSales f LEFT JOIN DimStore s ON s.StoreCode = f.LocationCode WHERE f.Channel = 'Store' AND f.BusinessDate >= @from AND f.BusinessDate <= @to " +
                "GROUP BY f.LocationCode, s.Name ORDER BY SUM(f.NetAmountEur) DESC", Math.Clamp(take, 1, 200)),
            new { from = (from ?? DateTime.Today.AddDays(-7)).Date, to = (to ?? DateTime.Today).Date });
    }

    [HttpGet("stock-alerts")]
    public async Task<IEnumerable<dynamic>> StockAlerts([FromQuery] int hours = 24)
    {
        await using var connection = await _db.OpenAsync();
        return await connection.QueryAsync(
            "SELECT a.LocationCode, a.Sku, p.Name, a.OnHand, a.RaisedUtc FROM FactStockAlert a LEFT JOIN DimProduct p ON p.Sku = a.Sku " +
            "WHERE a.RaisedUtc >= @since ORDER BY a.RaisedUtc DESC",
            new { since = DateTime.UtcNow.AddHours(-Math.Clamp(hours, 1, 720)) });
    }
}
