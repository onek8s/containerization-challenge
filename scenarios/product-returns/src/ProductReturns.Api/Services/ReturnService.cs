using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProductReturns.Core;
using ProductReturns.Core.Api;
using ProductReturns.Data;

namespace ProductReturns.Api.Services
{
    public class ReturnRuleException : Exception
    {
        public ReturnRuleException(string message, int statusCode = 400)
            : base(message)
        {
            StatusCode = statusCode;
        }

        public int StatusCode { get; }
    }

    public class ReturnService
    {
        private readonly ReturnsRepository _repository;
        private readonly CatalogCache _catalog;
        private readonly ReturnPolicy _policy;
        private readonly RefundCalculator _refunds;
        private readonly IBusinessClock _clock;
        private readonly ILogger<ReturnService> _logger;

        public ReturnService(ReturnsRepository repository, CatalogCache catalog, ReturnPolicy policy, RefundCalculator refunds,
            IBusinessClock clock, ILogger<ReturnService> logger)
        {
            _repository = repository;
            _catalog = catalog;
            _policy = policy;
            _refunds = refunds;
            _clock = clock;
            _logger = logger;
        }

        public static bool SameEmail(string a, string b) =>
            !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
            && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        public async Task<OrderLookupResponse> LookupOrderAsync(string orderNumber, string email, CancellationToken ct)
        {
            var order = await _repository.FindOrderAsync(orderNumber, ct);
            if (order == null || (email != null && !SameEmail(order.CustomerEmail, email)))
            {
                return null;
            }

            var returned = await _repository.ReturnedQuantitiesAsync(order.OrderNumber, ct);
            var lines = _policy.Evaluate(order, _catalog.Find, returned, _clock.Today);
            return new OrderLookupResponse
            {
                OrderNumber = order.OrderNumber,
                CustomerName = order.CustomerName,
                OrderDate = order.OrderDate,
                DeliveredDate = order.DeliveredDate,
                Currency = order.Currency,
                Lines = lines.ToList()
            };
        }

        public async Task<ReturnRequest> CreateAsync(CreateReturnRequest input, string createdBy, CancellationToken ct)
        {
            if (input?.Lines == null || input.Lines.Count == 0 || input.Lines.All(l => l.Quantity <= 0))
            {
                throw new ReturnRuleException("Select at least one item to return.");
            }

            var order = await _repository.FindOrderAsync(input.OrderNumber, ct);
            if (order == null || !SameEmail(order.CustomerEmail, input.Email))
            {
                throw new ReturnRuleException("Order not found.", 404);
            }

            var returned = await _repository.ReturnedQuantitiesAsync(order.OrderNumber, ct);
            var returnable = _policy.Evaluate(order, _catalog.Find, returned, _clock.Today).ToDictionary(l => l.LineNumber);

            var request = new ReturnRequest
            {
                OrderNumber = order.OrderNumber,
                CustomerName = order.CustomerName,
                CustomerEmail = order.CustomerEmail,
                Currency = order.Currency ?? "EUR",
                Status = ReturnStatus.Submitted,
                CreatedUtc = _clock.UtcNow,
                CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "customer" : createdBy
            };

            foreach (var group in input.Lines.Where(l => l.Quantity > 0).GroupBy(l => l.LineNumber))
            {
                if (!returnable.TryGetValue(group.Key, out var line))
                {
                    throw new ReturnRuleException("Order line " + group.Key + " does not exist.");
                }

                var quantity = group.Sum(l => l.Quantity);
                if (quantity > line.ReturnableQuantity)
                {
                    throw new ReturnRuleException(line.ReturnableQuantity == 0
                        ? line.Description + ": " + line.NotReturnableReason
                        : line.Description + ": at most " + line.ReturnableQuantity + " can be returned.");
                }

                var first = group.First();
                if (ReturnReasons.Find(first.ReasonCode) == null)
                {
                    throw new ReturnRuleException("Choose a return reason for " + line.Description + ".");
                }

                request.Lines.Add(new ReturnLine
                {
                    OrderLineNumber = line.LineNumber,
                    Sku = line.Sku,
                    Description = line.Description,
                    Category = line.Category,
                    Quantity = quantity,
                    UnitPrice = line.UnitPrice,
                    ReasonCode = ReturnReasons.Find(first.ReasonCode).Code,
                    CustomerComment = string.IsNullOrWhiteSpace(first.Comment) ? null : first.Comment.Trim()
                });
            }

            await _repository.CreateAsync(request, ct);
            _logger.LogInformation("Return {Rma} created for order {Order} with {Lines} lines", request.RmaNumber, request.OrderNumber, request.Lines.Count);
            return request;
        }

