using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VanArsdel.Integration.Carrier;
using VanArsdel.Returns.Api.Data;
using VanArsdel.Returns.Api.Domain;

namespace VanArsdel.Returns.Api.Controllers;

/// <summary>Return parcel tracking: logs when the parcel is handed in and when it reaches the returns centre.</summary>
[ApiController]
[Route("webhooks/carrier")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class CarrierWebhookController : ControllerBase
{
    private readonly ReturnsDbContext _db;
    private readonly ReturnsOptions _options;
    private readonly ILogger<CarrierWebhookController> _logger;

    public CarrierWebhookController(ReturnsDbContext db, IOptions<ReturnsOptions> options, ILogger<CarrierWebhookController> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CarrierTrackingEvent e)
    {
        if (!string.Equals(Request.Headers["X-Carrier-Token"], _options.CarrierWebhookSecret, StringComparison.Ordinal)) return Unauthorized();
        var ret = await _db.Returns.AsNoTracking().SingleOrDefaultAsync(r => r.ReturnTrackingNo == e.TrackingNumber || r.RmaNo == e.Reference);
        if (ret != null)
        {
            _logger.LogInformation("Return parcel {RmaNo}: {Event} at {Location}", ret.RmaNo, e.Event, e.Location);
        }
        return Ok();
    }
}
