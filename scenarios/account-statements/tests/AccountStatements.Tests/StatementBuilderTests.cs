using System.Linq;
using Woodgrove.Statements.Core.Ledger;
using Woodgrove.Statements.Core.Statements;
using Xunit;
using static Woodgrove.Statements.Tests.TestData;

namespace Woodgrove.Statements.Tests
{
    public class StatementBuilderTests
    {
        readonly StatementBuilder _builder = new StatementBuilder();

        [Fact]
        public void Opening_lines_and_closing_balance()
        {
            var c = Customer();
            var entries = List(
                Invoice(1, "100", D(2026, 8, 1), 1000m, paid: D(2026, 8, 14)),
                Payment(1, "P100", D(2026, 8, 14), 1000m),
                Invoice(1, "101", D(2026, 9, 1), 1000m, paid: D(2026, 9, 20)),
                Payment(1, "P101", D(2026, 9, 20), 1000m),
                Invoice(1, "102", D(2026, 9, 15), 250m));

            var st = _builder.Build(c, entries, D(2026, 9, 1), D(2026, 9, 30));

            Assert.Equal(0m, st.OpeningBalance);
            Assert.Equal(3, st.Lines.Count);
            Assert.Equal(new[] { "101", "102", "P101" }, st.Lines.Select(l => l.DocumentNo));
            Assert.Equal(new[] { 1000m, 1250m, 250m }, st.Lines.Select(l => l.Balance));
            Assert.Equal(1250m, st.TotalDebits);
            Assert.Equal(1000m, st.TotalCredits);
            Assert.Equal(250m, st.ClosingBalance);
            Assert.Equal("ST202609-C10001", st.StatementNo);
        }

        [Fact]
        public void Invoices_before_payments_on_the_same_day()
        {
            var entries = List(Payment(1, "P1", D(2026, 9, 5), 100m, allocated: false), Invoice(1, "1", D(2026, 9, 5), 100m));
            var st = _builder.Build(Customer(), entries, D(2026, 9, 1), D(2026, 9, 30));
            Assert.Equal(EntryTypes.Invoice, st.Lines[0].EntryType);
            Assert.Equal(100m, st.Lines[0].Balance);
        }

        [Fact]
        public void Item_closed_after_statement_date_is_open_on_the_statement()
        {
            var entries = List(Invoice(1, "1", D(2026, 9, 1), 500m, paid: D(2026, 10, 3)), Payment(1, "P1", D(2026, 10, 3), 500m));
            var st = _builder.Build(Customer(), entries, D(2026, 9, 1), D(2026, 9, 30));
            var item = Assert.Single(st.OpenItems);
            Assert.Equal(500m, item.OpenAmount);
            Assert.Equal(16, item.DaysOverdue);
            Assert.Equal(AgingBucketNames.Days1To30, item.Bucket);
            Assert.Equal(500m, st.ClosingBalance);
            Assert.DoesNotContain(st.Lines, l => l.DocumentNo == "P1");
        }

        [Fact]
        public void Aging_buckets_and_unallocated_credit()
        {
            var entries = List(
                Invoice(1, "A", D(2026, 5, 1), 100m),
                Invoice(1, "B", D(2026, 7, 1), 200m),
                Invoice(1, "C", D(2026, 8, 1), 300m),
                Invoice(1, "D", D(2026, 9, 1), 400m),
                Invoice(1, "E", D(2026, 9, 25), 500m),
                Payment(1, "X", D(2026, 9, 28), 50m, allocated: false));
            var st = _builder.Build(Customer(), entries, D(2026, 9, 1), D(2026, 9, 30));
            Assert.Equal(500m, st.Aging.NotDue);
            Assert.Equal(400m, st.Aging.Days1To30);
            Assert.Equal(300m, st.Aging.Days31To60);
            Assert.Equal(200m, st.Aging.Days61To90);
            Assert.Equal(100m, st.Aging.Over90);
            Assert.Equal(-50m, st.Aging.Unallocated);
            Assert.Equal(1000m, st.Aging.Overdue);
            Assert.Equal(1450m, st.Aging.Total);
            Assert.Equal(st.ClosingBalance, st.Aging.Total);
        }

        [Fact]
        public void Partial_payment_keeps_open_amount()
        {
            var entries = List(Invoice(1, "A", D(2026, 9, 1), 1000m, open: 400m), Payment(1, "P", D(2026, 9, 20), 600m));
            var st = _builder.Build(Customer(), entries, D(2026, 9, 1), D(2026, 9, 30));
            Assert.Equal(400m, Assert.Single(st.OpenItems).OpenAmount);
        }

        [Theory]
        [InlineData(2026, 9, 10, 0)]
        [InlineData(2026, 9, 1, 1)]
        [InlineData(2026, 8, 1, 2)]
        [InlineData(2026, 7, 1, 3)]
        public void Dunning_level_by_oldest_overdue_item(int y, int m, int d, int expected)
        {
            var entries = List(Invoice(1, "A", D(y, m, d), 100m));
            var st = _builder.Build(Customer(), entries, D(2026, 9, 1), D(2026, 9, 30));
            Assert.Equal(expected, st.DunningLevel);
        }

        [Fact]
        public void Over_credit_limit_raises_dunning_level_to_two()
        {
            var entries = List(Invoice(1, "A", D(2026, 9, 1), 5000m));
            var st = _builder.Build(Customer(creditLimit: 1000m), entries, D(2026, 9, 1), D(2026, 9, 30));
            Assert.Equal(2, st.DunningLevel);
        }

        [Fact]
        public void Other_customers_and_future_entries_are_ignored()
        {
            var entries = List(Invoice(2, "OTHER", D(2026, 9, 1), 100m), Invoice(1, "LATER", D(2026, 10, 1), 100m));
            var st = _builder.Build(Customer(), entries, D(2026, 9, 1), D(2026, 9, 30));
            Assert.False(st.HasActivity);
        }

        [Fact]
        public void Period_start_after_statement_date_is_rejected()
        {
            Assert.Throws<System.ArgumentException>(() => _builder.Build(Customer(), List(), D(2026, 10, 1), D(2026, 9, 30)));
        }
    }
}
