using System.Collections.Generic;
using System.Linq;
using Woodgrove.Statements.Core.Ledger;
using Woodgrove.Statements.Core.Statements;
using Xunit;
using static Woodgrove.Statements.Tests.TestData;

namespace Woodgrove.Statements.Tests
{
    public class AgedDebtAndPortfolioTests
    {
        [Fact]
        public void Aged_debt_rows_per_customer_sorted_by_controller_and_total()
        {
            var a = Customer(1); a.CreditController = "B";
            var b = Customer(2); b.CreditController = "A";
            var c = Customer(3); c.CreditController = "A";
            var d = Customer(4); d.CreditController = null;
            var entries = List(
                Invoice(1, "1", D(2026, 9, 1), 100m),
                Invoice(2, "2", D(2026, 9, 1), 50m),
                Invoice(3, "3", D(2026, 6, 1), 500m),
                Invoice(4, "4", D(2026, 9, 1), 10m, paid: D(2026, 9, 10)),
                Payment(1, "P", D(2026, 9, 20), 40m, allocated: false));
            var rows = AgedDebt.Build(new[] { a, b, c, d }, entries, D(2026, 9, 30));
            Assert.Equal(new[] { "C10003", "C10002", "C10001" }, rows.Select(r => r.CustomerNo));
            Assert.Equal(60m, rows.Single(r => r.CustomerNo == "C10001").Total);
            Assert.Equal(-40m, rows.Single(r => r.CustomerNo == "C10001").Unallocated);
            Assert.Equal(500m, rows.Single(r => r.CustomerNo == "C10003").Over90);
            Assert.Equal(40m, rows.Single(r => r.CustomerNo == "C10001").LastPaymentAmount);
        }

        [Fact]
        public void Aged_debt_can_include_customers_without_open_items()
        {
            var rows = AgedDebt.Build(new[] { Customer(9) }, new List<LedgerEntry>(), D(2026, 9, 30), includeZero: true);
            Assert.Equal("Anna Virtanen", Assert.Single(rows).CreditController);
        }

        [Theory]
        [InlineData(2026, 9, 30, 2027, 9, 30, 12)]
        [InlineData(2026, 9, 30, 2026, 10, 15, 1)]
        [InlineData(2026, 9, 15, 2026, 9, 30, 1)]
        [InlineData(2026, 10, 1, 2026, 9, 30, 0)]
        public void Remaining_months(int ay, int am, int ad, int ey, int em, int ed, int expected)
        {
            var contract = new LeaseContract { StartDate = D(2024, 1, 1), EndDate = D(ey, em, ed), MonthlyRent = 100m };
            Assert.Equal(expected, contract.RemainingMonths(D(ay, am, ad)));
            Assert.Equal(expected * 100m, contract.RemainingRent(D(ay, am, ad)));
        }

        [Fact]
        public void Portfolio_contains_active_contracts_with_end_bands()
        {
            var customers = new Dictionary<int, Customer> { { 1, Customer(1) } };
            var contracts = new[]
            {
                new LeaseContract { ContractNo = "L1", CustomerId = 1, AssetCategory = "Vehicles", StartDate = D(2024, 1, 1), EndDate = D(2027, 1, 31), MonthlyRent = 100m, Status = "ACTIVE" },
                new LeaseContract { ContractNo = "L2", CustomerId = 1, AssetCategory = "IT equipment", StartDate = D(2025, 1, 1), EndDate = D(2030, 12, 31), MonthlyRent = 50m, Status = "ACTIVE" },
                new LeaseContract { ContractNo = "L3", CustomerId = 1, AssetCategory = "Vehicles", StartDate = D(2020, 1, 1), EndDate = D(2026, 1, 31), MonthlyRent = 70m, Status = "ENDED" },
                new LeaseContract { ContractNo = "L4", CustomerId = 1, AssetCategory = "Vehicles", StartDate = D(2026, 12, 1), EndDate = D(2029, 1, 31), MonthlyRent = 70m, Status = "PENDING" }
            };
            var rows = Portfolio.Build(contracts, customers, D(2026, 9, 30));
            Assert.Equal(new[] { "L2", "L1" }, rows.Select(r => r.ContractNo));
            Assert.Equal("0-6 months", rows.Single(r => r.ContractNo == "L1").EndBand);
            Assert.Equal("over 48 months", rows.Single(r => r.ContractNo == "L2").EndBand);
            Assert.Equal("C10001", rows[0].CustomerNo);
        }
    }
}
