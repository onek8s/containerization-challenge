using Humongous.Quoting.Data;
using Humongous.Quoting.Data.Entities;
using Humongous.Quoting.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Humongous.Quoting.Tests;

public sealed class DataTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly QuotingDbContext _db;

    public DataTests()
    {
        _connection.Open();
        _db = new QuotingDbContext(new DbContextOptionsBuilder<QuotingDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.Agencies.AddRange(SampleData.Agencies());
        _db.SaveChanges();
    }

    [Fact]
    public async Task Sequences_hand_out_consecutive_numbers()
    {
        var numbers = new NumberGenerator(_db);
        Assert.Equal(1, await numbers.NextAsync("Quote"));
        Assert.Equal(2, await numbers.NextAsync("Quote"));
        Assert.Equal(1, await numbers.NextAsync("Policy"));
        Assert.Equal(3, await numbers.NextAsync("Quote"));
    }

    [Fact]
    public async Task Allocating_a_number_keeps_tracked_changes_pending()
    {
        var agency = await _db.Agencies.FindAsync("ALP01");
        agency!.CommissionRate = 0.15m;

        await new NumberGenerator(_db).NextAsync("Policy");

        Assert.Equal(EntityState.Modified, _db.Entry(agency).State);
    }

    [Fact]
    public async Task Risk_and_premium_round_trip_as_json()
    {
        var quote = NewQuote("ALP01", QuoteStatus.Quoted, 1);
        quote.Premium = PremiumCalculator.Calculate(TestRisks.Rated((SectionCode.MaterialDamage, 400m)), TestRisks.Product, 0.2m);
        quote.Loadings = new() { ["MD"] = 10m };
        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await _db.Quotes.SingleAsync(q => q.Id == quote.Id);
        Assert.Equal("Kettle & Crumb Ltd", loaded.Risk.Insured.Name);
        Assert.Equal(9, loaded.Risk.EmployersLiability!.Employees);
        Assert.Equal(quote.Premium.Total, loaded.Premium!.Total);
        Assert.Equal(10m, loaded.Loadings["MD"]);
        Assert.Equal("LS1 6HD", loaded.Postcode);
    }

    [Fact]
    public async Task Quote_list_and_dashboard_are_scoped_to_the_agency()
    {
        _db.Quotes.AddRange(NewQuote("ALP01", QuoteStatus.Quoted, 1), NewQuote("ALP01", QuoteStatus.Referred, 2), NewQuote("NWB02", QuoteStatus.Quoted, 3));
        await _db.SaveChangesAsync();
        var queries = new QuotingQueries(_db);

        Assert.Equal(2, (await queries.QuoteListAsync("ALP01", null, 0, 25)).Count);
        Assert.Equal(3, (await queries.QuoteListAsync(null, null, 0, 25)).Count);
        Assert.Single(await queries.QuoteListAsync(null, "Referred", 0, 25));
        Assert.Single(await queries.QuoteListAsync(null, null, 0, 25, "000003"));

        var dashboard = await queries.DashboardAsync("ALP01", DateTime.UtcNow);
        Assert.Equal(2, dashboard.QuotesByStatus.Sum(s => s.Count));

        var queue = await queries.ReferralQueueAsync();
        Assert.Equal("ALP01", Assert.Single(queue).AgencyCode);
    }

    [Fact]
    public async Task Bordereau_lines_cover_the_month()
    {
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "HBC/000001/24",
            AgencyCode = "ALP01",
            InceptionDate = new DateOnly(2024, 5, 1),
            ExpiryDate = new DateOnly(2025, 4, 30),
            ProductVersion = "2024.1",
            Risk = TestRisks.Cafe(),
            InsuredName = "Kettle & Crumb Ltd",
            CreatedUtc = new DateTime(2024, 4, 20, 10, 0, 0, DateTimeKind.Utc)
        };
        policy.Transactions.Add(new PolicyTransaction { Id = Guid.NewGuid(), Type = TransactionType.NewBusiness, EffectiveDate = policy.InceptionDate, GrossPremium = 1000m, Ipt = 120m, Commission = 200m, CreatedUtc = new DateTime(2024, 4, 20, 10, 0, 0, DateTimeKind.Utc), SectionGross = new() { ["MD"] = 1000m } });
        policy.Transactions.Add(new PolicyTransaction { Id = Guid.NewGuid(), Type = TransactionType.MidTermAdjustment, EffectiveDate = new DateOnly(2024, 6, 1), GrossPremium = 50m, Ipt = 6m, Commission = 10m, CreatedUtc = new DateTime(2024, 5, 30, 10, 0, 0, DateTimeKind.Utc) });
        _db.Policies.Add(policy);
        await _db.SaveChangesAsync();

        var lines = await new QuotingQueries(_db).BordereauLinesAsync(new DateTime(2024, 4, 1), new DateTime(2024, 5, 1));

        var line = Assert.Single(lines);
        Assert.Equal("NewBusiness", line.TransactionType);
        Assert.Equal("Alpine Insurance Brokers Ltd", line.AgencyName);
        Assert.Contains("\"MD\"", line.SectionGrossJson);
    }

    private static Quote NewQuote(string agency, QuoteStatus status, int n)
    {
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            Reference = References.Quote(2024, n),
            AgencyCode = agency,
            CreatedBy = "test",
            Status = status,
            ProductVersion = "2024.1",
            InceptionDate = new DateOnly(2024, 7, 1),
            CommissionRate = 0.2m,
            CreatedUtc = DateTime.UtcNow.AddMinutes(n),
            UpdatedUtc = DateTime.UtcNow,
            ReferredUtc = status == QuoteStatus.Referred ? DateTime.UtcNow : null
        };
        quote.SetRisk(TestRisks.Cafe());
        return quote;
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
