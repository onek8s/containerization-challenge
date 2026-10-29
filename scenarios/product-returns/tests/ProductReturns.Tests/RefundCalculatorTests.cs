using ProductReturns.Core;
using Xunit;

namespace ProductReturns.Tests
{
    public class RefundCalculatorTests
    {
        private static readonly RefundCalculator Calculator = new RefundCalculator(new ReturnPolicy(new ReturnPolicyOptions()));

        private static ReturnLine Line(string category, string reason, ItemCondition condition, int qty = 1, int received = 1, decimal price = 100m) =>
            new ReturnLine { Category = category, ReasonCode = reason, Condition = condition, Quantity = qty, ReceivedQuantity = received, UnitPrice = price };

        [Fact]
        public void As_new_customer_choice_pays_label_fee()
        {
            var request = new ReturnRequest();
            request.Lines.Add(Line("Home", "CHANGED_MIND", ItemCondition.AsNew));
            var result = Calculator.Calculate(request);
            Assert.Equal(5.95m, result.LabelFee);
            Assert.Equal(94.05m, result.Refund);
        }

        [Fact]
        public void Opened_electronics_have_restocking_fee_unless_company_fault()
        {
            var request = new ReturnRequest();
            request.Lines.Add(Line("Electronics", "NO_LONGER_NEEDED", ItemCondition.Opened, price: 249m));
            var result = Calculator.Calculate(request);
            Assert.Equal(37.35m, result.RestockingFee);
            Assert.Equal(249m - 37.35m - 5.95m, result.Refund);

            request = new ReturnRequest();
            request.Lines.Add(Line("Electronics", "DEFECTIVE", ItemCondition.Opened, price: 249m));
            result = Calculator.Calculate(request);
            Assert.Equal(0m, result.RestockingFee);
            Assert.Equal(0m, result.LabelFee);
            Assert.Equal(249m, result.Refund);
        }

        [Fact]
        public void Used_and_unsellable_items()
        {
            var request = new ReturnRequest();
            request.Lines.Add(Line("Home", "DAMAGED", ItemCondition.AsNew));
            request.Lines.Add(Line("Home", "CHANGED_MIND", ItemCondition.Used, price: 50m));
            request.Lines.Add(Line("Home", "CHANGED_MIND", ItemCondition.Unsellable, price: 30m));
            var result = Calculator.Calculate(request);
            Assert.Equal(0m, result.LabelFee);
            Assert.Equal(25m + 30m, result.RestockingFee);
            Assert.Equal(125m, result.Refund);
            Assert.Equal(25m, request.Lines[1].RefundAmount);
            Assert.Equal(0m, request.Lines[2].RefundAmount);
        }

        [Fact]
        public void Only_received_quantity_is_refunded_and_refund_is_not_negative()
        {
            var request = new ReturnRequest();
            request.Lines.Add(Line("Home", "CHANGED_MIND", ItemCondition.AsNew, qty: 3, received: 1, price: 4m));
            var result = Calculator.Calculate(request);
            Assert.Equal(4m, result.LinesTotal);
            Assert.Equal(0m, result.Refund);
        }
    }
}
