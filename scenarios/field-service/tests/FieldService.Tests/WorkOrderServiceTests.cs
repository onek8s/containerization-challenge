using FieldService.Data;
using FieldService.Data.Entities;
using FieldService.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FieldService.Tests;

public class WorkOrderServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FieldServiceContext _db;
    private readonly WorkOrderService _service;

    public WorkOrderServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new FieldServiceContext(new DbContextOptionsBuilder<FieldServiceContext>().UseSqlite(_connection).Options);
        DbInitializer.InitializeAsync(_db, seedSampleData: true).GetAwaiter().GetResult();
        _service = new WorkOrderService(_db, new NumberSequence(_db), TimeProvider.System);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Create_assigns_sequential_numbers()
    {
        var a = await _service.CreateAsync(new NewWorkOrder("ACME", "Site", "One", null, WorkOrderPriority.Normal));
        var b = await _service.CreateAsync(new NewWorkOrder("ACME", "Site", "Two", null, WorkOrderPriority.Normal));

        var year = DateTime.UtcNow.Year;
        Assert.Equal($"WO-{year}-00005", a.Number);
        Assert.Equal($"WO-{year}-00006", b.Number);
    }

    [Fact]
    public async Task Create_rejects_unknown_customer()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new NewWorkOrder("NOPE", "Site", "x", null, WorkOrderPriority.Low)));
    }

    [Fact]
    public async Task Full_lifecycle_to_uninvoiced_list()
    {
        var order = await _service.CreateAsync(new NewWorkOrder("OAKFIELD", "School", "Broken window", null, WorkOrderPriority.High));
        var tech = await _db.Technicians.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.StartAsync(order.Number));
        await _service.AssignAsync(order.Number, tech.Id);
        await _service.StartAsync(order.Number);
        await _service.CompleteAsync(order.Number, 2m, "Replaced pane", new[]
        {
            new WorkOrderPart { PartNumber = " gl-600 ", Description = "Glass", Quantity = 1, UnitPrice = 40m },
            new WorkOrderPart { PartNumber = "", Quantity = 0 }
        });

        var uninvoiced = await _service.GetUninvoicedAsync();
        var done = Assert.Single(uninvoiced);
        Assert.Equal(order.Number, done.Number);
        Assert.Equal("GL-600", Assert.Single(done.Parts).PartNumber);
        Assert.Equal(185m, InvoiceExport.OrderTotal(done));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelAsync(order.Number));
    }

    [Fact]
    public async Task Overdue_lists_open_orders_past_due()
    {
        var order = await _db.WorkOrders.FirstAsync();
        order.DueUtc = DateTime.UtcNow.AddHours(-1);
        await _db.SaveChangesAsync();

        var overdue = await _service.GetOverdueAsync();

        Assert.Contains(overdue, o => o.Number == order.Number);
    }
}
