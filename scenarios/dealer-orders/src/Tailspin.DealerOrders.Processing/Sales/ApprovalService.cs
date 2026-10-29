using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Notifications;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Processing.Sales
{
    public class ApprovalService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ApprovalService));

        private readonly IApprovalStore _approvals;
        private readonly IQuoteStore _quotes;
        private readonly IOrderStore _orders;
        private readonly IDealerStore _dealers;
        private readonly QuoteService _quoteService;
        private readonly OrderService _orderService;
        private readonly NotificationComposer _notifications;
        private readonly Configurator.PriceCalculator _prices;
        private readonly IClock _clock;

        public ApprovalService(IApprovalStore approvals, IQuoteStore quotes, IOrderStore orders, IDealerStore dealers, QuoteService quoteService,
            OrderService orderService, NotificationComposer notifications, Configurator.PriceCalculator prices, IClock clock)
        {
            _approvals = approvals;
            _quotes = quotes;
            _orders = orders;
            _dealers = dealers;
            _quoteService = quoteService;
            _orderService = orderService;
            _notifications = notifications;
            _prices = prices;
            _clock = clock;
        }

        /// <param name="isManager">The desk user is in the sales manager group (needed for large or credit approvals).</param>
        /// <param name="axUser">AX user alias of the desk user; the AX order is then created in their name.</param>
        public void Decide(int approvalId, string deskUser, bool isManager, bool approve, decimal? discount, string comment, string axUser)
        {
            var request = _approvals.Get(approvalId) ?? throw new KeyNotFoundException($"Approval {approvalId} not found.");
            if (request.Decision != ApprovalDecision.Pending) throw new SalesRuleException("The approval has already been decided.");

            var amountEur = request.Amount * (_prices.Rate(request.Currency, Currencies.Euro, _clock.Today) ?? 1m);
            if (approve && request.RequiresManager(amountEur) && !isManager)
                throw new SalesRuleException($"{NotificationComposer.Describe(request.Reason)} for {amountEur:N0} EUR needs a sales manager.");
            if (!approve && string.IsNullOrWhiteSpace(comment))
                throw new SalesRuleException("Give a reason for the rejection; it is sent to the dealer.");

            request.Decision = approve ? ApprovalDecision.Approved : ApprovalDecision.Rejected;
            request.DecidedBy = deskUser;
            request.DecidedUtc = _clock.UtcNow;
            request.DecisionComment = comment;
            request.ApprovedDiscount = approve ? discount ?? request.RequestedDiscount : (decimal?)null;
            _approvals.Save(request);
            Log.InfoFormat("Approval {0} ({1}) {2} by {3}", request.Id, request.Reason, request.Decision, deskUser);

            if (request.QuoteId.HasValue) DecideQuote(request, deskUser, approve, comment);
            else if (request.OrderId.HasValue) DecideOrder(request, deskUser, approve, comment, axUser);
        }

        private void DecideQuote(ApprovalRequest request, string deskUser, bool approve, string comment)
        {
            var quote = _quotes.Get(request.QuoteId.Value);
            var dealer = _dealers.Get(quote.DealerId);
            if (approve)
            {
                _quoteService.Approve(quote, request.ApprovedDiscount ?? quote.RequestedExtraDiscount, comment, deskUser);
                _notifications.QuoteEvent(NotificationKind.QuoteApproved, quote, dealer, null);
            }
            else
            {
                _quoteService.Reject(quote, comment);
                _notifications.QuoteEvent(NotificationKind.QuoteRejected, quote, dealer, null);
            }
        }

        private void DecideOrder(ApprovalRequest request, string deskUser, bool approve, string comment, string axUser)
        {
            var order = _orders.Get(request.OrderId.Value);
            var dealer = _dealers.Get(order.DealerId);
            if (!approve)
            {
                order.ErrorMessage = comment;
                order.ChangeStatus(OrderStatus.Rejected, deskUser, comment, _clock.UtcNow);
                foreach (var other in _approvals.ForOrder(order.Id).Where(a => a.Decision == ApprovalDecision.Pending))
                {
                    other.Decision = ApprovalDecision.Rejected;
                    other.DecidedBy = deskUser;
                    other.DecidedUtc = _clock.UtcNow;
                    other.DecisionComment = "Order rejected: " + comment;
                    _approvals.Save(other);
                }
                _orders.Save(order);
                _notifications.OrderEvent(NotificationKind.OrderRejected, order, dealer);
                return;
            }

            if (request.Reason == ApprovalReason.DiscountAboveLimit && request.ApprovedDiscount.HasValue && request.ApprovedDiscount.Value != order.ExtraDiscount)
                Reprice(order, request.ApprovedDiscount.Value);

            if (_approvals.ForOrder(order.Id).Any(a => a.Decision == ApprovalDecision.Pending))
            {
                _orders.Save(order);
                return;
            }

            if (order.Status == OrderStatus.CreditHold) order.ChangeStatus(OrderStatus.PendingApproval, deskUser, "Credit released", _clock.UtcNow);
            order.ChangeStatus(OrderStatus.Approved, deskUser, comment, _clock.UtcNow);
            _orders.Save(order);
            _notifications.OrderEvent(NotificationKind.OrderApproved, order, dealer);
            _orderService.QueueForAx(order, deskUser, axUser);
        }

        private static void Reprice(SalesOrder order, decimal newDiscount)
        {
            var oldFactor = (100m - order.ExtraDiscount) / 100m;
            var newFactor = (100m - newDiscount) / 100m;
            foreach (var line in order.Lines.Where(l => l.ModelCode != null))
            {
                var beforeExtra = oldFactor == 0 ? line.NetPrice : line.NetPrice / oldFactor;
                line.NetPrice = Math.Round(beforeExtra * newFactor, Currencies.Decimals(order.Currency), MidpointRounding.AwayFromZero);
                line.DiscountPercent = line.ListPrice == 0 ? 0 : Math.Round((line.ListPrice - line.NetPrice) / line.ListPrice * 100m, 4);
            }
            order.ExtraDiscount = newDiscount;
        }
    }
}
