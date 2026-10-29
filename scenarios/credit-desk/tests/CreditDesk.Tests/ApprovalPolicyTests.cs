using System;
using CreditDesk.Core;
using Xunit;

namespace CreditDesk.Tests
{
    public class ApprovalPolicyTests
    {
        private static readonly DateTime Today = new DateTime(2024, 3, 15);
        private readonly ApprovalPolicy _policy = new ApprovalPolicy();

        private static CustomerAccount Account(decimal limit = 20000m) => new CustomerAccount
        {
            AccountNumber = "C10442",
            Name = "Alpine Hardware GmbH",
            CreditLimit = limit,
            CustomerSince = new DateTime(2015, 1, 1)
        };

        [Theory]
        [InlineData(20000, 15000, ApprovalLevel.Automatic)]
        [InlineData(20000, 22500, ApprovalLevel.Automatic)]
        [InlineData(20000, 22600, ApprovalLevel.CreditController)]
        [InlineData(20000, 50000, ApprovalLevel.CreditController)]
        [InlineData(40000, 50100, ApprovalLevel.FinanceManager)]
        [InlineData(200000, 250000, ApprovalLevel.FinanceManager)]
        [InlineData(200000, 250100, ApprovalLevel.CreditCommittee)]
        public void RequiredLevel_follows_thresholds(decimal current, decimal requested, ApprovalLevel expected)
        {
            Assert.Equal(expected, _policy.RequiredLevel(current, requested));
        }

        [Fact]
        public void Validate_accepts_normal_increase()
        {
            Assert.Empty(_policy.Validate(Account(), 30000m, "Seasonal stock build-up for spring", Today));
        }

        [Fact]
        public void Validate_rejects_unrounded_amount_and_short_reason()
        {
            var errors = _policy.Validate(Account(), 30050m, "more", Today);
            Assert.Equal(2, errors.Count);
        }

        [Fact]
        public void Validate_blocks_increase_when_on_stop_or_overdue()
        {
            var account = Account();
            account.OnStop = true;
            account.OldestOverdueDays = 75;
            var errors = _policy.Validate(account, 30000m, "Customer opened second branch", Today);
            Assert.Equal(2, errors.Count);
        }

        [Fact]
        public void Validate_allows_decrease_when_overdue()
        {
            var account = Account();
            account.OldestOverdueDays = 120;
            account.OnStop = true;
            Assert.Empty(_policy.Validate(account, 5000m, "Reduce exposure after late payments", Today));
        }

        [Fact]
        public void Validate_limits_multiplier()
        {
            var errors = _policy.Validate(Account(10000m), 40000m, "Large framework agreement signed", Today);
            Assert.Single(errors);
        }

        [Fact]
        public void Validate_limits_young_customers()
        {
            var account = Account(30000m);
            account.CustomerSince = new DateTime(2023, 11, 1);
            var errors = _policy.Validate(account, 60000m, "Rapid growth in orders", Today);
            Assert.Single(errors);
        }

        [Theory]
        [InlineData(ApprovalLevel.CreditController, ApproverRoles.CreditController, true)]
        [InlineData(ApprovalLevel.CreditController, ApproverRoles.SalesRep, false)]
        [InlineData(ApprovalLevel.FinanceManager, ApproverRoles.CreditController, false)]
        [InlineData(ApprovalLevel.FinanceManager, ApproverRoles.FinanceManager, true)]
        [InlineData(ApprovalLevel.CreditCommittee, ApproverRoles.FinanceManager, false)]
        [InlineData(ApprovalLevel.CreditCommittee, ApproverRoles.CreditCommittee | ApproverRoles.SalesRep, true)]
        public void CanDecide_respects_roles(ApprovalLevel level, ApproverRoles roles, bool expected)
        {
            Assert.Equal(expected, ApprovalPolicy.CanDecide(level, roles));
        }

        [Fact]
        public void ValidUntil_is_one_year_from_decision_date()
        {
            Assert.Equal(new DateTime(2025, 3, 15), _policy.ValidUntil(new DateTime(2024, 3, 15, 16, 30, 0)));
        }
    }
}
