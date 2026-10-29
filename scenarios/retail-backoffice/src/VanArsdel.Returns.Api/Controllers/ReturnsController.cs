using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts;
using VanArsdel.Returns.Api.Data;
using VanArsdel.Returns.Api.Domain;

namespace VanArsdel.Returns.Api.Controllers;

[ApiController]
[Route("api/returns")]
[Authorize(Policy = Policies.Read)]
public class ReturnsController : ControllerBase
{
    private readonly ReturnsDbContext _db;
    private readonly ReturnService _returns;

    public ReturnsController(ReturnsDbContext db, ReturnService returns)
    {
        _db = db;
        _returns = returns;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string status, [FromQuery] string orderNo, [FromQuery] int take = 100) =>
        Ok(await _db.Returns.AsNoTracking()
            .Where(r => (status == null || r.Status == status) && (orderNo == null || r.OrderNo == orderNo))
            .OrderByDescending(r => r.CreatedUtc).Take(Math.Clamp(take, 1, 500))
            .Select(r => new { r.RmaNo, r.OrderNo, r.Channel, r.StoreCode, r.Status, r.CustomerEmail, r.RefundAmount, r.Currency, r.CreatedUtc, r.CompletedUtc })
            .ToListAsync());

    [HttpGet("{rmaNo}")]
    public Task<ReturnRequest> Get(string rmaNo, CancellationToken ct) => _returns.LoadAsync(rmaNo, ct);

    /// <summary>Return registered by the customer in the web shop; a prepaid carrier label is created.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Register)]
    public async Task<IActionResult> Register([FromBody] RegisterReturn request, CancellationToken ct)
    {
        var ret = await _returns.RegisterAsync(request, "Web", null, ct);
        return CreatedAtAction(nameof(Get), new { rmaNo = ret.RmaNo }, new { ret.RmaNo, ret.ReturnLabelUrl, ret.ReturnTrackingNo });
    }

    /// <summary>Web order returned over the counter in a store: registered, received and refunded at once.</summary>
    [HttpPost("store/{storeCode}")]
    [Authorize(Policy = Policies.Handle)]
    public async Task<IActionResult> StoreReturn(string storeCode, [FromBody] StoreReturnRequest request, CancellationToken ct)
    {
        if (!User.HasAnyRole(Roles.Admin, Roles.Service, Roles.CustomerService) && !string.Equals(User.StoreCode(), storeCode, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }
        var ret = await _returns.RegisterAsync(request.Return, "Store", storeCode, ct);
        await _returns.ReceiveAsync(ret.RmaNo, storeCode, new List<RegisterReturnLine>(), User.UserName(), ct);
        ret = await _returns.InspectAndRefundAsync(ret.RmaNo, request.Grades, request.RefundMethod, User.UserName(), ct);
        return Ok(new { ret.RmaNo, ret.RefundAmount, ret.Currency, ret.RefundMethod });
    }

    [HttpPost("{rmaNo}/receive")]
    [Authorize(Policy = Policies.Handle)]
    public async Task<IActionResult> Receive(string rmaNo, [FromBody] ReceiveRequest request, CancellationToken ct)
    {
        var ret = await _returns.ReceiveAsync(rmaNo, request.Location, request.Lines, User.UserName(), ct);
        return Ok(new { ret.RmaNo, ret.Status });
    }

    [HttpPost("{rmaNo}/inspect")]
    [Authorize(Policy = Policies.Handle)]
    public async Task<IActionResult> Inspect(string rmaNo, [FromBody] InspectRequest request, CancellationToken ct)
    {
        var ret = await _returns.InspectAndRefundAsync(rmaNo, request.Grades, request.RefundMethod, User.UserName(), ct);
        return Ok(new { ret.RmaNo, ret.Status, ret.RefundAmount, ret.Currency, ret.RefundMethod });
    }

    [HttpPost("{rmaNo}/reject")]
    [Authorize(Policy = Policies.Handle)]
    public async Task<IActionResult> Reject(string rmaNo, [FromBody] RejectRequest request, CancellationToken ct)
    {
        var ret = await _returns.RejectAsync(rmaNo, request.Reason, User.UserName(), ct);
        return Ok(new { ret.RmaNo, ret.Status });
    }
}

public class StoreReturnRequest
{
    public RegisterReturn Return { get; set; } = new();
    public Dictionary<string, string> Grades { get; set; } = new();
    public string RefundMethod { get; set; }
}

public class ReceiveRequest
{
    public string Location { get; set; } = "DC01";
    public List<RegisterReturnLine> Lines { get; set; } = new();
}

public class InspectRequest
{
    public Dictionary<string, string> Grades { get; set; } = new();
    public string RefundMethod { get; set; }
}

public class RejectRequest
{
    public string Reason { get; set; } = "";
}

public class RegisterReturnValidator : AbstractValidator<RegisterReturn>
{
    public RegisterReturnValidator()
    {
        RuleFor(x => x.OrderNo).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Sku).NotEmpty();
            l.RuleFor(x => x.Quantity).InclusiveBetween(1, 20);
            l.RuleFor(x => x.ReasonCode).Must(r => ReturnReasons.All.Contains(r)).WithMessage("Unknown reason code.");
        });
    }
}
