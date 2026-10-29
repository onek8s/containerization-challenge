using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.CoreBanking.Simulator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Lucerne.Lending.Tests;

public sealed class CoreSimulatorTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "lending-tests", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _services;

    public CoreSimulatorTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new CoreOptions { Provider = "Simulator", SimulatorDatabase = Path.Combine(_folder, "core.db") }));
        services.AddCoreSimulator(withEventSource: true);
        _services = services.BuildServiceProvider();
    }

    private ICoreCustomerService Customers => _services.GetRequiredService<ICoreCustomerService>();
    private ICoreLoanService Loans => _services.GetRequiredService<ICoreLoanService>();

    [Fact]
    public async Task Seeded_customer_has_accounts_and_exposure()
    {
        var aino = await Customers.FindByPersonalIdentityCodeAsync("150385-2481");
        Assert.NotNull(aino);
        Assert.Equal("VIRTANEN AINO MARIA", aino!.Name);
        Assert.True(aino.KycComplete);
        var accounts = await Customers.GetAccountsAsync(aino.CustomerNumber);
        Assert.Contains(accounts, a => a.Iban.StartsWith("FI"));
        Assert.All(accounts, a => Assert.True(Lucerne.Lending.Domain.Iban.IsValid(a.Iban)));
        var exposure = await Customers.GetExposureAsync(aino.CustomerNumber);
        Assert.Equal(aino.CustomerNumber, exposure.CustomerNumber);
    }

    [Fact]
    public async Task Blocked_and_delinquent_customers()
    {
        var blocked = await Customers.FindByPersonalIdentityCodeAsync("080883-3776");
        Assert.True(blocked!.Blocked);
        var ville = await Customers.FindByPersonalIdentityCodeAsync("140772-159C");
        var exposure = await Customers.GetExposureAsync(ville!.CustomerNumber);
        Assert.True(exposure.MaxDaysPastDue >= 90);
        Assert.Null(await Customers.FindByPersonalIdentityCodeAsync("190492-6440"));
    }

    [Fact]
    public async Task Create_customer_book_and_disburse()
    {
        var cif = await Customers.CreateCustomerAsync(new NewCustomerRequest("190492-6440", "Elina", "Heikkinen", "Satamakatu 4", "33200",
            "Tampere", "FI", "elina@example.fi", "+358401234507", "0901", "TEST"));
        Assert.False(string.IsNullOrWhiteSpace(cif));
        Assert.Equal(cif, (await Customers.FindByPersonalIdentityCodeAsync("190492-6440"))!.CustomerNumber);

        var reference = _services.GetRequiredService<ICoreReferenceData>();
        var today = await reference.GetBusinessDateAsync();
        var rate = await reference.GetReferenceRateAsync("E12", today);
        Assert.InRange(rate, 0m, 10m);

        var booking = await Loans.BookAsync(new LoanBookingRequest("LA-2026-000001", cif, null, "PL01", 8000m, 48, "E12", 6.9m, 6.9m + rate,
            60m, 5m, 15, today.AddMonths(1), "0901", "TEST"));
        Assert.True(booking.Success, booking.Message);
        Assert.Equal(CoreReturnCodes.Ok, booking.ReturnCode);

        var duplicate = await Loans.BookAsync(new LoanBookingRequest("LA-2026-000001", cif, null, "PL01", 8000m, 48, "E12", 6.9m, 6.9m + rate,
            60m, 5m, 15, today.AddMonths(1), "0901", "TEST"));
        Assert.Equal(CoreReturnCodes.Duplicate, duplicate.ReturnCode);

        var disbursed = await Loans.DisburseAsync(new DisbursementRequest(booking.AccountNumber!, 8000m, "FI2112345600000785", "Loan LA-2026-000001", "TEST"));
        Assert.True(disbursed.Success, disbursed.Message);
        Assert.True(disbursed.ExternalTransfer);

        var loan = await Loans.GetLoanAsync(booking.AccountNumber!);
        Assert.Equal(8000m, loan!.Balance);
        var events = _services.GetRequiredService<ICoreEventSource>();
        var evt = await events.ReceiveAsync(TimeSpan.FromSeconds(2));
        Assert.NotNull(evt);
    }

    [Fact]
    public async Task End_of_day_moves_business_date()
    {
        var ops = _services.GetRequiredService<SimulatorOperations>();
        var before = ops.BusinessDate();
        var result = ops.RunEndOfDay();
        Assert.Equal(before.AddDays(1), result.BusinessDate);
        Assert.Equal(result.BusinessDate, await _services.GetRequiredService<ICoreReferenceData>().GetBusinessDateAsync());
    }

    public void Dispose()
    {
        _services.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, true); } catch (IOException) { }
    }
}
