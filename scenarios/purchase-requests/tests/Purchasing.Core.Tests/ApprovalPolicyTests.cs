using System;
using System.Linq;
using Xunit;

namespace Contoso.Purchasing.Core.Tests
{
    public class ApprovalPolicyTests
    {
        private static readonly DateTime Now = new DateTime(2026, 3, 2, 10, 0, 0);
        private readonly ApprovalPolicy _policy = new ApprovalPolicy();

        [Fact]
        public void Small_consumables_need_only_manager()
        {
            var steps = _policy.RequiredSteps(TestData.Request(100m));
            Assert.Equal(new[] { ApprovalStep.Manager }, steps.ToArray());
        }

        [Fact]
        public void Finance_threshold_adds_finance_step()
        {
            var steps = _policy.RequiredSteps(TestData.Request(2500m));
            Assert.Equal(new[] { ApprovalStep.Manager, ApprovalStep.Finance }, steps.ToArray());
        }

        [Fact]
        public void It_equipment_always_needs_finance()
        {
            var steps = _policy.RequiredSteps(TestData.Request(50m, category: RequestCategory.ItEquipment));
            Assert.Contains(ApprovalStep.Finance, steps);
        }

        [Fact]
        public void Capital_needs_all_three_steps()
        {
            var steps = _policy.RequiredSteps(TestData.Request(10m, category: RequestCategory.Capital));
            Assert.Equal(new[] { ApprovalStep.Manager, ApprovalStep.Finance, ApprovalStep.Director }, steps.ToArray());
        }

        [Fact]
        public void Full_chain_approves_request()
        {
            var request = TestData.Request(8000m, 2);
            _policy.Submit(request, Now);

            Assert.Equal(RequestStatus.InApproval, _policy.Apply(request, TestData.Manager(), ApprovalDecision.Approved, null, Now));
            Assert.Equal(ApprovalStep.Finance, _policy.NextStep(request));
            Assert.Equal(RequestStatus.InApproval, _policy.Apply(request, TestData.Finance(), ApprovalDecision.Approved, null, Now));
            Assert.Equal(ApprovalStep.Director, _policy.NextStep(request));
            Assert.Equal(RequestStatus.Approved, _policy.Apply(request, TestData.Director(), ApprovalDecision.Approved, "ok", Now));
            Assert.Null(_policy.NextStep(request));
        }

        [Fact]
        public void Requester_cannot_approve_own_request()
        {
            var request = TestData.Request(100m);
            _policy.Submit(request, Now);
            var self = TestData.Manager();
            self.Account = request.RequesterAccount;

            Assert.False(_policy.CanDecide(request, self));
        }

        [Fact]
        public void Finance_cannot_take_manager_step()
        {
            var request = TestData.Request(3000m);
            _policy.Submit(request, Now);

            Assert.False(_policy.CanDecide(request, TestData.Finance()));
            Assert.Throws<PolicyException>(() => _policy.Apply(request, TestData.Finance(), ApprovalDecision.Approved, null, Now));
        }

        [Fact]
        public void Same_person_cannot_approve_two_steps()
        {
            var request = TestData.Request(3000m);
            _policy.Submit(request, Now);
            var managerInFinance = TestData.Manager();
            managerInFinance.IsFinance = true;

            _policy.Apply(request, managerInFinance, ApprovalDecision.Approved, null, Now);

            Assert.False(_policy.CanDecide(request, managerInFinance));
        }

        [Fact]
        public void Reject_requires_comment()
        {
            var request = TestData.Request(100m);
            _policy.Submit(request, Now);

            Assert.Throws<PolicyException>(() => _policy.Apply(request, TestData.Manager(), ApprovalDecision.Rejected, " ", Now));
            Assert.Equal(RequestStatus.Rejected, _policy.Apply(request, TestData.Manager(), ApprovalDecision.Rejected, "Use framework agreement", Now));
        }

        [Fact]
        public void Rejected_request_can_be_resubmitted()
        {
            var request = TestData.Request(100m);
            _policy.Submit(request, Now);
            _policy.Apply(request, TestData.Manager(), ApprovalDecision.Rejected, "Wrong supplier", Now);

            _policy.Submit(request, Now.AddDays(1));

            Assert.Equal(RequestStatus.InApproval, request.Status);
            Assert.Empty(request.Approvals);
        }

        [Fact]
        public void Submit_validates_request()
        {
            var request = TestData.Request(100m, category: RequestCategory.Capital);
            request.Justification = "";
            request.Lines[0].Quantity = 0;

            var ex = Assert.Throws<PolicyException>(() => _policy.Submit(request, Now));
            Assert.Contains("justification", ex.Message);
            Assert.Contains("Quantities", ex.Message);
        }
    }
}
