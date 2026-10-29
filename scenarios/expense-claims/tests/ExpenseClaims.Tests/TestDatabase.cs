using ExpenseClaims.Data;
using ExpenseClaims.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ExpenseClaims.Tests;

/// <summary>In-memory SQLite database that lives as long as the test.</summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public ExpenseClaimsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ExpenseClaimsDbContext>().UseSqlite(_connection).Options);

    public static Employee Employee(string number, string name, string? manager = null, string? iban = "FI1447380010009051") => new()
    {
        EmployeeNumber = number,
        Upn = name.ToLowerInvariant().Replace(' ', '.') + "@lamnahealthcare.com",
        DisplayName = name,
        Email = name.ToLowerInvariant().Replace(' ', '.') + "@lamnahealthcare.com",
        CostCenter = "2140",
        ManagerEmployeeNumber = manager,
        Iban = iban,
        UpdatedAt = DateTime.Now
    };

    public void Dispose() => _connection.Dispose();
}
