using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Contracts.Portal;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Notifications;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Processing.Sales
{
    public class PartLineRequest
    {
        public string ItemId { get; set; }
        public decimal Quantity { get; set; }
        public string DealerLineReference { get; set; }
        public string DealerItemNumber { get; set; }
    }

    public class NewOrderRequest
    {
        public int DealerId { get; set; }
        public string User { get; set; }
        public OrderSource Source { get; set; }
        public string DealerPoNumber { get; set; }
        public DateTime? RequestedDeliveryDate { get; set; }
        public int? DeliveryAddressId { get; set; }
        public string EndCustomerName { get; set; }
        public string Notes { get; set; }
        public decimal ExtraDiscount { get; set; }
        public List<ConfigurationRequest> Machines { get; set; } = new List<ConfigurationRequest>();
        public List<PartLineRequest> Parts { get; set; } = new List<PartLineRequest>();

        /// <summary>Dealer line references for the machine lines (EDI PO1 01), same order as <see cref="Machines"/>.</summary>
        public List<string> MachineLineReferences { get; set; } = new List<string>();
        public int? EdiMessageId { get; set; }
    }

    public class OrderService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(OrderService));

        private readonly IOrderStore _orders;
        private readonly IQuoteStore _quotes;
        private readonly IDealerStore _dealers;
        private readonly ICatalog _catalog;
        private readonly IApprovalStore _approvals;
        private readonly QuoteService _quoteService;
        private readonly PriceCalculator _prices;
        private readonly AxCommandFactory _commands;
        private readonly NotificationComposer _notifications;
        private readonly IClock _clock;
        private readonly ProcessingSettings _settings;

        public OrderService(IOrderStore orders, IQuoteStore quotes, IDealerStore dealers, ICatalog catalog, IApprovalStore approvals,
            QuoteService quoteService, PriceCalculator prices, AxCommandFactory commands, NotificationComposer notifications, IClock clock,
            ProcessingSettings settings)
        {
            _orders = orders;
            _quotes = quotes;
            _dealers = dealers;
            _catalog = catalog;
            _approvals = approvals;
            _quoteService = quoteService;
            _prices = prices;
            _commands = commands;
            _notifications = notifications;
            _clock = clock;
            _settings = settings;
        }

        /// <summary>The dealer accepts an approved quote: the order is created at the quoted prices and submitted.</summary>
        public SalesOrder AcceptQuote(int quoteId, string user, string dealerPo, DateTime? requestedDelivery, int? deliveryAddressId)
        {
            var quote = _quotes.Get(quoteId) ?? throw new KeyNotFoundException($"Quote {quoteId} not found.");
            if (!quote.CanAccept(_clock.Today)) throw new SalesRuleException($"Quote {quote.QuoteNumber} is {quote.Status} and cannot be accepted.");
            var dealer = _dealers.Get(quote.DealerId);
            CheckDuplicatePo(dealer, dealerPo);

            var order = NewOrder(dealer, user, OrderSource.Portal, dealerPo, requestedDelivery, deliveryAddressId, quote.EndCustomerName, quote.Notes);
            order.QuoteId = quote.Id;
            order.ExtraDiscount = quote.ApprovedExtraDiscount;
            order.OrderType = quote.Lines.Any(l => !string.IsNullOrEmpty(l.ModelCode)) ? OrderType.Machine : OrderType.Parts;
            foreach (var line in quote.Lines.OrderBy(l => l.LineNumber))
            {
                var factor = (100m - quote.ApprovedExtraDiscount) / 100m;
                var net = Math.Round(line.NetPrice * factor, Currencies.Decimals(order.Currency), MidpointRounding.AwayFromZero);
                order.Lines.Add(new OrderLine
                {
                    LineNumber = line.LineNumber,
                    ItemId = line.ItemId,
                    ModelCode = line.ModelCode,
                    ConfigurationId = line.ConfigurationId,
                    OptionCodes = line.OptionCodes,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    ListPrice = line.ListPrice,
                    NetPrice = net,
                    DiscountPercent = line.ListPrice == 0 ? 0 : Math.Round((line.ListPrice - net) / line.ListPrice * 100m, 4)
                });
            }

            var model = quote.Lines.Select(l => l.ModelCode).Where(m => m != null).Select(_catalog.Get).FirstOrDefault(m => m != null);
            order.FreightCharge = FreightFor(dealer, order, model);
            _orders.Add(order);

            quote.Status = QuoteStatus.Accepted;
            quote.OrderId = order.Id;
            _quotes.Save(quote);

            // The extra discount was approved on the quote; the order itself only goes through the credit check.
            Route(order, dealer, user, discountApproved: true);
            return order;
        }

        public SalesOrder CreateOrder(NewOrderRequest request)
        {
            var dealer = _dealers.Get(request.DealerId) ?? throw new KeyNotFoundException($"Dealer {request.DealerId} not found.");
            if (request.Machines.Count == 0 && request.Parts.Count == 0) throw new SalesRuleException("The order has no lines.");
            CheckDuplicatePo(dealer, request.DealerPoNumber);

            var order = NewOrder(dealer, request.User, request.Source, request.DealerPoNumber, request.RequestedDeliveryDate, request.DeliveryAddressId,
                request.EndCustomerName, request.Notes);
            order.ExtraDiscount = request.ExtraDiscount;
            order.EdiMessageId = request.EdiMessageId;
            order.OrderType = request.Machines.Count > 0 ? OrderType.Machine : OrderType.Parts;

            var lineNumber = 1;
            ProductModel firstModel = null;
            for (var i = 0; i < request.Machines.Count; i++)
            {
                var machine = request.Machines[i];
                var evaluation = _quoteService.Evaluate(dealer, machine);
                if (!evaluation.IsValid) throw new SalesRuleException($"Line {lineNumber}: " + string.Join(" ", evaluation.Errors));
                var model = _catalog.Get(machine.ModelCode);
                firstModel = firstModel ?? model;
                var list = evaluation.PriceLines.Sum(p => p.ListPrice);
                var net = Math.Round(evaluation.PriceLines.Sum(p => p.NetPrice) * (100m - request.ExtraDiscount) / 100m, Currencies.Decimals(dealer.Currency), MidpointRounding.AwayFromZero);
                order.Lines.Add(new OrderLine
                {
                    LineNumber = lineNumber++,
                    ItemId = model.ItemId,
                    ModelCode = model.ModelCode,
                    ConfigurationId = evaluation.ConfigurationId,
                    OptionCodes = string.Join(",", evaluation.Options),
                    Description = model.Name,
                    Quantity = machine.Quantity <= 0 ? 1 : machine.Quantity,
                    ListPrice = list,
                    NetPrice = net,
                    DiscountPercent = list == 0 ? 0 : Math.Round((list - net) / list * 100m, 4),
                    DealerLineReference = i < request.MachineLineReferences.Count ? request.MachineLineReferences[i] : null
                });
            }

            foreach (var part in request.Parts)
            {
                var description = _catalog.PartDescription(part.ItemId) ?? throw new SalesRuleException($"Item {part.ItemId} is not sold to dealers.");
                var price = _prices.PricePart(part.ItemId, description, dealer, part.Quantity, _clock.Today);
                if (price.ListPrice == 0) throw new SalesRuleException($"No price for {part.ItemId} in {dealer.Currency}.");
                order.Lines.Add(new OrderLine
                {
                    LineNumber = lineNumber++,
                    ItemId = part.ItemId,
                    Description = description,
                    Quantity = part.Quantity,
                    ListPrice = price.ListPrice,
                    NetPrice = price.NetPrice,
                    DiscountPercent = price.DiscountPercent,
                    DealerLineReference = part.DealerLineReference,
                    DealerItemNumber = part.DealerItemNumber
                });
            }

            order.FreightCharge = FreightFor(dealer, order, firstModel);
            _orders.Add(order);
            Route(order, dealer, request.User, discountApproved: false);
            return order;
        }

        /// <summary>
        /// Moves a submitted order to approval, credit hold or approved. Approved orders are queued for AX together with a
        /// stock reservation for parts and stock machines.
        /// </summary>
        private void Route(SalesOrder order, Dealer dealer, string user, bool discountApproved)
        {
            var now = _clock.UtcNow;
            order.ChangeStatus(OrderStatus.Submitted, user, null, now);
            var reasons = new List<Tuple<ApprovalReason, string>>();

            if (!discountApproved && order.ExtraDiscount > dealer.MaxSelfApprovedDiscount)
                reasons.Add(Tuple.Create(ApprovalReason.DiscountAboveLimit, $"Extra discount {order.ExtraDiscount:0.##} % above dealer limit {dealer.MaxSelfApprovedDiscount:0.##} %."));

            var exportModels = order.Lines.Where(l => l.ModelCode != null).Select(l => _catalog.Get(l.ModelCode))
                .Where(m => m != null && !string.IsNullOrEmpty(m.ExportControlClass)).ToList();
            var destination = order.DeliveryAddress?.CountryCode ?? dealer.Addresses.FirstOrDefault(a => a.Id == order.DeliveryAddressId)?.CountryCode ?? dealer.CountryCode;
            if (exportModels.Count > 0 && !_settings.FreeTradeCountries.Contains(destination))
                reasons.Add(Tuple.Create(ApprovalReason.ExportControl, $"{string.Join(", ", exportModels.Select(m => m.ModelCode))} ({exportModels[0].ExportControlClass}) to {destination}."));

            var exposure = dealer.OpenBalance + _orders.OpenOrderValue(dealer.Id, order.Id) + order.OrderValue;
            var creditProblem = dealer.CreditHold
                ? Tuple.Create(ApprovalReason.CreditHold, "Dealer account is on credit hold in AX.")
                : exposure > dealer.CreditLimit
                    ? Tuple.Create(ApprovalReason.CreditLimitExceeded, $"Exposure {exposure:N0} {order.Currency} exceeds credit limit {dealer.CreditLimit:N0}.")
                    : null;

            if (creditProblem != null)
            {
                order.ChangeStatus(OrderStatus.CreditHold, "system", creditProblem.Item2, now);
                reasons.Insert(0, creditProblem);
            }
            else if (reasons.Count > 0)
            {
                order.ChangeStatus(OrderStatus.PendingApproval, "system", string.Join(" ", reasons.Select(r => r.Item2)), now);
            }

            if (reasons.Count > 0)
            {
                foreach (var reason in reasons)
                {
                    var request = new ApprovalRequest
                    {
                        OrderId = order.Id,
                        DealerId = dealer.Id,
                        Reason = reason.Item1,
                        Details = reason.Item2,
                        Amount = order.OrderValue,
                        Currency = order.Currency,
                        RequestedDiscount = order.ExtraDiscount,
                        RequestedUtc = now,
                        RequestedBy = user,
                        Decision = ApprovalDecision.Pending
                    };
                    _approvals.Add(request);
                    _notifications.ApprovalNeeded(request, dealer, order.OrderNumber);
                }
                _orders.Save(order);
                _notifications.OrderEvent(creditProblem != null ? NotificationKind.CreditHold : NotificationKind.OrderReceived, order, dealer);
                Log.InfoFormat("Order {0} waiting for approval: {1}", order.OrderNumber, string.Join(", ", reasons.Select(r => r.Item1)));
                return;
            }

            order.ChangeStatus(OrderStatus.Approved, "system", null, now);
            _orders.Save(order);
            _notifications.OrderEvent(NotificationKind.OrderReceived, order, dealer);
            QueueForAx(order, user);
        }

        internal void QueueForAx(SalesOrder order, string user, string runAs = null)
        {
            var quote = order.QuoteId.HasValue ? _quotes.Get(order.QuoteId.Value) : null;
            order.ChangeStatus(OrderStatus.SentToAx, user, null, _clock.UtcNow);
            _orders.Save(order);
            if (quote != null && !string.IsNullOrEmpty(quote.AxQuotationId))
                _commands.ForOrder(AxCommandType.ConvertQuote, order, user, new ConvertQuotePayload { CustomerRef = order.DealerPoNumber }, runAs);
            else
                _commands.ForOrder(AxCommandType.CreateOrder, order, user, null, runAs);
            if (order.OrderType != OrderType.Machine)
                _commands.ForOrder(AxCommandType.ReserveStock, order, user, null, runAs);
            Log.InfoFormat("Order {0} queued for AX", order.OrderNumber);
        }

        public void Cancel(int orderId, string user, string reason)
        {
            var order = _orders.Get(orderId) ?? throw new KeyNotFoundException($"Order {orderId} not found.");
            if (!order.CanCancel) throw new SalesRuleException($"Order {order.OrderNumber} is {order.Status} and cannot be cancelled.");
            if (string.IsNullOrEmpty(order.AxSalesId))
            {
                order.ChangeStatus(OrderStatus.Cancelled, user, reason, _clock.UtcNow);
                foreach (var approval in _approvals.ForOrder(order.Id).Where(a => a.Decision == ApprovalDecision.Pending))
                {
                    approval.Decision = ApprovalDecision.Rejected;
                    approval.DecisionComment = "Order cancelled";
                    approval.DecidedBy = user;
                    approval.DecidedUtc = _clock.UtcNow;
                    _approvals.Save(approval);
                }
                _orders.Save(order);
                return;
            }

            _commands.ForOrder(AxCommandType.CancelOrder, order, user, new CancelPayload { Reason = reason });
            order.Notes = (order.Notes + Environment.NewLine + $"Cancellation requested by {user}: {reason}").Trim();
            _orders.Save(order);
        }

        public void ChangeRequestedDate(int orderId, string user, DateTime requestedDate)
        {
            var order = _orders.Get(orderId) ?? throw new KeyNotFoundException($"Order {orderId} not found.");
            if (!order.CanChange) throw new SalesRuleException($"Order {order.OrderNumber} is {order.Status} and cannot be changed.");
            if (requestedDate < _clock.Today) throw new SalesRuleException("The requested date is in the past.");
            order.RequestedDeliveryDate = requestedDate;
            _orders.Save(order);
            if (!string.IsNullOrEmpty(order.AxSalesId)) _commands.ForOrder(AxCommandType.UpdateOrder, order, user);
        }

        /// <summary>Sales desk: sends an order in Error back to AX after the data has been fixed.</summary>
        public void Resubmit(int orderId, string deskUser, string axUser)
        {
            var order = _orders.Get(orderId) ?? throw new KeyNotFoundException($"Order {orderId} not found.");
            if (order.Status != OrderStatus.Error) throw new SalesRuleException($"Order {order.OrderNumber} is not in error.");
            order.ErrorMessage = null;
            if (string.IsNullOrEmpty(order.AxSalesId))
            {
                order.ChangeStatus(OrderStatus.Approved, deskUser, "Resubmitted", _clock.UtcNow);
                QueueForAx(order, deskUser, axUser);
            }
            else
            {
                order.ChangeStatus(OrderStatus.CreatedInAx, deskUser, "Resubmitted", _clock.UtcNow);
                _orders.Save(order);
                _commands.ForOrder(AxCommandType.ReadOrder, order, deskUser, null, axUser);
            }
        }

        private SalesOrder NewOrder(Dealer dealer, string user, OrderSource source, string dealerPo, DateTime? requested, int? addressId, string endCustomer, string notes)
        {
            if (!dealer.CanOrder) throw new SalesRuleException($"Dealer {dealer.AccountNumber} is {dealer.Status} and cannot order.");
            if (requested.HasValue && requested.Value.Date < _clock.Today) throw new SalesRuleException("The requested delivery date is in the past.");
            var address = addressId.HasValue ? dealer.Addresses.FirstOrDefault(a => a.Id == addressId.Value) : dealer.Addresses.FirstOrDefault(a => a.IsDefaultDelivery);
            if (addressId.HasValue && address == null) throw new SalesRuleException("The delivery address does not belong to the dealer.");
            var today = _clock.Today;
            return new SalesOrder
            {
                OrderNumber = DocumentNumbers.Order(today.Year, _orders.NextNumber(today.Year)),
                DealerId = dealer.Id,
                Source = source,
                Status = OrderStatus.Draft,
                DealerPoNumber = string.IsNullOrWhiteSpace(dealerPo) ? null : dealerPo.Trim(),
                Currency = dealer.Currency,
                CreatedUtc = _clock.UtcNow,
                CreatedBy = user,
                RequestedDeliveryDate = requested,
                DeliveryAddressId = address?.Id,
                DeliveryAddress = address,
                DeliveryTerms = dealer.DeliveryTerms,
                EndCustomerName = endCustomer,
                Notes = notes
            };
        }

        private void CheckDuplicatePo(Dealer dealer, string dealerPo)
        {
            if (string.IsNullOrWhiteSpace(dealerPo)) return;
            var existing = _orders.FindByDealerPo(dealer.Id, dealerPo.Trim());
            if (existing != null && existing.Status != OrderStatus.Cancelled && existing.Status != OrderStatus.Rejected)
                throw new SalesRuleException($"Purchase order {dealerPo} was already used on order {existing.OrderNumber}.");
        }

        private decimal FreightFor(Dealer dealer, SalesOrder order, ProductModel model)
        {
            if (model == null) return 0m;
            var machines = order.Lines.Where(l => l.ModelCode != null).Sum(l => l.Quantity);
            var config = new MachineConfiguration(model.ModelCode, Enumerable.Empty<string>());
            var country = order.DeliveryAddress?.CountryCode ?? dealer.CountryCode;
            return _prices.Calculate(model, config, dealer, machines <= 0 ? 1 : machines, 0, country, _clock.Today).Freight;
        }
    }
}
