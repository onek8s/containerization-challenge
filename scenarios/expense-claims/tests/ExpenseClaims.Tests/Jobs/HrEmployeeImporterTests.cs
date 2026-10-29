using ExpenseClaims.Jobs.Hr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExpenseClaims.Tests.Jobs;

public class HrEmployeeImporterTests
{
    private static IReadOnlyList<EmployeeRecord> SampleFile()
    {
        using var file = File.OpenRead(TestFiles.Sample("hr", "employees_20221012.csv"));
        return HrEmployeeImporter.Read(file);
    }

    [Fact]
    public void Sample_export_is_read()
    {
        var records = SampleFile();
        Assert.Equal(8, records.Count);
        var jari = records.Single(r => r.EmployeeNumber == "100150");
        Assert.Equal("Mäkinen", jari.LastName);
        Assert.Equal("100102", jari.ManagerEmployeeNumber);
        Assert.Equal(new DateTime(2022, 10, 31), records.Single(r => r.EmployeeNumber == "100255").EmploymentEnd);
    }

    [Fact]
    public async Task Import_adds_updates_and_deactivates()
    {
        using var database = new TestDatabase();
        await using (var db = database.CreateContext())
        {
            var leaver = TestDatabase.Employee("099999", "Old Employee");
            var changed = TestDatabase.Employee("100233", "Aino Laine-Old", manager: "100102");
            db.Employees.AddRange(leaver, changed);
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateContext())
        {
            var result = await new HrEmployeeImporter(db, NullLogger<HrEmployeeImporter>.Instance)
                .ImportAsync(SampleFile(), maxDeactivationShare: 1.0, today: new DateTime(2022, 11, 1), CancellationToken.None);
            Assert.Equal(7, result.Added);
            Assert.Equal(1, result.Updated);
            Assert.Contains(result.Rejected, r => r.StartsWith("100255"));
        }

        await using (var db = database.CreateContext())
        {
            Assert.False((await db.Employees.SingleAsync(e => e.EmployeeNumber == "099999")).Active);
            var aino = await db.Employees.SingleAsync(e => e.EmployeeNumber == "100233");
            Assert.Equal("Aino Laine", aino.DisplayName);
            Assert.Equal("100150", aino.ManagerEmployeeNumber);
            Assert.Equal("FI1447380010009051", aino.Iban);
            var emilia = await db.Employees.SingleAsync(e => e.EmployeeNumber == "100255");
            Assert.False(emilia.Active);
            Assert.Null(emilia.Iban);
            Assert.Null((await db.Employees.SingleAsync(e => e.EmployeeNumber == "100241")).Iban);
        }
    }

    [Fact]
    public async Task Import_stops_when_file_looks_incomplete()
    {
        using var database = new TestDatabase();
        await using (var db = database.CreateContext())
        {
            for (var i = 0; i < 40; i++)
                db.Employees.Add(TestDatabase.Employee($"2000{i:00}", $"Person {i}"));
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateContext())
        {
            var importer = new HrEmployeeImporter(db, NullLogger<HrEmployeeImporter>.Instance);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                importer.ImportAsync(SampleFile(), 0.1, new DateTime(2022, 10, 12), CancellationToken.None));
        }
    }
}
