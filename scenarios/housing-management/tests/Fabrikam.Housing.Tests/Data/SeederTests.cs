using System;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Data.Queries;
using Fabrikam.Housing.Data.Seed;
using Fabrikam.Housing.Repairs;
using FluentAssertions;
using Xunit;

namespace Fabrikam.Housing.Tests.Data
{
    public class SeederTests : IClassFixture<SeededDatabase>
    {
        private readonly SeededDatabase _fixture;

        public SeederTests(SeededDatabase fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Seeds_estates_properties_and_tenancies()
        {
            using (var db = _fixture.Database.CreateContext())
            {
                db.Estates.Count().Should().Be(6);
                db.Properties.Count().Should().Be(320);
                db.Tenancies.Count().Should().BeInRange(280, 315);
                db.Tenants.Count(t => t.PortalSubjectId != null).Should().Be(5);
                db.Contractors.Count().Should().Be(8);
            }
        }

        [Fact]
        public void Work_order_references_are_unique_and_counter_matches_this_year()
        {
            using (var db = _fixture.Database.CreateContext())
            {
                var refs = db.WorkOrders.Select(w => w.Reference).ToList();
                refs.Should().OnlyHaveUniqueItems();
                var counter = db.Counters.Single(c => c.Name == "WorkOrder" + SeededDatabase.Today.Year);
                counter.Value.Should().Be(db.WorkOrders.Count(w => w.ReportedAt.Year == SeededDatabase.Today.Year));
                db.WorkOrders.Count(w => w.Status == WorkOrderStatus.Completed).Should().BeGreaterThan(150);
            }
        }

        [Fact]
        public async Task Counter_service_continues_the_sequence()
        {
            using (var db = _fixture.Database.CreateContext())
            {
                var counters = new CounterService(db, _fixture.Database.Connections);
                var before = db.Counters.Single(c => c.Name == "BacsFile").Value;
                (await counters.NextAsync("BacsFile")).Should().Be(before + 1);
                (await counters.NextAsync("NewCounter")).Should().Be(1);
                (await counters.NextAsync("NewCounter")).Should().Be(2);
            }
        }

        [Fact]
        public async Task Report_queries_run_on_the_development_database()
        {
            var reports = new ReportQueries(_fixture.Database.Connections);
            var arrears = await reports.ArrearsAsync(50m);
            arrears.Should().NotBeEmpty();
            arrears.Should().BeInDescendingOrder(a => a.Balance);
            arrears.First().LeadTenant.Should().NotBeNullOrEmpty();

            var perf = await reports.RepairsPerformanceAsync(SeededDatabase.Today.AddDays(-90), SeededDatabase.Today, SeededDatabase.Today);
            perf.Sum(p => p.Completed).Should().BeGreaterThan(50);
            (await reports.ComplianceAsync()).Should().NotBeEmpty();
            (await reports.RentRollAsync()).Count.Should().Be(320);
            (await reports.DashboardCountsAsync(SeededDatabase.Today))["OpenRepairs"].Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task Statement_running_balance_matches_account_balance()
        {
            var queries = new RentAccountQueries(_fixture.Database.Connections);
            int tenancyId;
            using (var db = _fixture.Database.CreateContext()) tenancyId = db.Tenancies.OrderBy(t => t.Id).Skip(10).First().Id;
            var statement = await queries.StatementAsync(tenancyId, new DateTime(2000, 1, 1), SeededDatabase.Today);
            statement.Last().Balance.Should().Be(await queries.BalanceAsync(tenancyId));
        }

        [Fact]
        public async Task Audit_writer_inserts_with_dapper_contrib()
        {
            var writer = new AuditWriter(_fixture.Database.Connections);
            await writer.WriteAsync("tester", "WorkOrder", "R21-000001", "Viewed", new { Reason = "test" });
            using (var db = _fixture.Database.CreateContext())
                db.Set<Fabrikam.Housing.Common.AuditEntry>().Count(a => a.Actor == "tester").Should().Be(1);
        }
    }

    public sealed class SeededDatabase : IDisposable
    {
        public static readonly DateTime Today = new DateTime(2021, 6, 14);

        public SeededDatabase()
        {
            Database = new SqliteDatabase();
            using (var db = Database.CreateContext())
                new DevelopmentSeeder(db, Today).Seed();
        }

        public SqliteDatabase Database { get; }

        public void Dispose() => Database.Dispose();
    }
}