        public async Task<ReturnRequest> ApproveAsync(string rma, string user, CancellationToken ct)
        {
            var request = await RequireAsync(rma, ct);
            if (request.Status != ReturnStatus.Submitted)
            {
                throw new ReturnRuleException("Only submitted returns can be approved.", 409);
            }

            request.Status = ReturnStatus.Approved;
            request.DecidedUtc = _clock.UtcNow;
            request.DecidedBy = user;
            AddNote(request, user, "Approved.");
            await _repository.SaveAsync(ct);
            _logger.LogInformation("Return {Rma} approved by {User}", request.RmaNumber, user);
            return request;
        }

        public async Task<ReturnRequest> RejectAsync(string rma, string user, string reason, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ReturnRuleException("A reason is required.");
            }

            var request = await RequireAsync(rma, ct);
            if (request.Status != ReturnStatus.Submitted && request.Status != ReturnStatus.Approved)
            {
                throw new ReturnRuleException("Only submitted or approved returns can be rejected.", 409);
            }

            request.Status = ReturnStatus.Rejected;
            request.DecidedUtc = _clock.UtcNow;
            request.DecidedBy = user;
            request.RejectReason = reason.Trim();
            AddNote(request, user, "Rejected: " + reason.Trim());
            await _repository.SaveAsync(ct);
            return request;
        }

        public async Task<ReturnRequest> CancelAsync(string rma, string email, CancellationToken ct)
        {
            var request = await RequireAsync(rma, ct);
            if (!SameEmail(request.CustomerEmail, email))
            {
                throw new ReturnRuleException("Return not found.", 404);
            }

            if (request.Status != ReturnStatus.Submitted && request.Status != ReturnStatus.Approved && request.Status != ReturnStatus.LabelIssued)
            {
                throw new ReturnRuleException("This return can no longer be cancelled.", 409);
            }

            request.Status = ReturnStatus.Cancelled;
            AddNote(request, "customer", "Cancelled by the customer.");
            await _repository.SaveAsync(ct);
            return request;
        }

        public async Task<ReturnRequest> AddNoteAsync(string rma, string user, string text, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ReturnRuleException("Note text is required.");
            }

            var request = await RequireAsync(rma, ct);
            AddNote(request, user, text.Trim());
            await _repository.SaveAsync(ct);
            return request;
        }

        public async Task<(ReturnRequest Request, ScanResult Result)> ScanAsync(string rma, string scanner, ScanRequest scan, CancellationToken ct)
        {
            var request = await RequireAsync(rma, ct);
            var result = ReceivingRules.Receive(request, scan.Sku?.Trim(), scan.Quantity, scan.Condition, _clock.UtcNow);
            if (result.Accepted)
            {
                AddNote(request, scanner, $"Received {scan.Quantity} x {scan.Sku} ({scan.Condition}).");
                await _repository.SaveAsync(ct);
            }

            return (request, result);
        }

        public async Task<ReturnRequest> CompleteInspectionAsync(string rma, string user, CancellationToken ct)
        {
            var request = await RequireAsync(rma, ct);
            if (request.Status != ReturnStatus.Received)
            {
                throw new ReturnRuleException("Only received returns can be completed.", 409);
            }

            var refund = _refunds.Calculate(request);
            request.RefundAmount = refund.Refund;
            request.RestockingFee = refund.RestockingFee;
            request.LabelFee = refund.LabelFee;
            request.InspectedUtc = _clock.UtcNow;
            request.InspectedBy = user;
            request.Status = ReturnStatus.Inspected;
            AddNote(request, user, $"Inspection completed, refund {refund.Refund:0.00} {request.Currency}.");
            await _repository.SaveAsync(ct);
            return request;
        }

        public async Task<ReturnRequest> RequireAsync(string rma, CancellationToken ct)
        {
            var request = await _repository.GetAsync(rma, ct);
            if (request == null)
            {
                throw new ReturnRuleException("Return not found.", 404);
            }

            return request;
        }

        private void AddNote(ReturnRequest request, string author, string text)
        {
            request.Notes.Add(new ReturnNote
            {
                Author = string.IsNullOrWhiteSpace(author) ? "system" : author,
                Text = text,
                CreatedUtc = _clock.UtcNow
            });
        }

        public static IEnumerable<ReturnSummary> Summaries(IEnumerable<ReturnRequest> requests) =>
            requests.Select(r => new ReturnSummary
            {
                RmaNumber = r.RmaNumber,
                OrderNumber = r.OrderNumber,
                CustomerName = r.CustomerName,
                CustomerEmail = r.CustomerEmail,
                Status = r.Status,
                CreatedUtc = r.CreatedUtc,
                Items = r.Lines.Sum(l => l.Quantity),
                Value = r.Lines.Sum(l => l.Quantity * l.UnitPrice),
                TrackingNumber = r.TrackingNumber
            });
    }
}
