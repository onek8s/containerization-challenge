using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExpenseClaims.Data;

/// <summary>Used by <c>dotnet ef</c> to create migrations and the deployment script.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ExpenseClaimsDbContext>
{
    public ExpenseClaimsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("EXPENSECLAIMS_DESIGN_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ExpenseClaims;Trusted_Connection=True";
        var options = new DbContextOptionsBuilder<ExpenseClaimsDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "exp"))
            .Options;
        return new ExpenseClaimsDbContext(options);
    }
}
