using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PartsCatalog.Data;
using PartsCatalog.Models;
using PartsCatalog.Services;
using Xunit;

namespace PartsCatalog.Tests;

public class PartServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CatalogContext _db;
    private readonly PartService _svc;

    public PartServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new CatalogContext(new DbContextOptionsBuilder<CatalogContext>().UseSqlite(_connection).Options);
        SeedData.Initialize(_db);
        _svc = new PartService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Search_ByTerm_MatchesPartNumberAndDescription()
    {
        var byNumber = await _svc.SearchAsync("BRG", null, false);
        var byDescription = await _svc.SearchAsync("V-belt", null, false);

        Assert.Equal(2, byNumber.Count);
        Assert.Single(byDescription);
    }

    [Fact]
    public async Task Search_ReorderOnly_ReturnsPartsAtOrBelowReorderLevel()
    {
        var result = await _svc.SearchAsync(null, null, true);

        Assert.NotEmpty(result);
        Assert.All(result, p => Assert.True(p.QuantityOnHand <= p.ReorderLevel));
    }

    [Fact]
    public async Task AdjustStock_UpdatesQuantity()
    {
        var part = await _svc.AdjustStockAsync("BLT-A42", -5);

        Assert.NotNull(part);
        Assert.Equal(30, part!.QuantityOnHand);
    }

    [Fact]
    public async Task AdjustStock_BelowZero_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _svc.AdjustStockAsync("MTR-0.75KW-4P", -10));
    }

    [Fact]
    public async Task Add_NormalisesPartNumber()
    {
        await _svc.AddAsync(new Part { PartNumber = " grs-1 ", Description = "Grease cartridge", QuantityOnHand = 1 });

        Assert.NotNull(await _svc.FindByNumberAsync("GRS-1"));
    }
}
