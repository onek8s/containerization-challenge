using FurnaceRecords.Core;
using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace FurnaceRecords.Tests;

public sealed class SqliteDb : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public SqliteDb()
    {
        _connection.Open();
        using var db = Create();
        DbInitializer.InitializeAsync(db, new DatabaseOptions { Provider = "Sqlite", SeedSampleData = true }).GetAwaiter().GetResult();
    }

    public FurnaceRecordsContext Create() =>
        new(new DbContextOptionsBuilder<FurnaceRecordsContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}

public class LoadServiceTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 3, 2, 6, 0, 0, TimeSpan.Zero));

    public void Dispose() => _db.Dispose();

    private static PlanLoadRequest Request(string furnace = "F01", string recipe = "ST-7075") =>
        new(furnace, recipe, new[] { new PlannedPart("WO-1", "7Q4410-203", "H1", 12) }, "MES01");

    [Fact]
    public async Task Plans_loads_with_running_numbers()
    {
        using var db = _db.Create();
        var service = new LoadService(db, _time);
        var a = await service.PlanAsync(Request());
        var b = await service.PlanAsync(Request());
        Assert.Equal("F01-26-00001", a.LoadNumber);
        Assert.Equal("F01-26-00002", b.LoadNumber);
        Assert.Equal(a.Id, (await service.NextPlannedAsync(a.FurnaceId))!.Id);
    }

    [Fact]
    public async Task Rejects_unqualified_furnace_and_missing_parts()
    {
        using var db = _db.Create();
        var service = new LoadService(db, _time);
        var ex = await Assert.ThrowsAsync<LoadException>(() => service.PlanAsync(Request("F02") with { Parts = Array.Empty<PlannedPart>() }));
        Assert.Contains("not qualified", ex.Message);
        Assert.Contains("At least one part", ex.Message);
        await Assert.ThrowsAsync<LoadException>(() => service.PlanAsync(Request("F01", "NOPE")));
    }

    [Fact]
    public async Task Rejects_furnace_with_overdue_survey()
    {
        using (var db = _db.Create())
        {
            var f = await db.Furnaces.SingleAsync(x => x.Code == "F01");
            f.AccuracyTestDue = new DateOnly(2026, 3, 1);
            await db.SaveChangesAsync();
        }
        using var db2 = _db.Create();
        var ex = await Assert.ThrowsAsync<LoadException>(() => new LoadService(db2, _time).PlanAsync(Request()));
        Assert.Contains("overdue", ex.Message);
    }

    [Fact]
    public async Task Records_completes_evaluates_and_releases()
    {
        Load load;
        using (var db = _db.Create())
        {
            var service = new LoadService(db, _time);
            load = await service.PlanAsync(Request());
            load = (await service.NextPlannedAsync(load.FurnaceId))!;
            await service.StartAsync(load, _time.GetUtcNow().UtcDateTime);
        }

        var t0 = _time.GetUtcNow().UtcDateTime;
        var samples = new List<Sample>();
        for (var s = 0; s <= 120 * 60; s += 10)
        {
            var m = s / 60.0;
            foreach (var ch in new[] { 0, 4, 5, 6, 7 })
            {
                samples.Add(new Sample(t0.AddSeconds(s), ch, Math.Min(480, 20 + 10 * Math.Max(0, m - ch / 2.0))));
            }
        }
        using (var db = _db.Create())
        {
            var service = new LoadService(db, _time);
            await service.AppendAsync(load.Id, samples, new[] { new LoadEvent(t0.AddMinutes(118), LoadEventKind.DoorOpen), new LoadEvent(t0.AddMinutes(118).AddSeconds(9), LoadEventKind.QuenchStart) });
            var result = await service.CompleteAsync(load.Id, t0.AddMinutes(120), TimeSpan.FromSeconds(10));
            Assert.True(result.Passed, string.Join("; ", result.Findings.Select(f => f.Message)));
        }

        using (var db = _db.Create())
        {
            var service = new LoadService(db, _time);
            var stored = (await service.FindAsync(load.LoadNumber))!;
            Assert.Equal(LoadStatus.Completed, stored.Status);
            Assert.Equal(9, stored.QuenchDelaySeconds);
            Assert.Equal(DateTimeKind.Utc, stored.EndedAtUtc!.Value.Kind);

            await Assert.ThrowsAsync<LoadException>(() => service.DecideAsync(load.LoadNumber, false, "CORP\\q1", null, stored.RowVersion));
            await service.DecideAsync(load.LoadNumber, true, "CORP\\q1", null, stored.RowVersion);
        }

        using (var db = _db.Create())
        {
            var stored = (await new LoadService(db, _time).FindAsync(load.LoadNumber))!;
            Assert.Equal(LoadStatus.Released, stored.Status);
            Assert.Equal("CORP\\q1", stored.DecidedBy);
            await Assert.ThrowsAsync<LoadException>(() => new LoadService(db, _time).DecideAsync(load.LoadNumber, false, "CORP\\q2", "late", stored.RowVersion));
        }
    }

    [Fact]
    public async Task Stale_row_version_is_rejected()
    {
        string number;
        byte[] version;
        using (var db = _db.Create())
        {
            var service = new LoadService(db, _time);
            var load = await service.PlanAsync(Request());
            load.Status = LoadStatus.Completed;
            load.Passed = false;
            await db.SaveChangesAsync();
            number = load.LoadNumber;
            version = load.RowVersion;
        }
        using (var db = _db.Create())
        {
            var load = await db.Loads.SingleAsync(l => l.LoadNumber == number);
            load.DecisionComment = "touched";
            await db.SaveChangesAsync();
        }
        using (var db = _db.Create())
        {
            var ex = await Assert.ThrowsAsync<LoadException>(() => new LoadService(db, _time).DecideAsync(number, false, "CORP\\q1", "bad", version));
            Assert.Contains("changed by someone else", ex.Message);
        }
    }
}
