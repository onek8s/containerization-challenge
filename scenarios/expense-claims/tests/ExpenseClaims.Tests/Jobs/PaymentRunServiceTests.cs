using System.Xml.Linq;
using ExpenseClaims.Core.Claims;
using ExpenseClaims.Core.Payments;
using ExpenseClaims.Data.Entities;
using ExpenseClaims.Jobs.Bank;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ExpenseClaims.Tests.Jobs;

public sealed class PaymentRunServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly string _outbound = Path.Combine(Path.GetTempPath(), "expense-claims-tests", Guid.NewGuid().ToString("N"));

    private PaymentRunService Service(Data.ExpenseClaimsDbContext db) => new(db, Options.Create(new BankOptions
    {
        Mode = "Folder",
        OutboundFolder = _outbound,
        DebtorIban = "FI7515723500050136",
        DebtorBic = "OKOYFIHH",
        DebtorOrganisationId = "2468135-7"
    }), NullLogger<PaymentRunService>.Instance);

    private static ExpenseClaim Claim(Employee e, string number, decimal total, decimal reimbursable, ClaimStatus status = ClaimStatus.Checked) => new()
    {
        Number = number,
        Employee = e,
        Purpose = "Test",
        CostCenter = e.CostCenter,
        Status = status,
        Total = total,
        Reimbursable = reimbursable,
        CreatedAt = DateTime.Now
    };

    [Fact]
    public async Task One_payment_per_employee_with_reimbursable_amounts()
    {
        await using (var db = _database.CreateContext())
        {
            var aino = TestDatabase.Employee("100233", "Aino Laine");
            var jari = TestDatabase.Employee("100150", "Jari Makinen", iban: "FI3715903000000776");
            var noBank = TestDatabase.Employee("100241", "Oskari Heikkinen", iban: null);
            db.Claims.AddRange(
                Claim(aino, "EC-2022-000001", 300m, 120m),
                Claim(aino, "EC-2022-000002", 80m, 80m),
                Claim(jari, "EC-2022-000003", 58.90m, 0m),
                Claim(noBank, "EC-2022-000004", 20m, 20m),
                Claim(jari, "EC-2022-000005", 45m, 45m, ClaimStatus.Approved));
            await db.SaveChangesAsync();
        }

        PaymentRunService.CreatedRun? created;
        await using (var db = _database.CreateContext())
            created = await Service(db).CreateAsync(new DateTime(2022, 10, 14, 13, 0, 0), CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(1, created!.Run.PaymentCount);
        Assert.Equal(200m, created.Run.Total);
        Assert.Equal(new DateOnly(2022, 10, 14), created.Run.ExecutionDate);
        Assert.True(File.Exists(created.Path));

        XNamespace ns = Pain001Writer.Namespace;
        var doc = XDocument.Load(created.Path);
        Assert.Equal("Expense claims EC-2022-000001 EC-2022-000002", doc.Descendants(ns + "Ustrd").Single().Value);

        await using (var db = _database.CreateContext())
        {
            var statuses = await db.Claims.ToDictionaryAsync(c => c.Number, c => c.Status);
            Assert.Equal(ClaimStatus.InPayment, statuses["EC-2022-000001"]);
            Assert.Equal(ClaimStatus.Paid, statuses["EC-2022-000003"]);
            Assert.Equal(ClaimStatus.Checked, statuses["EC-2022-000004"]);
            Assert.Equal(ClaimStatus.Approved, statuses["EC-2022-000005"]);

            var run = await db.PaymentRuns.SingleAsync();
            await Service(db).CompleteAsync(run, accepted: false, "AC01 incorrect account", DateTime.Now, CancellationToken.None);
        }

        await using (var db = _database.CreateContext())
        {
            Assert.All(await db.Claims.Where(c => c.Number == "EC-2022-000001" || c.Number == "EC-2022-000002").ToListAsync(),
                c => Assert.Equal((ClaimStatus.Checked, (int?)null), (c.Status, c.PaymentRunId)));
            Assert.Equal(PaymentRunStatus.Failed, (await db.PaymentRuns.SingleAsync()).Status);
        }
    }

    [Theory]
    [InlineData("2022-10-14 13:59", "2022-10-14")]
    [InlineData("2022-10-14 14:00", "2022-10-17")]
    [InlineData("2022-10-15 09:00", "2022-10-17")]
    [InlineData("2022-10-17 08:00", "2022-10-17")]
    public void Execution_date_follows_cut_off(string now, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), PaymentRunService.ExecutionDate(DateTime.Parse(now), new TimeSpan(14, 0, 0)));

    public void Dispose()
    {
        _database.Dispose();
        if (Directory.Exists(_outbound))
            Directory.Delete(_outbound, recursive: true);
    }
}
