using Microsoft.AspNetCore.Mvc;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Tower.Models;
using Proseware.ControlTower.Tower.Services;

namespace Proseware.ControlTower.Tower.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController : ControllerBase
{
    private readonly AlertService _alerts;

    public AlertsController(AlertService alerts) => _alerts = alerts;

    [HttpGet]
    public Task<IReadOnlyList<AlertRow>> List([FromQuery] bool openOnly = true, [FromQuery] Severity? minSeverity = null, [FromQuery] int? shipmentId = null,
        [FromQuery] int take = 200, CancellationToken ct = default) =>
        _alerts.ListAsync(openOnly, minSeverity, shipmentId, take, ct);

    [HttpPost("{id:long}/acknowledge")]
    public async Task<ActionResult<AlertRow>> Acknowledge(long id, CancellationToken ct)
    {
        var row = await _alerts.AcknowledgeAsync(id, CurrentUser, ct);
        return row is null ? NotFound() : row;
    }

    [HttpPost("{id:long}/resolve")]
    public async Task<ActionResult<AlertRow>> Resolve(long id, [FromBody] ResolveAlertRequest request, CancellationToken ct)
    {
        if (request.Resolution?.Length > 400) return ValidationProblem("Resolution is limited to 400 characters.");
        var row = await _alerts.ResolveAsync(id, CurrentUser, request.Resolution ?? "", ct);
        return row is null ? NotFound() : row;
    }

    private string CurrentUser => User.FindFirst("name")?.Value ?? User.FindFirst("sub")?.Value ?? "unknown";
}
