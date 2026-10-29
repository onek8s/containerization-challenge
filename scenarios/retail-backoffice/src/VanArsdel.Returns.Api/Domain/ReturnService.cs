using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refit;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.Contracts.Returns;
using VanArsdel.Integration.Carrier;
using VanArsdel.Integration.Payments;
using VanArsdel.Returns.Api.Clients;
using VanArsdel.Returns.Api.Data;

namespace VanArsdel.Returns.Api.Domain;

public class ReturnsOptions
{
    public int ReturnWindowDays { get; set; } = 30;
    public string ReturnsCentre { get; set; } = "DC01";
    public string CarrierApiKey { get; set; } = "";
    public string CarrierWebhookSecret { get; set; } = "";
}

public class RegisterReturn
{
    public string OrderNo { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public List<RegisterReturnLine> Lines { get; set; } = new();
}

public class RegisterReturnLine
{
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public string ReasonCode { get; set; } = "Other";
}

public class ReturnService
{
    private readonly ReturnsDbContext _db;
    private readonly IOrdersApi _orders;
    private readonly ICarrierApi _carrier;
    private readonly IPspApi _psp;
    private readonly IOutbox _outbox;
    private readonly ReturnsOptions _options;
    private readonly ILogger<ReturnService> _logger;

    public ReturnService(ReturnsDbContext db, IOrdersApi orders, ICarrierApi carrier, IPspApi psp, IOutbox outbox,
        IOptions<ReturnsOptions> options, ILogger<ReturnService> logger)
    {
        _db = db;
        _orders = orders;
        _carrier = carrier;
        _psp = psp;
        _outbox = outbox;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ReturnRequest> RegisterAsync(RegisterReturn request, string channel, string storeCode, CancellationToken ct)
    {
        OrderView order;
        try
        {
            order = await _orders.GetOrderAsync(request.OrderNo);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new NotFoundException("Order", request.OrderNo);
        }

        if (!string.Equals(order.CustomerEmail, request.CustomerEmail?.Trim(), StringComparison.OrdinalIgnoreCase) && channel == "Web")
        {
            throw new DomainException("The e-mail address does not match the order.");
        }
        if (order.Status is not ("Completed" or "Shipped" or "PartlyShipped"))
        {
            throw new DomainException($"Order {order.OrderNo} is {order.Status} and cannot be returned.");
        }
        if (order.PlacedUtc < DateTime.UtcNow.AddDays(-_options.ReturnWindowDays))
        {
            throw new DomainException($"The {_options.ReturnWindowDays}-day return period has ended.");
        }

        var previous = await _db.Returns.AsNoTracking().Include(r => r.Lines)
            .Where(r => r.OrderNo == order.OrderNo && r.Status != ReturnStatus.Rejected).SelectMany(r => r.Lines).ToListAsync(ct);

        var ret = new ReturnRequest
        {
            Id = Guid.NewGuid(),
            RmaNo = "R" + DateTime.UtcNow.ToString("yyMMdd") + Random.Shared.Next(10000, 99999),
            OrderNo = order.OrderNo,
            OrderId = order.Id,
            Channel = channel,
            StoreCode = storeCode,
            CustomerEmail = order.CustomerEmail,
            LoyaltyCardNo = order.LoyaltyCardNo,
            Market = order.Market,
            Currency = order.Currency,
            PaymentReference = order.PaymentReference,
            PaymentMethod = order.PaymentMethod,
            OrderPlacedUtc = order.PlacedUtc,
            CreatedUtc = DateTime.UtcNow,
        };

        foreach (var l in request.Lines.Where(l => l.Quantity > 0))
        {
            var ordered = order.Lines.Where(o => string.Equals(o.Sku, l.Sku, StringComparison.OrdinalIgnoreCase)).ToList();
            if (ordered.Count == 0) throw new DomainException($"{l.Sku} is not on order {order.OrderNo}.");
            var orderedQty = ordered.Sum(o => o.Quantity);
            var alreadyReturned = previous.Where(p => string.Equals(p.Sku, l.Sku, StringComparison.OrdinalIgnoreCase)).Sum(p => p.Quantity);
            if (l.Quantity + alreadyReturned > orderedQty)
            {
                throw new DomainException($"Only {orderedQty - alreadyReturned} x {l.Sku} can be returned.");
            }
            var paidUnit = ordered.Sum(o => o.Quantity * o.UnitPrice - o.Discount) / orderedQty;
            ret.Lines.Add(new ReturnLine { Sku = l.Sku.ToUpperInvariant(), Quantity = l.Quantity, UnitPrice = Math.Round(paidUnit, 2), ReasonCode = l.ReasonCode });
        }
        if (ret.Lines.Count == 0) throw new DomainException("Nothing to return.");

        if (channel == "Web")
        {
            var label = await _carrier.CreateReturnAsync(new CarrierReturnRequest
            {
                Reference = ret.RmaNo,
                Sender = new CarrierParty
                {
                    Name = order.ShipTo?.ShipToName,
                    Street = order.ShipTo?.ShipToStreet,
                    PostalCode = order.ShipTo?.ShipToPostalCode,
                    City = order.ShipTo?.ShipToCity,
                    Country = order.ShipTo?.ShipToCountry,
                    Email = order.CustomerEmail,
                },
                Receiver = new CarrierParty { Name = "VanArsdel Returns", Street = "Logistiikkatie 4", PostalCode = "01510", City = "Vantaa", Country = "FI" },
            }, _options.CarrierApiKey);
            ret.ReturnTrackingNo = label.TrackingNumber;
            ret.ReturnLabelUrl = label.LabelUrl;
        }

        _db.Returns.Add(ret);
        _outbox.Add(new ReturnRegistered { ReturnId = ret.Id, RmaNo = ret.RmaNo, OrderNo = ret.OrderNo, Channel = channel, CustomerEmail = ret.CustomerEmail, ReturnLabelUrl = ret.ReturnLabelUrl });
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Return {RmaNo} registered for {OrderNo} ({Channel})", ret.RmaNo, ret.OrderNo, channel);
        return ret;
    }

    public async Task<ReturnRequest> ReceiveAsync(string rmaNo, string location, List<RegisterReturnLine> lines, string user, CancellationToken ct)
    {
        var ret = await LoadAsync(rmaNo, ct);
        ret.Receive(location, lines.Count == 0 ? ret.Lines.Select(l => (l.Sku, l.Quantity)) : lines.Select(l => (l.Sku, l.Quantity)), user, DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return ret;
    }

    /// <summary>Grades the items, restocks A and B grades, refunds to the original payment and completes the return.</summary>
    public async Task<ReturnRequest> InspectAndRefundAsync(string rmaNo, Dictionary<string, string> grades, string refundMethod, string user, CancellationToken ct)
    {
        var ret = await LoadAsync(rmaNo, ct);
        var refund = ret.Inspect(grades.ToDictionary(g => g.Key.ToUpperInvariant(), g => g.Value.ToUpperInvariant()));
        ret.RefundMethod = refundMethod ?? (ret.PaymentMethod == "POS" ? "Cash" : "Original");

        if (refund > 0 && ret.RefundMethod == "Original" && !string.IsNullOrEmpty(ret.PaymentReference))
        {
            try
            {
                var result = await _psp.RefundAsync(ret.PaymentReference, new PspRefundRequest { Amount = Money.ToMinor(refund), Reference = ret.RmaNo, Reason = "return" }, "refund-" + ret.RmaNo);
                ret.RefundReference = result.Id;
                refund = Money.FromMinor(result.Amount);
            }
            catch (ApiException ex)
            {
                throw new UpstreamException("PSP", $"Refund of {ret.RmaNo} failed with {(int)ex.StatusCode}", ex);
            }
        }

        ret.RefundAmount = refund;
        ret.Status = ReturnStatus.Completed;
        ret.CompletedUtc = DateTime.UtcNow;
        ret.HandledBy = user;

        var lines = ret.Lines.Where(l => l.ReceivedQuantity > 0)
            .Select(l => new ReturnStockLine { Sku = l.Sku, Quantity = l.ReceivedQuantity, Grade = l.Grade }).ToList();
        _outbox.Add(new ReturnRestocked { ReturnId = ret.Id, RmaNo = ret.RmaNo, LocationCode = ret.ReceivedAt ?? _options.ReturnsCentre, Lines = lines });
        _outbox.Add(new ReturnCompleted
        {
            ReturnId = ret.Id,
            RmaNo = ret.RmaNo,
            OrderNo = ret.OrderNo,
            StoreCode = ret.StoreCode,
            CustomerEmail = ret.CustomerEmail,
            LoyaltyCardNo = ret.LoyaltyCardNo,
            RefundAmount = ret.RefundAmount,
            Currency = ret.Currency,
            RefundMethod = ret.RefundMethod,
            CompletedUtc = ret.CompletedUtc.Value,
            Lines = lines,
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Return {RmaNo} completed, refund {Amount} {Currency} ({Method})", ret.RmaNo, ret.RefundAmount, ret.Currency, ret.RefundMethod);
        return ret;
    }

    public async Task<ReturnRequest> RejectAsync(string rmaNo, string reason, string user, CancellationToken ct)
    {
        var ret = await LoadAsync(rmaNo, ct);
        if (ret.Status == ReturnStatus.Completed) throw new DomainException("A completed return cannot be rejected.");
        ret.Status = ReturnStatus.Rejected;
        ret.RejectReason = reason;
        ret.HandledBy = user;
        await _db.SaveChangesAsync(ct);
        return ret;
    }

    public async Task<ReturnRequest> LoadAsync(string rmaNo, CancellationToken ct) =>
        await _db.Returns.Include(r => r.Lines).SingleOrDefaultAsync(r => r.RmaNo == rmaNo, ct) ?? throw new NotFoundException("Return", rmaNo);
}
