using System;
using System.Linq;

namespace ProductReturns.Core
{
    public class RefundResult
    {
        public decimal LinesTotal { get; set; }
        public decimal RestockingFee { get; set; }
        public decimal LabelFee { get; set; }
        public decimal Refund { get; set; }
    }

    /// <summary>Calculates the refund from the inspected return lines.</summary>
    public class RefundCalculator
    {
        private readonly ReturnPolicy _policy;

        public RefundCalculator(ReturnPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public RefundResult Calculate(ReturnRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var options = _policy.Options;
            var result = new RefundResult();

            foreach (var line in request.Lines)
            {
                var quantity = Math.Min(line.ReceivedQuantity, line.Quantity);
                var amount = Round(quantity * line.UnitPrice);
                var companyFault = ReturnReasons.IsCompanyFault(line.ReasonCode);
                decimal lineRefund;
                decimal fee = 0m;

                switch (line.Condition)
                {
                    case ItemCondition.AsNew:
                        lineRefund = amount;
                        break;
                    case ItemCondition.Opened:
                        if (!companyFault && line.Category != null
                            && options.OpenedRestockingFee != null
                            && options.OpenedRestockingFee.TryGetValue(line.Category, out var share))
                        {
                            fee = Round(amount * share);
                        }

                        lineRefund = amount - fee;
                        break;
                    case ItemCondition.Used:
                        lineRefund = companyFault ? amount : Round(amount * options.UsedRefundShare);
                        fee = amount - lineRefund;
                        break;
                    case ItemCondition.Unsellable:
                        lineRefund = companyFault ? amount : 0m;
                        fee = amount - lineRefund;
                        break;
                    default:
                        lineRefund = 0m;
                        break;
                }

                line.RefundAmount = lineRefund;
                result.LinesTotal += amount;
                result.RestockingFee += fee;
            }

            var received = request.Lines.Where(l => l.ReceivedQuantity > 0).ToList();
            result.LabelFee = received.Count > 0 && _policy.ChargesLabelFee(received) ? options.LabelFee : 0m;
            var refund = request.Lines.Sum(l => l.RefundAmount ?? 0m) - result.LabelFee;
            result.Refund = refund < 0m ? 0m : refund;
            return result;
        }

        private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
